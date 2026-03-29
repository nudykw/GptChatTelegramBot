using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using OpenAI.Audio;
using OpenAI.Chat;

using ServiceLayer.Services.GptChat;
using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.DependencyInjection;
using ServiceLayer.Constans;
using ServiceLayer.Services.Localization;
using OpenAIModel = OpenAI.Models.Model;
using GoogleModel = Google.GenAI.Types.Model;
using GoogleContent = Google.GenAI.Types.Content;
using GooglePart = Google.GenAI.Types.Part;
using OpenAI;
using System.Linq;
using ServiceLayer.Utils;


namespace ServiceLayer.Services.GeminiChat.DotNet
{
    internal class ChatGeminiService : BaseService, IChatService
    {
        private ChatProviderConfig _apiConfiguration;
        private IGeminiClient _client;
        private readonly IDynamicLocalizer _localizer;
        private readonly IRepository<GptBilingItem>? _gptBilingItemRepository;

        private class GeminiModelCache
        {
            internal DateTime? LastUpdates { get; set; }
            internal IReadOnlyList<OpenAIModel> Models { get; set; }
        }
        private GeminiModelCache? _geminiModelCache = null;

        public ChatGeminiService(IServiceProvider serviceProvider, ILogger<ChatGeminiService> logger,
            ChatProviderConfig chatProviderConfig, IDynamicLocalizer localizer)
            : this(serviceProvider, logger, chatProviderConfig, null, localizer)
        {
        }

        internal ChatGeminiService(IServiceProvider serviceProvider, ILogger<ChatGeminiService> logger,
            ChatProviderConfig chatProviderConfig, IGeminiClient? client, IDynamicLocalizer localizer)
            : base(serviceProvider, logger)
        {
            _apiConfiguration = chatProviderConfig;
            _client = client ?? new GeminiClientWrapper(_apiConfiguration.ApiKey);
            _gptBilingItemRepository = _serviceProvider.GetService<IRepository<GptBilingItem>>();
            _localizer = localizer;
        }

        private string GetModelName() => string.IsNullOrEmpty(_apiConfiguration.ModelName) 
            ? (string)AiModel.GeminiFlash 
            : _apiConfiguration.ModelName;

        public async Task<string> Ask(long chatId, long userId, string message)
        {
            var modelName = GetModelName();
            GenerateContentResponse response;
            try
            {
                response = await _client.GenerateContentAsync(modelName, message);
            }
            catch (Exception ex)
            {
                throw AiErrorHelper.HandleAndGetException(_logger, ex, _apiConfiguration.Name, nameof(Ask));
            }
            
            if (response.UsageMetadata != null)
                await SaveBilling(modelName, _apiConfiguration.Name, chatId, userId, response.UsageMetadata);
            
            return response.Text ?? string.Empty;
        }

        public async Task<IReadOnlyList<OpenAIModel>> GetAvailibleModels(long? userId = null, bool validateModels = true)
        {
            if (_geminiModelCache != null && _geminiModelCache.LastUpdates.HasValue && (DateTime.UtcNow - _geminiModelCache.LastUpdates.Value).TotalHours < 6)
            {
                return _geminiModelCache.Models;
            }

            var result = new List<OpenAIModel>();
            try
            {
                var models = _client.ListModelsAsync();
                await foreach (GoogleModel modelInfo in models)
                {
                    // Filter for models that support generating content
                    if (modelInfo.SupportedActions != null && 
                        modelInfo.SupportedActions.Any(a => string.Equals(a, "generateContent", StringComparison.OrdinalIgnoreCase)))
                    {
                        var modelName = modelInfo.Name.StartsWith("models/") 
                            ? modelInfo.Name.Substring("models/".Length) 
                            : modelInfo.Name;

                        if (!modelName.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (validateModels)
                        {
                            try
                            {
                                await _client.GenerateContentAsync(modelInfo.Name, "Hi");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Gemini model {ModelName} failed validation and will be skipped.", modelInfo.Name);
                                continue;
                            }
                        }
                        result.Add(new OpenAIModel(modelName));
                    }
                }

                _geminiModelCache = new GeminiModelCache
                {
                    LastUpdates = DateTime.UtcNow,
                    Models = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Gemini models dynamically. Falling back to hardcoded list.");
                return new List<OpenAIModel> 
                { 
                    new OpenAIModel((string)AiModel.GeminiPro),
                    new OpenAIModel((string)AiModel.GeminiFlash)
                };
            }

            return result;
        }

        public async Task<ChatServiceResponse> SendMessages2ChatAsync(long telegramChatId, long telegramUserId, List<Message> messages, string? model = null)
        {
            var modelName = model ?? GetModelName();
            
            var contents = messages.Select(m => new GoogleContent
            {
                Role = m.Role == Role.User ? "user" : "model",
                Parts = new List<GooglePart> { new GooglePart { Text = m.Content } }
            }).ToList();

            GenerateContentResponse response;
            try
            {
                response = await _client.GenerateContentAsync(modelName, contents);
            }
            catch (Exception ex)
            {
                throw AiErrorHelper.HandleAndGetException(_logger, ex, _apiConfiguration.Name, nameof(SendMessages2ChatAsync));
            }
            
            if (response.UsageMetadata != null)
                await SaveBilling(modelName, _apiConfiguration.Name, telegramChatId, telegramUserId, response.UsageMetadata);

            return new ChatServiceResponse
            {
                Choices = new List<string> { response.Text ?? string.Empty },
                ProviderName = _apiConfiguration.Name,
                ModelName = modelName
            };
        }

        private async Task SaveBilling(string modelName, string providerName, long telegramChatId, long telegramUserId, GenerateContentResponseUsageMetadata usage)
        {
            if (_gptBilingItemRepository == null) return;

            var dbGptBilingItem = new GptBilingItem
            {
                CreationDate = DateTime.UtcNow,
                ModelName = modelName,
                ProviderName = providerName,
                ModifiedDate = DateTime.UtcNow,
                TelegramChatInfoId = telegramChatId,
                TelegramUserInfoId = telegramUserId,
                PromptTokens = usage.PromptTokenCount,
                CompletionTokens = usage.CandidatesTokenCount,
                TotalTokens = usage.TotalTokenCount,
                Cost = 0 // Cost calculation can be added if price mapping is available
            };
            _gptBilingItemRepository.Add(dbGptBilingItem);
            await _gptBilingItemRepository.SaveChanges();

            // Update user balance
            var userRepository = _serviceProvider.GetService<IRepository<TelegramUserInfo>>();
            if (userRepository != null)
            {
                var user = await userRepository.Get(p => p.Id == telegramUserId);
                if (user != null)
                {
                    var appConfig = _serviceProvider.GetConfiguration<AppSettings>();
                    bool isIgnored = appConfig?.TelegramBotConfiguration?.IgnoredBalanceUserIds?.Contains(telegramUserId) == true ||
                                     appConfig?.TelegramBotConfiguration?.OwnerId == telegramUserId;

                    if (!isIgnored)
                    {
                        // Note: Cost is currently 0 for Gemini in this implementation
                        // If cost calculation is implemented for Gemini, it will deduct correctly
                        user.Balance -= dbGptBilingItem.Cost ?? 0;
                    }
                    user.LastAiInteraction = DateTime.UtcNow;
                    userRepository.Update(user);
                    await userRepository.SaveChanges();
                }
            }
        }

        public Task<ChatServiceResponse> GenerateImage(long chatId, long telegramUserId, string prompt)
        {
            throw new NotSupportedException("Gemini provider does not support image generation yet.");
        }

        public Task<string> AudioTranscription(long chatId, long telegramUserId, Stream audio, string audioName, 
            string model = null, string prompt = null, AudioResponseFormat responseFormat = AudioResponseFormat.Json, 
            int? temperature = null, string language = null)
        {
            throw new NotSupportedException("Gemini provider does not support audio transcription yet.");
        }

        public Task<ChatServiceResponse> CreateImageEditAsync(long chatId, long telegramUserId, string filePath, string? messageText)
        {
            throw new NotSupportedException("Gemini provider does not support image editing yet.");
        }

        public async Task<(bool, string)> SetGPTModel(string? modelName, long? userId = null)
        {
            if (string.IsNullOrEmpty(modelName)) return (false, _localizer["EmptyModelName"]);
            _apiConfiguration.ModelName = modelName;
            return (true, string.Empty);
        }

        public Task<string?> GetSelectedModel(long userId)
        {
            return Task.FromResult<string?>(_apiConfiguration.ModelName);
        }
    }
}


