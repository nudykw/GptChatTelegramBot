using Microsoft.Extensions.Logging;
using OpenAI.Audio;
using ServiceLayer.Services.GptChat;

namespace ServiceLayer.Services.AudioTranscriptor
{
    public class AudioTranscriptorService : BaseService
    {
        private readonly ChatGptService _chatGptService;

        public AudioTranscriptorService(IServiceProvider serviceProvider,
            ILogger<AudioTranscriptorService> logger,
            ChatGptService chatGptService)
            : base(serviceProvider, logger)
        {
            _chatGptService = chatGptService;
        }

        public async Task<string> AudioTranscription(long chatId, long telegramUserId, Stream audio, string audioName,
            string model = null, string prompt = null, int? temperature = null,
            string language = null)
        {
            var responce = await _chatGptService.AudioTranscription(chatId, telegramUserId, audio,
                audioName, model, prompt, AudioResponseFormat.Json, temperature, language);
            if (string.IsNullOrWhiteSpace(responce))
            {
                responce = "bla bla bla.";
            }
            _logger.LogInformation($"Transcripted audio: {responce}");
            return responce;
        }
    }
}
