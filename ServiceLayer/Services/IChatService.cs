using OpenAI.Chat;
using OpenAI.Models;

namespace ServiceLayer.Services
{
    public interface IChatService
    {
        Task<string> Asc(long chatId, long userId, string message);
        Task<IReadOnlyList<Model>> GetAvailibleModels();
        Task<ChatResponse> SendMessages2ChatAsync(long telegramChatId, long telegramUserId, List<Message> messages);
    }
}