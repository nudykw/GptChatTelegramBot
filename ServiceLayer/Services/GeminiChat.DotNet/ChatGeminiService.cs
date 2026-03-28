using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using OpenAI.Audio;
using OpenAI.Chat;
using ServiceLayer.Services.GeminiChat.DotNet.Configurations;
using ServiceLayer.Services.GptChat;
using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.DependencyInjection;
using ServiceLayer.Constans;
using Model = OpenAI.Models.Model;
using GoogleContent = Google.GenAI.Types.Content;
using GooglePart = Google.GenAI.Types.Part;
using OpenAI;

namespace ServiceLayer.Services.GeminiChat.DotNet
{
    internal class ChatGeminiService : BaseService, IChatService
    {
        private ChatProviderConfig _apiConfiguration;
        private Client _client;
        private readonly IRepository<GptBilingItem>? _gptBilingItemRepository;

        public ChatGeminiService(IServiceProvider serviceProvider, ILogger<ChatGeminiService> logger,
            ChatProviderConfig chatProviderConfig)
            : base(serviceProvider, logger)
        {
            _apiConfiguration = chatProviderConfig;
            
            // Note: Official SDK handles endpoints. If BaseUrl is needed, 
            // it would typically be configured via ClientOptions if supported.
            _client = new Client(apiKey: _apiConfiguration.ApiKey);
            _gptBilingItemRepository = _serviceProvider.GetService<IRepository<GptBilingItem>>();
        }

        private string GetModelName() => string.IsNullOrEmpty(_apiConfiguration.ModelName) 
            ? (string)AiModel.GeminiFlash 
            : _apiConfiguration.ModelName;

        public async Task<string> Ask(long chatId, long userId, string message)
        {
            var modelName = GetModelName();
            var response = await _client.Models.GenerateContentAsync(modelName, message);
            
            if (response.UsageMetadata != null)
                await SaveBilling(modelName, _apiConfiguration.Name, chatId, userId, response.UsageMetadata);
            
            return response.Text ?? string.Empty;
        }

        public Task<IReadOnlyList<Model>> GetAvailibleModels(long? userId = null)
        {
            IReadOnlyList<Model> result = new List<Model>() 
            { 
                new Model(AiModel.GeminiPro),
                new Model(AiModel.GeminiFlash)
            };
            return Task.FromResult(result);
        }

        public async Task<ChatServiceResponse> SendMessages2ChatAsync(long telegramChatId, long telegramUserId, List<Message> messages)
        {
            var modelName = GetModelName();
            
            var contents = messages.Select(m => new GoogleContent
            {
                Role = m.Role == Role.User ? "user" : "model",
                Parts = new List<GooglePart> { new GooglePart { Text = m.Content } }
            }).ToList();

            // Last message is the user prompt in GenerateContentAsync, or we can use the contents list directly
            var response = await _client.Models.GenerateContentAsync(modelName, contents);
            
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
        }

        public Task<IReadOnlyList<string>> GenerateImage(long chatId, long telegramUserId, string prompt)
        {
            throw new NotSupportedException("Gemini provider does not support image generation yet.");
        }

        public Task<string> AudioTranscription(long chatId, long telegramUserId, Stream audio, string audioName, 
            string model = null, string prompt = null, AudioResponseFormat responseFormat = AudioResponseFormat.Json, 
            int? temperature = null, string language = null)
        {
            throw new NotSupportedException("Gemini provider does not support audio transcription yet.");
        }

        public Task<IReadOnlyList<string>> CreateImageEditAsync(long chatId, long telegramUserId, string filePath, string? messageText)
        {
            throw new NotSupportedException("Gemini provider does not support image editing yet.");
        }

        public async Task<(bool, string)> SetGPTModel(string? modelName, long? userId = null)
        {
            if (string.IsNullOrEmpty(modelName)) return (false, "Пустое название модели");
            _apiConfiguration.ModelName = modelName;
            return (true, string.Empty);
        }
    }
}


