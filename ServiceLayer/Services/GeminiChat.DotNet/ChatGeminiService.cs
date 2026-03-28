using GeminiChat.DotNet;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Models;
using ServiceLayer.Services.GeminiChat.DotNet.Configurations;
using ServiceLayer.Services.GptChat;
using Content = GeminiChat.DotNet.Content;

namespace ServiceLayer.Services.GeminiChat.DotNet
{
    internal class ChatGeminiService : BaseService, IChatService
    {
        private ChatProviderConfig _apiConfiguration;
        private GeminiClient _client;
        const string GeminiModelName = "gemini-pro";

        public ChatGeminiService(IServiceProvider serviceProvider, ILogger<ChatGeminiService> logger,
            ChatProviderConfig chatProviderConfig)
            : base(serviceProvider, logger)
        {
            _apiConfiguration = chatProviderConfig;
            var modelName = string.IsNullOrEmpty(_apiConfiguration.ModelName)
                ? GeminiModelName
                : _apiConfiguration.ModelName;
            _client = new GeminiClient(_apiConfiguration.ApiKey, modelName, _apiConfiguration.BaseUrl);
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

        public Task<IReadOnlyList<Model>> GetAvailibleModels()
        {
            IReadOnlyList<Model> result = new List<Model>() { new Model(GeminiModelName) };
            return Task.FromResult(result);
        }

        public async Task<ChatResponse> SendMessages2ChatAsync(long telegramChatId, long telegramUserId, List<Message> messages)
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
            // Currently basic implementation, returning null as seen in original code or maybe I should return something better?
            // Original code had return null and commented out streaming.
            return null;
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

        public async Task<(bool, string)> SetGPTModel(string? modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return (false, "Пустое название модели");
            _apiConfiguration.ModelName = modelName;
            _client = new GeminiClient(_apiConfiguration.ApiKey, modelName, _apiConfiguration.BaseUrl);
            return (true, string.Empty);
        }
    }
}

