using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Models;

namespace ServiceLayer.Services
{
    public interface IChatService
    {
        Task<string> Ask(long chatId, long userId, string message);
        Task<IReadOnlyList<Model>> GetAvailibleModels();
        Task<ChatResponse> SendMessages2ChatAsync(long telegramChatId, long telegramUserId, List<Message> messages);
        Task<IReadOnlyList<string>> GenerateImage(long chatId, long telegramUserId, string prompt);
        Task<string> AudioTranscription(long chatId, long telegramUserId, Stream audio, string audioName, 
            string model = null, string prompt = null, AudioResponseFormat responseFormat = AudioResponseFormat.Json, 
            int? temperature = null, string language = null);
        Task<IReadOnlyList<string>> CreateImageEditAsync(long chatId, long telegramUserId, string filePath, string? messageText);
        Task<(bool, string)> SetGPTModel(string? modelName);
    }
}