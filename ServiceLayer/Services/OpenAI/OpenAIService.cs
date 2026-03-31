using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using ServiceLayer.Constans;
using ServiceLayer.Services.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Images;
using OpenAI.Models;

using ServiceLayer.Models;
using ServiceLayer.Services.OpenAI.Models;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Net.Http;
using ServiceLayer.Utils;
using System.Diagnostics;

namespace ServiceLayer.Services.OpenAI;

internal class OpenAIService : BaseService, IChatService
{
    private class OpenAIModelCache
    {
        internal DateTime? LastUpdates { get; set; }
        internal required IReadOnlyList<Model> Models { get; set; }
    }
    private const int defTokens = 1000;
    internal static readonly ConcurrentDictionary<string, AIModelCost> _liveModelsCosts = new();
    internal static DateTime _lastPriceUpdate = DateTime.MinValue;
    internal static readonly object _priceLock = new();

    private static Dictionary<string, AIModelCost> aiModelsCosts = new Dictionary<string, AIModelCost>()
    {
        {"gpt-4-1106-preview",  new AIModelCost(defTokens, 0.01M, 0.03M)},
        {"gpt-4-1106-vision-preview",  new AIModelCost(defTokens, 0.01M, 0.03M)},
        {"gpt-4",  new AIModelCost(defTokens, 0.03M, 0.06M)},
        {"gpt-4-32k",  new AIModelCost(defTokens, 0.06M, 0.12M)},
        {"gpt-3.5-turbo-1106",  new AIModelCost(defTokens, 0.001M, 0.002M)},
        {"gpt-3.5-turbo-instruct",  new AIModelCost(defTokens, 0.0015M, 0.002M)},
        {AiModel.Gpt4oMini,  new AIModelCost(defTokens, 0.00015M, 0.0006M)},
        {AiModel.Gpt4o,  new AIModelCost(defTokens, 0.005M, 0.015M)},
        {AiModel.DeepSeekChat, new AIModelCost(defTokens, 0.00007M, 0.0011M)},
        {AiModel.GrokBeta, new AIModelCost(defTokens, 0.005M, 0.015M)},

        {"Code interpreter",  new AIModelCost(1, 0.03M, 0.0M)},
        {"Retrieval",  new AIModelCost(1, 0.2M, 0.0M)},

        {Model.GPT3_5_Turbo,  new AIModelCost(defTokens, 0.003M, 0.006M)},
        {Model.Davinci,  new AIModelCost(defTokens, 0.012M, 0.012M)},
        {Model.Babbage,  new AIModelCost(defTokens, 0.0016M, 0.0016M)},

        {Model.DallE_3,  new AIModelCost(1, 0.0M, 0.04M)},
        {Model.DallE_2,  new AIModelCost(1, 0.0M, 0.02M)},

        {Model.Whisper1,  new AIModelCost(1, 0.0M, 0.006M)},
        {Model.TTS_1,  new AIModelCost(1, 0.0M, 0.015M)},
        {Model.TTS_1HD,  new AIModelCost(1, 0.0M, 0.03M)},
    };

    private readonly OpenAIClient _api;
    private readonly ChatProviderConfig _chatProviderConfiguration;
    private readonly IRepository<AIBilingItem>? _aiBilingItemRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDynamicLocalizer _localizer;
    private OpenAIModelCache? openAiModelCache = null;

    public OpenAIService(IServiceProvider serviceProvider, ILogger<OpenAIService> logger,
        ChatProviderConfig chatProviderConfig, IHttpClientFactory httpClientFactory, 
        IDynamicLocalizer localizer, OpenAIClient? openAiClient = null)
        : base(serviceProvider, logger)
    {
        _chatProviderConfiguration = chatProviderConfig;
        if (openAiClient != null)
        {
            _api = openAiClient;
        }
        else
        {
            var auth = new OpenAIAuthentication(_chatProviderConfiguration.ApiKey);
            var domain = _chatProviderConfiguration.BaseUrl ?? "";
            if (!string.IsNullOrEmpty(domain))
            {
                // Sanitize: strip protocol and path to get only the domain
                domain = domain.Replace("https://", "").Replace("http://", "").Split('/')[0];
            }
            
            var settings = string.IsNullOrEmpty(domain) 
                ? new OpenAISettings() 
                : new OpenAISettings(domain: domain);
            
            var httpClient = httpClientFactory.CreateClient("OpenAIClient");
            httpClient.Timeout = TimeSpan.FromMinutes(_chatProviderConfiguration.TimeoutMinutes);
            _api = new OpenAIClient(auth, settings, httpClient);
        }
        _aiBilingItemRepository = _serviceProvider.GetService<IRepository<AIBilingItem>>();
        _httpClientFactory = httpClientFactory;
        _localizer = localizer;
        
        // Initial async update
        _ = RefreshModelPricesAsync();
    }
    public async Task<IReadOnlyList<Model>> GetAvailibleModels(long? userId = null, bool validateModels = true)
    {
        IReadOnlyList<Model> modelsResponce = await _api.ModelsEndpoint.GetModelsAsync();
        var result = new List<Model>();
        if (openAiModelCache != null && (DateTime.UtcNow - openAiModelCache.LastUpdates.Value).Hours < 6)
        {
            return openAiModelCache.Models;
        }
        foreach (Model model in modelsResponce)
        {
            // Pre-filter: only attempt chat check for known model prefixes or if we have cost info
            string modelId = model.Id.ToLowerInvariant();
            if (!modelId.StartsWith("gpt-") && 
                !modelId.StartsWith("o1-") && 
                !modelId.StartsWith("o3-") &&
                !modelId.StartsWith("deepseek-") &&
                !modelId.StartsWith("grok-") &&
                !aiModelsCosts.ContainsKey(model.Id))
            {
                continue;
            }

            if (validateModels)
            {
                try
                {
                    ChatRequest chatRequest = new ChatRequest(new[] { new AiMessage(Role.User, "Hi") }
                    , model: model.Id
                    );
                    ChatResponse teatResult = await _api.ChatEndpoint.GetCompletionAsync(chatRequest);
                }
                catch
                {
                    continue;
                }
            }
            result.Add(model);
        }
        openAiModelCache = new OpenAIModelCache
        {
            LastUpdates = DateTime.UtcNow,
            Models = result
        };
        return result;
    }
    public async Task<string> Ask(long chatId, long userId, string message)
    {
        var chatServiceResponce = await SendMessages2ChatAsync(chatId, userId, new List<AiMessage>()
        {
            new AiMessage(Role.User, message)
        });
        _logger.LogInformation("Usage for ask '{0}: {1}", message,
            chatServiceResponce.TotalTokens);
        var sb = new StringBuilder();
        foreach (var choice in chatServiceResponce.Choices)
        {
            sb.AppendLine(choice);
        }
        return sb.ToString();
    }
    public async Task<ChatServiceResponse> SendMessages2ChatAsync(long telegramChatId, long telegramUserId, List<AiMessage> messages, string? model = null)
    {
        var modelName = model;
        if (string.IsNullOrEmpty(modelName))
        {
            modelName = string.IsNullOrEmpty(_chatProviderConfiguration.ModelName) 
                ? (string)AiModel.Gpt4oMini 
                : _chatProviderConfiguration.ModelName;
        }

        ChatRequest chatRequest = new ChatRequest(messages, model: modelName);
        ChatResponse result;
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("OpenAI Request: Model={0}, MessagesCount={1}", modelName, messages.Count);
            result = await _api.ChatEndpoint.GetCompletionAsync(chatRequest);
            if (result == null || result.Choices == null || !result.Choices.Any())
            {
                _logger.LogWarning("OpenAI returned an empty response for {0}", modelName);
                throw new Exception($"OpenAI returned an empty response for {modelName}");
            }
            _logger.LogInformation("OpenAI Response: Choices={0}, Tokens={1}", result.Choices.Count, result.Usage?.TotalTokens);
        }
        catch (Exception ex)
        {
            throw AiErrorHelper.HandleAndGetException(_logger, ex, _chatProviderConfiguration.Name, nameof(SendMessages2ChatAsync));
        }
        sw.Stop();

        decimal? cost = await SaveBilling(modelName, _chatProviderConfiguration.Name, telegramChatId, telegramUserId, result.Usage);

        var latency = sw.Elapsed.TotalSeconds;
        var tps = result.Usage?.TotalTokens / latency;

        return new ChatServiceResponse
        {
            Choices = result.Choices.Select(p => p.Message.ToString()).ToList(),
            TotalTokens = result.Usage?.TotalTokens,
            PromptTokens = result.Usage?.PromptTokens,
            CompletionTokens = result.Usage?.CompletionTokens,
            // ReasoningTokens might be null or not exist on older SDK versions
            // ReasoningTokens = result.Usage?.ReasoningTokens, 
            Cost = cost,
            LatencySeconds = latency,
            TokensPerSecond = tps,
            ProviderName = _chatProviderConfiguration.Name,
            ModelName = modelName
        };
    }

    private async Task<decimal?> SaveBilling(string modelName, string providerName, long telegramChatId, long telegramUserId, AIUsage usage)
    {
        if ((DateTime.UtcNow - _lastPriceUpdate).TotalHours > 24)
        {
            _ = RefreshModelPricesAsync();
        }

        if (!_liveModelsCosts.TryGetValue(modelName, out AIModelCost? modelCost))
        {
            aiModelsCosts.TryGetValue(modelName, out modelCost);
        }

        decimal? cost = modelCost == null
            ? 0M
            : ((usage.PromptTokens * modelCost.Input) + (usage.CompletionTokens * modelCost.Output)) / modelCost.PerTokens;
        
        var dbAIBilingItem = new AIBilingItem
        {
            CreationDate = DateTime.UtcNow,
            ModelName = modelName,
            ModifiedDate = DateTime.UtcNow,
            TelegramChatInfoId = telegramChatId,
            TelegramUserInfoId = telegramUserId,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            TotalTokens = usage.TotalTokens,
            Cost = cost,
            ProviderName = providerName
        };
        _aiBilingItemRepository.Add(dbAIBilingItem);
        // Update user balance
        var userRepository = _serviceProvider.GetService<IRepository<TelegramUserInfo>>();
        if (userRepository != null)
        {
            var user = await userRepository.Get(p => p.Id == telegramUserId);
            if (user != null)
            {
                var appConfig = _serviceProvider.GetConfiguration<AppSettings>();
                var config = appConfig?.TelegramBotConfiguration;
                bool isIgnored = (config?.IgnoredBalanceUserIds?.Contains(telegramUserId) == true) ||
                                 (config?.OwnerId == telegramUserId);
                bool isFree = config?.InitialBalance == 0;

                if (!isIgnored && !isFree)
                {
                    user.Balance -= cost ?? 0;
                    user.BalanceModifiedAt = DateTime.UtcNow;
                }
                user.LastAiInteraction = DateTime.UtcNow;
                userRepository.Update(user);
            }
        }

        return await _aiBilingItemRepository.SaveChanges() > 0 ? (decimal?)cost : null;
    }

    public async Task<ChatServiceResponse> GenerateImage(long chatId, long telegramUserId, string prompt, string? modelName = null)
    {
        modelName ??= _chatProviderConfiguration.DrawingModelName 
                 ?? _chatProviderConfiguration.ProviderType.DefaultDrawingModel 
                 ?? _chatProviderConfiguration.ModelName 
                 ?? (string)AiModel.DallE3;

        ImageGenerationRequest request = new ImageGenerationRequest(prompt, modelName, 1, null, ImageResponseFormat.Url);
        IReadOnlyList<ImageResult> imageResults;
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            imageResults = await _api.ImagesEndPoint.GenerateImageAsync(request);
        }
        catch (Exception ex)
        {
            throw AiErrorHelper.HandleAndGetException(_logger, ex, _chatProviderConfiguration.Name, nameof(GenerateImage));
        }
        sw.Stop();
        _logger.LogInformation("Use model: {0} for generate image", request.Model);
        var results = imageResults.Select(p => p.Url).ToList();
        
        var usage = new AIUsage(0, 1, 1);
        decimal? cost = await SaveBilling(request.Model, _chatProviderConfiguration.Name, chatId, telegramUserId, usage);

        return new ChatServiceResponse
        {
            Choices = results,
            TotalTokens = usage.TotalTokens,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            Cost = cost,
            LatencySeconds = sw.Elapsed.TotalSeconds,
            TokensPerSecond = 1.0 / sw.Elapsed.TotalSeconds, // 1 image per response
            ProviderName = _chatProviderConfiguration.Name,
            ModelName = modelName
        };
    }
    public async Task<string> AudioTranscription(long chatId, long telegramUserId,
        Stream audio, string audioName, string model = null,
        string prompt = null, AudioResponseFormat responseFormat = AudioResponseFormat.Json,
        int? temperature = null, string language = null)
    {
        var request = new AudioTranscriptionRequest(
            audio: audio,
            audioName: audioName,
            model: model,
            prompt: prompt,
            responseFormat: responseFormat,
            temperature: (float?)temperature,
            language: language);
        string result;
        try
        {
            result = await _api.AudioEndpoint.CreateTranscriptionTextAsync(request);
        }
        catch (Exception ex)
        {
            throw AiErrorHelper.HandleAndGetException(_logger, ex, _chatProviderConfiguration.Name, nameof(AudioTranscription));
        }
        await SaveBilling(request.Model, _chatProviderConfiguration.Name, chatId, telegramUserId, new AIUsage(0, 1, 1));
        return result;
    }

    public async Task<ChatServiceResponse> AnalyzeImageAsync(long chatId, long telegramUserId, string? imageUrl, string? filePath = null, string? prompt = null, string? model = null)
    {
        var appSettings = _serviceProvider.GetConfiguration<AppSettings>();
        var visionModel = model ?? appSettings?.TelegramBotConfiguration?.AiTaskSettings?.Vision?.ModelName ?? (string)AiModel.Gpt4o;
        var finalPrompt = prompt ?? "Describe this image.";

        string? finalImageUrl = imageUrl;
        string? finalFilePath = filePath;

        // Safety: if imageUrl looks like a local path, treat it as filePath
        if (!string.IsNullOrEmpty(finalImageUrl) && finalImageUrl.StartsWith("/"))
        {
            finalFilePath = finalImageUrl;
            finalImageUrl = null;
        }

        if (string.IsNullOrEmpty(finalImageUrl) && !string.IsNullOrEmpty(finalFilePath))
        {
            var bytes = await File.ReadAllBytesAsync(finalFilePath);
            var base64 = Convert.ToBase64String(bytes);
            var extension = Path.GetExtension(finalFilePath).TrimStart('.').ToLower();
            var mimeType = (extension == "png" || extension == "webp") ? $"image/{extension}" : "image/jpeg";
            finalImageUrl = $"data:{mimeType};base64,{base64}";
        }

        if (string.IsNullOrEmpty(finalImageUrl))
        {
            throw new ArgumentException("imageUrl");
        }

        _logger.LogInformation("Vision Request: Model={Model}, URL_Start={URLStart}", visionModel, finalImageUrl.Length > 50 ? finalImageUrl.Substring(0, 50) : finalImageUrl);

        var messages = new List<AiMessage>
        {
            new AiMessage(Role.System, "You are a visual analysis assistant. Provide direct and concise descriptions or answers based on the image. Do not provide tutorials, advice on how to edit images, or general chat unless relevant to the visual content."),
            new AiMessage(Role.User, new List<Content>
            {
                finalPrompt,
                new ImageUrl(finalImageUrl)
            })
        };

        _logger.LogInformation("Analyzing image via SendMessages2ChatAsync: Model={0}", visionModel);
        var response = await SendMessages2ChatAsync(chatId, telegramUserId, messages, visionModel);
        _logger.LogInformation("Image analysis completed: ChoicesCount={0}", response.Choices?.Count ?? 0);
        return response;
    }

    public async Task<ChatServiceResponse> CreateImageEditAsync(long chatId, long telegramUserId,
        string filePath, string? messageText)
    {
        var modelName = _chatProviderConfiguration.DrawingModelName 
            ?? _chatProviderConfiguration.ProviderType.DefaultDrawingModel 
            ?? _chatProviderConfiguration.ModelName 
            ?? (string)AiModel.DallE2; // Default for edits

        var imageEditRequest = new ImageEditRequest(prompt: messageText, imagePath: filePath,
            model: modelName, responseFormat: ImageResponseFormat.Url);
        
        IReadOnlyList<ImageResult> imagesResults;
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            imagesResults = await _api.ImagesEndPoint.CreateImageEditAsync(imageEditRequest);
        }
        catch (Exception ex)
        {
            throw AiErrorHelper.HandleAndGetException(_logger, ex, _chatProviderConfiguration.Name, nameof(CreateImageEditAsync));
        }
        sw.Stop();
        var results = imagesResults.Select(p => p.Url).ToList();
        
        var usage = new AIUsage(0, 1, 1);
        decimal? cost = await SaveBilling(modelName, _chatProviderConfiguration.Name, chatId, telegramUserId, usage);

        return new ChatServiceResponse
        {
            Choices = results,
            TotalTokens = usage.TotalTokens,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            Cost = cost,
            LatencySeconds = sw.Elapsed.TotalSeconds,
            TokensPerSecond = 1.0 / sw.Elapsed.TotalSeconds,
            ProviderName = _chatProviderConfiguration.Name,
            ModelName = modelName
        };
    }

    public async Task<(bool, string)> SetGPTModel(string? modelName, long? userId = null)
    {
        if (string.IsNullOrEmpty(modelName)) return (false, _localizer["EmptyModelName"]);
        try
        {
            ChatRequest chatRequest = new ChatRequest(new[] { new AiMessage(Role.User, "Hi") }, model:
            modelName
            );
            ChatResponse result = await _api.ChatEndpoint.GetCompletionAsync(chatRequest);
        }
        catch (Exception ex)
        {
            AiErrorHelper.LogDetailedError(_logger, ex, _chatProviderConfiguration.Name, nameof(SetGPTModel));
            var details = AiErrorHelper.GetErrorDetails(ex, _chatProviderConfiguration.Name);
            return (false, details.UserMessage);
        }
        _chatProviderConfiguration.ModelName = modelName;
        return (true, string.Empty);
    }

    public Task<string?> GetSelectedModel(long userId)
    {
        return Task.FromResult<string?>(_chatProviderConfiguration.ModelName);
    }

    internal async Task RefreshModelPricesAsync()
    {
        lock (_priceLock)
        {
            if ((DateTime.UtcNow - _lastPriceUpdate).TotalMinutes < 60) return;
            _lastPriceUpdate = DateTime.UtcNow;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "GPTChatTelegramBot-PriceFetcher");
            var response = await client.GetAsync("https://raw.githubusercontent.com/BerriAI/litellm/main/model_prices_and_context_window.json");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<Dictionary<string, LiteLlmModelInfo>>(content);
                if (data != null)
                {
                    _logger.LogInformation("Downloaded {0} models from LiteLLM", data.Count);
                    foreach (var kvp in data)
                    {
                        var modelId = kvp.Key;
                        var info = kvp.Value;
                        if ((string.Equals(info.Provider, AiProvider.OpenAI.Value, StringComparison.OrdinalIgnoreCase) 
                             || string.Equals(info.Provider, AiProvider.DeepSeek.Value, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(info.Provider, AiProvider.Grok.Value, StringComparison.OrdinalIgnoreCase))
                            && info.InputCostPerToken.HasValue 
                            && info.OutputCostPerToken.HasValue)
                        {
                            var inputPrice = (decimal)info.InputCostPerToken.Value * defTokens;
                            var outputPrice = (decimal)info.OutputCostPerToken.Value * defTokens;
                            _liveModelsCosts[modelId] = new AIModelCost(defTokens, inputPrice, outputPrice);
                        }
                    }
                    _logger.LogInformation("Successfully updated {0} OpenAI model prices from LiteLLM", _liveModelsCosts.Count);
                }
                else
                {
                    _logger.LogWarning("Downloaded LiteLLM data is null");
                }
            }
            else
            {
                _logger.LogWarning("Failed to download LiteLLM prices: {0} {1}", response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update model prices from LiteLLM");
        }
    }
}
