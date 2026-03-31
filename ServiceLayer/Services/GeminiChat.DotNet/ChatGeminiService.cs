using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using OpenAI.Audio;
using OpenAI.Chat;

using DataBaseLayer.Models;
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
using System.Collections.Concurrent;
using ServiceLayer.Utils;


namespace ServiceLayer.Services.GeminiChat.DotNet
{
    internal class ChatGeminiService : BaseService, IChatService
    {
        private ChatProviderConfig _apiConfiguration;
        private IGeminiClient _client;
        private readonly IDynamicLocalizer _localizer;
        private readonly IRepository<AIBilingItem>? _aiBilingItemRepository;
        private readonly IRepository<CachedAIModel>? _cachedAIModelRepository;

        private class GeminiModelCache
        {
            internal DateTime? LastUpdates { get; set; }
            internal required IReadOnlyList<OpenAIModel> Models { get; set; }
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
            _aiBilingItemRepository = _serviceProvider.GetService<IRepository<AIBilingItem>>();
            _cachedAIModelRepository = _serviceProvider.GetService<IRepository<CachedAIModel>>();
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
            var appConfig = _serviceProvider.GetConfiguration<AppSettings>();
            var expiryHours = appConfig?.TelegramBotConfiguration?.ModelCacheExpiryHours ?? 48;
            var providerName = _apiConfiguration.Name;

            if (_cachedAIModelRepository != null)
            {
                var cachedModels = _cachedAIModelRepository.GetAll()
                    .Where(p => p.ProviderName == providerName && p.IsAvailable)
                    .ToList();

                if (cachedModels.Any())
                {
                    var oldestChecked = cachedModels.Min(p => p.LastChecked);
                    if ((DateTime.UtcNow - oldestChecked).TotalHours < expiryHours)
                    {
                        return cachedModels.Select(p => new OpenAIModel(p.ModelId)).ToList();
                    }
                }
            }

            if (_geminiModelCache != null && _geminiModelCache.LastUpdates.HasValue && (DateTime.UtcNow - _geminiModelCache.LastUpdates.Value).TotalHours < expiryHours)
            {
                return _geminiModelCache.Models;
            }

            await RefreshAvailibleModels();
            return _geminiModelCache?.Models ?? new List<OpenAIModel> 
            { 
                new OpenAIModel((string)AiModel.GeminiPro),
                new OpenAIModel((string)AiModel.GeminiFlash)
            };
        }

        public async Task RefreshAvailibleModels(bool validate = true)
        {
            var providerName = _apiConfiguration.Name;
            _logger.LogInformation("Refreshing {0} models cache (validate={1})...", providerName, validate);
            var result = new ConcurrentBag<OpenAIModel>();
            try
            {
                var models = _client.ListModelsAsync();
                var modelInfos = new List<GoogleModel>();
                await foreach (var modelInfo in models)
                {
                    modelInfos.Add(modelInfo);
                }

                if (validate)
                {
                    var validationTasks = modelInfos.Select(async modelInfo => 
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
                                return;
                            }

                            try
                            {
                                await _client.GenerateContentAsync(modelInfo.Name, "Hi");
                                result.Add(new OpenAIModel(modelName));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug("Gemini model {0} validation failed: {1}", modelInfo.Name, ex.Message);
                            }
                        }
                    });

                    await Task.WhenAll(validationTasks);
                }
                else
                {
                     foreach (var modelInfo in modelInfos)
                     {
                        var modelName = modelInfo.Name.StartsWith("models/") 
                            ? modelInfo.Name.Substring("models/".Length) 
                            : modelInfo.Name;
                        if (modelName.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase))
                            result.Add(new OpenAIModel(modelName));
                     }
                }

                if (_cachedAIModelRepository != null)
                {
                    var existingModels = _cachedAIModelRepository.GetAll()
                        .Where(p => p.ProviderName == providerName)
                        .ToList();
                    foreach (var model in result)
                    {
                        var existing = existingModels.FirstOrDefault(p => p.ModelId == model.Id);
                        if (existing != null)
                        {
                            existing.LastChecked = DateTime.UtcNow;
                            existing.IsAvailable = true;
                            _cachedAIModelRepository.Update(existing);
                        }
                        else
                        {
                            _cachedAIModelRepository.Add(new CachedAIModel
                            {
                                ModelId = model.Id,
                                ProviderName = providerName,
                                IsAvailable = true,
                                LastChecked = DateTime.UtcNow,
                                FriendlyName = model.Id
                            });
                        }
                    }

                    // Mark those not in result as unavailable
                    foreach (var existing in existingModels.Where(e => result.All(r => r.Id != e.ModelId)))
                    {
                        if (existing.IsAvailable)
                        {
                            existing.IsAvailable = false;
                            existing.LastChecked = DateTime.UtcNow;
                            _cachedAIModelRepository.Update(existing);
                        }
                    }
                    await _cachedAIModelRepository.SaveChanges();
                }

                _geminiModelCache = new GeminiModelCache
                {
                    LastUpdates = DateTime.UtcNow,
                    Models = result.ToList()
                };
                _logger.LogInformation("{0} models cache refreshed. Found {1} available models.", providerName, result.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing {0} models cache.", providerName);
            }
        }

        public async Task<ChatServiceResponse> SendMessages2ChatAsync(long telegramChatId, long telegramUserId, List<AiMessage> messages, string? model = null)
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
            if (_aiBilingItemRepository == null) return;

            var dbAIBilingItem = new AIBilingItem
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
            _aiBilingItemRepository.Add(dbAIBilingItem);
            await _aiBilingItemRepository.SaveChanges();

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
                        user.Balance -= dbAIBilingItem.Cost ?? 0;
                    }
                    user.LastAiInteraction = DateTime.UtcNow;
                    userRepository.Update(user);
                    await userRepository.SaveChanges();
                }
            }
        }

        public Task<ChatServiceResponse> GenerateImage(long chatId, long telegramUserId, string prompt, string? modelName = null)
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

        public async Task<ChatServiceResponse> AnalyzeImageAsync(long chatId, long telegramUserId, string? imageUrl, string? filePath = null, string? prompt = null, string? model = null)
        {
            throw new NotSupportedException("Gemini provider analysis not implemented yet.");
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


