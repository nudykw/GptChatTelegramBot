using GeminiChat.DotNet;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using ServiceLayer.Services.GeminiChat.DotNet.Configurations;
using ServiceLayer.Services.GptChat;
using Content = GeminiChat.DotNet.Content;

namespace ServiceLayer.Services.GeminiChat.DotNet
{
    internal class ChatGeminiService : BaseService, IChatService
    {
        private GeminiChatConfiguration _apiConfiguration;
        private GeminiClient _client;
        const string GeminiModelName = "gemini-pro";

        public ChatGeminiService(IServiceProvider serviceProvider, ILogger<ChatGptService> logger,
    AppSettings appSettings)
    : base(serviceProvider, logger)
        {
            _apiConfiguration = appSettings.GeminiChatConfiguration;
            _client = new GeminiClient(_apiConfiguration.APIKey, GeminiModelName, null);
            //_gptChatConfiguration = appSettings.GptChatConfiguration;
            //_gptBilingItemRepository = _serviceProvider.GetService<IRepository<GptBilingItem>>();
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
            return (Task<IReadOnlyList<Model>>)result;
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
            //var result = await _client.PostAsync(conversation);
            await foreach (string data in _client.StreamingRequest(conversation))
            {
                //result.Append(data);
                //resultHandler(data);
            }
            //return result ?? string.Empty;
            return null;
        }
    }
}
