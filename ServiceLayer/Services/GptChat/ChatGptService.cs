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

namespace ServiceLayer.Services.GptChat;

public class ChatGptService : BaseService, IChatService
{
    private class GptModelCache
    {
        internal DateTime? LastUpdates { get; set; }
        internal IReadOnlyList<Model> Models { get; set; }
    }
    private const int defTokens = 1000;
    private static Dictionary<string, GptModelCost> gptModelsCosts = new Dictionary<string, GptModelCost>()
    {
        {"gpt-4-1106-preview",  new GptModelCost(defTokens, 0.01M, 0.03M)},
        {"gpt-4-1106-vision-preview",  new GptModelCost(defTokens, 0.01M, 0.03M)},
        {"gpt-4",  new GptModelCost(defTokens, 0.03M, 0.06M)},
        {"gpt-4-32k",  new GptModelCost(defTokens, 0.06M, 0.12M)},
        {"gpt-3.5-turbo-1106",  new GptModelCost(defTokens, 0.001M, 0.002M)},
        {"gpt-3.5-turbo-instruct",  new GptModelCost(defTokens, 0.0015M, 0.002M)},

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
    private static GptModelCache gptModelCache = null;

    public ChatGptService(IServiceProvider serviceProvider, ILogger<ChatGptService> logger,
        AppSettings appSettings)
        : base(serviceProvider, logger)
    {
        _api = new OpenAIClient(appSettings.GptChatConfiguration.APIKey);
        _gptChatConfiguration = appSettings.GptChatConfiguration;
        _gptBilingItemRepository = _serviceProvider.GetService<IRepository<GptBilingItem>>();
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
    public async Task<string> Asc(long chatId, long userId, string message)
    {
        var chatResponce = await SendMessages2ChatAsync(chatId, userId, new List<Message>()
        {
            new Message(Role.User, message)
        });
        _logger.LogInformation("Usage for asc '{0}: {1}", message,
            chatResponce.GetUsage());
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
        gptModelsCosts.TryGetValue(modelName, out GptModelCost? modelCost);
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
        var request = new AudioTranscriptionRequest(audio, audioName, model, prompt, responseFormat,
            temperature, language);
        string result = await _api.AudioEndpoint.CreateTranscriptionTextAsync(request);
        await SaveBilling(request.Model, chatId, telegramUserId, new GptUsage(0, 1, 1));
        return result;
    }

    internal async Task<IReadOnlyList<string>> CreateImageEditAsync(long chatId, long telegramUserId,
        string filePath, string? messageText, ImageSize imageSize)
    {
        //ImageEditRequest request = new ImageEditRequest(filePath, messageText, size: imageSize);
        var imageEditRequest = new ImageEditRequest(filePath, filePath, messageText, size: imageSize);
        var imagesResults =
            //await _api.ImagesEndPoint.CreateImageEditAsync(filePath, filePath, messageText, size: imageSize);
            await _api.ImagesEndPoint.CreateImageEditAsync(imageEditRequest);
        //await SaveBilling(request.Model, chatId, telegramUserId, new Usage(0, 1, 1));
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
}
