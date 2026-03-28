using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Images;
using OpenAI.Models;
using ServiceLayer.Services.GptChat.Configurations;
using ServiceLayer.Services.GptChat.Models;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Net.Http;
using ServiceLayer.Services.GptChat.Models;

namespace ServiceLayer.Services.GptChat;

public class ChatGptService : BaseService, IChatService
{
    private class GptModelCache
    {
        internal DateTime? LastUpdates { get; set; }
        internal IReadOnlyList<Model> Models { get; set; }
    }
    private const int defTokens = 1000;
    internal static readonly ConcurrentDictionary<string, GptModelCost> _liveModelsCosts = new();
    internal static DateTime _lastPriceUpdate = DateTime.MinValue;
    internal static readonly object _priceLock = new();

    private static Dictionary<string, GptModelCost> gptModelsCosts = new Dictionary<string, GptModelCost>()
    {
        {"gpt-4-1106-preview",  new GptModelCost(defTokens, 0.01M, 0.03M)},
        {"gpt-4-1106-vision-preview",  new GptModelCost(defTokens, 0.01M, 0.03M)},
        {"gpt-4",  new GptModelCost(defTokens, 0.03M, 0.06M)},
        {"gpt-4-32k",  new GptModelCost(defTokens, 0.06M, 0.12M)},
        {"gpt-3.5-turbo-1106",  new GptModelCost(defTokens, 0.001M, 0.002M)},
        {"gpt-3.5-turbo-instruct",  new GptModelCost(defTokens, 0.0015M, 0.002M)},
        {"gpt-4o-mini",  new GptModelCost(defTokens, 0.00015M, 0.0006M)},
        {"gpt-4o",  new GptModelCost(defTokens, 0.005M, 0.015M)},

        {"Code interpreter",  new GptModelCost(1, 0.03M, 0.0M)},
        {"Retrieval",  new GptModelCost(1, 0.2M, 0.0M)},

        {Model.GPT3_5_Turbo,  new GptModelCost(defTokens, 0.003M, 0.006M)},
        {Model.Davinci,  new GptModelCost(defTokens, 0.012M, 0.012M)},
        {Model.Babbage,  new GptModelCost(defTokens, 0.0016M, 0.0016M)},

        {Model.DallE_3,  new GptModelCost(1, 0.0M, 0.04M)},
        {Model.DallE_2,  new GptModelCost(1, 0.0M, 0.02M)},

        {Model.Whisper1,  new GptModelCost(1, 0.0M, 0.006M)},
        {Model.TTS_1,  new GptModelCost(1, 0.0M, 0.015M)},
        {Model.TTS_1HD,  new GptModelCost(1, 0.0M, 0.03M)},
    };

    private readonly OpenAIClient _api;
    private readonly GptChatConfiguration _gptChatConfiguration;
    private readonly IRepository<GptBilingItem>? _gptBilingItemRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private static GptModelCache gptModelCache = null;

    public ChatGptService(IServiceProvider serviceProvider, ILogger<ChatGptService> logger,
        AppSettings appSettings, IHttpClientFactory httpClientFactory, OpenAIClient? openAiClient = null)
        : base(serviceProvider, logger)
    {
        _api = openAiClient ?? new OpenAIClient(appSettings.GptChatConfiguration.APIKey);
        _gptChatConfiguration = appSettings.GptChatConfiguration;
        _gptBilingItemRepository = _serviceProvider.GetService<IRepository<GptBilingItem>>();
        _httpClientFactory = httpClientFactory;
        
        // Initial async update
        _ = RefreshModelPricesAsync();
    }
    public async Task<IReadOnlyList<Model>> GetAvailibleModels()
    {
        IReadOnlyList<Model> modelsResponce = await _api.ModelsEndpoint.GetModelsAsync();
        var result = new List<Model>();
        if (gptModelCache != null && (DateTime.UtcNow - gptModelCache.LastUpdates.Value).Hours < 6)
        {
            return gptModelCache.Models;
        }
        foreach (Model model in modelsResponce)
        {
            // Pre-filter: only attempt chat check for known chat models or prefixes
            if (!model.Id.StartsWith("gpt-") && 
                !model.Id.StartsWith("o1-") && 
                !gptModelsCosts.ContainsKey(model.Id))
            {
                continue;
            }

            try
            {
                ChatRequest chatRequest = new ChatRequest(new[] { new Message(Role.User, "Hi") }
                , model: model.Id
                );
                ChatResponse teatResult = await _api.ChatEndpoint.GetCompletionAsync(chatRequest);
            }
            catch
            {
                continue;
            }
            result.Add(model);
        }
        gptModelCache = new GptModelCache
        {
            LastUpdates = DateTime.UtcNow,
            Models = result
        };
        return result;
    }
    public async Task<string> Ask(long chatId, long userId, string message)
    {
        var chatResponce = await SendMessages2ChatAsync(chatId, userId, new List<Message>()
        {
            new Message(Role.User, message)
        });
        _logger.LogInformation("Usage for ask '{0}: {1}", message,
            chatResponce.Usage?.TotalTokens);
        var sb = new StringBuilder();
        foreach (var responce in chatResponce.Choices)
        {
            sb.AppendLine(responce.Message.ToString());
        }
        return sb.ToString();
    }
    public async Task<ChatResponse> SendMessages2ChatAsync(long telegramChatId, long telegramUserId, List<Message> messages)
    {
        ChatRequest chatRequest = new ChatRequest(messages, model:
            _gptChatConfiguration.ModelName
            //OpenAI.Models.Model.GPT3_5_Turbo_16K
            );
        ChatResponse result = await _api.ChatEndpoint.GetCompletionAsync(chatRequest);
        await SaveBilling(_gptChatConfiguration.ModelName, telegramChatId, telegramUserId, result.Usage);
        return result;
    }

    private async Task SaveBilling(string modelName, long telegramChatId, long telegramUserId, GptUsage usage)
    {
        if ((DateTime.UtcNow - _lastPriceUpdate).TotalHours > 24)
        {
            _ = RefreshModelPricesAsync();
        }

        if (!_liveModelsCosts.TryGetValue(modelName, out GptModelCost? modelCost))
        {
            gptModelsCosts.TryGetValue(modelName, out modelCost);
        }

        decimal? cost = modelCost == null
            ? 0M
            : ((usage.PromptTokens * modelCost.Input) + (usage.CompletionTokens * modelCost.Output)) / modelCost.PerTokens;
        var dbGptBilingItem = new GptBilingItem
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
        };
        _gptBilingItemRepository.Add(dbGptBilingItem);
        await _gptBilingItemRepository.SaveChanges();
    }

    public async Task<IReadOnlyList<string>> GenerateImage(long chatId, long telegramUserId, string prompt)
    {
        var modelName = Model.DallE_3;
        ImageGenerationRequest request = new ImageGenerationRequest(prompt, modelName, 1, null, ImageResponseFormat.Url);
        IReadOnlyList<ImageResult> imageResults = await _api.ImagesEndPoint.GenerateImageAsync(request);
        _logger.LogInformation("Use model: {0} for generate image", request.Model);
        var results = imageResults.Select(p => p.Url).ToList();
        await SaveBilling(request.Model, chatId, telegramUserId, new GptUsage(0, 1, 1));
        return results;
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
        string result = await _api.AudioEndpoint.CreateTranscriptionTextAsync(request);
        await SaveBilling(request.Model, chatId, telegramUserId, new GptUsage(0, 1, 1));
        return result;
    }

    internal async Task<IReadOnlyList<string>> CreateImageEditAsync(long chatId, long telegramUserId,
        string filePath, string? messageText)
    {
        var imageEditRequest = new ImageEditRequest(
            imagePath: filePath,
            maskPath: filePath,
            prompt: messageText ?? "",
            numberOfResults: 1,
            size: OpenAI.Images.ImageSize.Medium);
        var imagesResults = await _api.ImagesEndPoint.CreateImageEditAsync(imageEditRequest);
        var results = imagesResults.Select(p => p.Url).ToList();
        return results;
    }

    internal async Task<(bool, string)> SetGPTModel(string? modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return (false, "Пустое название модели");
        try
        {
            ChatRequest chatRequest = new ChatRequest(new[] { new Message(Role.User, "Hi") }, model:
            modelName
            );
            ChatResponse result = await _api.ChatEndpoint.GetCompletionAsync(chatRequest);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        _gptChatConfiguration.ModelName = modelName;
        return (true, string.Empty);
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
                        if (string.Equals(info.Provider, "openai", StringComparison.OrdinalIgnoreCase) 
                            && info.InputCostPerToken.HasValue 
                            && info.OutputCostPerToken.HasValue)
                        {
                            var inputPrice = (decimal)info.InputCostPerToken.Value * defTokens;
                            var outputPrice = (decimal)info.OutputCostPerToken.Value * defTokens;
                            _liveModelsCosts[modelId] = new GptModelCost(defTokens, inputPrice, outputPrice);
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
