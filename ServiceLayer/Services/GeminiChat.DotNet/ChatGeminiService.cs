using GeminiChat.DotNet;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Models;
using ServiceLayer.Services.GeminiChat.DotNet.Configurations;
using ServiceLayer.Services.GptChat;
using Content = GeminiChat.DotNet.Content;
using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceLayer.Services.GeminiChat.DotNet
{
    internal class ChatGeminiService : BaseService, IChatService
    {
        private ChatProviderConfig _apiConfiguration;
        private GeminiClient _client;
        private readonly IRepository<GptBilingItem>? _gptBilingItemRepository;

        public ChatGeminiService(IServiceProvider serviceProvider, ILogger<ChatGeminiService> logger,
            ChatProviderConfig chatProviderConfig)
            : base(serviceProvider, logger)
        {
            _apiConfiguration = chatProviderConfig;
            var modelName = string.IsNullOrEmpty(_apiConfiguration.ModelName)
                ? AiModel.GeminiPro
                : _apiConfiguration.ModelName;
            _client = new GeminiClient(_apiConfiguration.ApiKey, modelName, _apiConfiguration.BaseUrl);
            _gptBilingItemRepository = _serviceProvider.GetService<IRepository<GptBilingItem>>();
        }

        public async Task<string> Ask(long chatId, long userId, string message)
        {
            var contents = new List<Content>() { new Content { Role = MessageRole.User, Parts = new List<Part>() { new() { Text = message } } } };
            var conversation = new GeminiRequest()
            {
                Contents = contents
            };
            var result = await _client.PostAsync(conversation);
            return result ?? string.Empty;
        }

        public Task<IReadOnlyList<Model>> GetAvailibleModels(long? userId = null)
        {
            IReadOnlyList<Model> result = new List<Model>() { new Model(AiModel.GeminiPro) };
            return Task.FromResult(result);
        }

        public async Task<ChatServiceResponse> SendMessages2ChatAsync(long telegramChatId, long telegramUserId, List<Message> messages)
        {
            var contents = messages.Select(p => new Content
            {
                Role = p.Role == Role.User ? MessageRole.User : MessageRole.Model,
                Parts = new List<Part>() { new() { Text = p.Content } }
            }).ToList();
            var conversation = new GeminiRequest()
            {
                Contents = contents
            };
            var result = await _client.PostAsync(conversation);
            var modelName = string.IsNullOrEmpty(_apiConfiguration.ModelName) ? AiModel.GeminiPro : _apiConfiguration.ModelName;
            
            await SaveBilling(modelName, _apiConfiguration.Name, telegramChatId, telegramUserId);

            return new ChatServiceResponse
            {
                Choices = new List<string> { result },
                ProviderName = _apiConfiguration.Name,
                ModelName = modelName
            };
        }

        private async Task SaveBilling(string modelName, string providerName, long telegramChatId, long telegramUserId)
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
                // Gemini currently doesn't expose tokens in this basic client
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                Cost = 0
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
            _client = new GeminiClient(_apiConfiguration.ApiKey, modelName, _apiConfiguration.BaseUrl);
            return (true, string.Empty);
        }
    }
}

