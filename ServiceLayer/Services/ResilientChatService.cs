using Microsoft.Extensions.Logging;
using OpenAI.Audio;
using OpenAI.Chat;
using OpenAI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataBaseLayer.Models;
using DataBaseLayer.Repositories;

namespace ServiceLayer.Services;

public class ResilientChatService : IChatService
{
    private readonly IChatServiceFactory _chatServiceFactory;
    private readonly ILogger<ResilientChatService> _logger;
    private readonly IRepository<TelegramUserInfo> _telegramUserInfoRepository;

    public ResilientChatService(IChatServiceFactory chatServiceFactory, ILogger<ResilientChatService> logger, IRepository<TelegramUserInfo> telegramUserInfoRepository)
    {
        _chatServiceFactory = chatServiceFactory;
        _logger = logger;
        _telegramUserInfoRepository = telegramUserInfoRepository;
    }

    private async Task<T> ExecuteWithFallback<T>(Func<IChatService, Task<T>> action, string methodName, long? userId = null)
    {
        var providers = _chatServiceFactory.GetAvailableProviders().ToList();
        
        if (userId.HasValue)
        {
            var user = await _telegramUserInfoRepository.Get(p => p.Id == userId.Value);
            if (user != null && user.PreferredProvider != ChatStrategy.Auto)
            {
                var providerType = user.PreferredProvider switch
                {
                    ChatStrategy.OpenAI => ChatProviderType.OpenAI,
                    ChatStrategy.Gemini => ChatProviderType.Gemini,
                    _ => (ChatProviderType?)null
                };

                if (providerType.HasValue)
                {
                    var preferredProvider = providers.FirstOrDefault(p => p.ProviderType == providerType.Value);
                    if (preferredProvider != null)
                        {
                        _logger.LogInformation("Using preferred provider type {ProviderType} for {Method} (Strict Mode)", providerType.Value, methodName);
                        var service = _chatServiceFactory.CreateService(preferredProvider.Name);
                        return await action(service);
                    }
                    else
                    {
                        _logger.LogWarning("Preferred provider type {ProviderType} not found in configuration. Falling back to rotation.", providerType.Value);
                    }
                }
            }
        }

        var exceptions = new List<Exception>();

        if (!providers.Any())
        {
            throw new Exception("No chat providers configured.");
        }

        foreach (var provider in providers)
        {
            try
            {
                _logger.LogInformation("Attempting {Method} with provider: {Provider}", methodName, provider.Name);
                var service = _chatServiceFactory.CreateService(provider.Name);
                return await action(service);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogWarning("Provider {Provider} does not support {Method}: {Message}", provider.Name, methodName, ex.Message);
                exceptions.Add(ex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} failed for {Method}", provider.Name, methodName);
                exceptions.Add(ex);
            }
        }

        throw new AggregateException($"All providers failed for {methodName}", exceptions);
    }

    public Task<string> Ask(long chatId, long userId, string message)
        => ExecuteWithFallback(s => s.Ask(chatId, userId, message), nameof(Ask), userId);

    public Task<IReadOnlyList<Model>> GetAvailibleModels(long? userId = null)
        => ExecuteWithFallback(s => s.GetAvailibleModels(userId), nameof(GetAvailibleModels), userId);

    public Task<ChatServiceResponse> SendMessages2ChatAsync(long telegramChatId, long telegramUserId, List<Message> messages)
        => ExecuteWithFallback(s => s.SendMessages2ChatAsync(telegramChatId, telegramUserId, messages), nameof(SendMessages2ChatAsync), telegramUserId);

    public Task<IReadOnlyList<string>> GenerateImage(long chatId, long telegramUserId, string prompt)
        => ExecuteWithFallback(s => s.GenerateImage(chatId, telegramUserId, prompt), nameof(GenerateImage), telegramUserId);

    public Task<string> AudioTranscription(long chatId, long telegramUserId, Stream audio, string audioName, string model = null, string prompt = null, AudioResponseFormat responseFormat = AudioResponseFormat.Json, int? temperature = null, string language = null)
        => ExecuteWithFallback(s => s.AudioTranscription(chatId, telegramUserId, audio, audioName, model, prompt, responseFormat, temperature, language), nameof(AudioTranscription), telegramUserId);

    public Task<IReadOnlyList<string>> CreateImageEditAsync(long chatId, long telegramUserId, string filePath, string? messageText)
        => ExecuteWithFallback(s => s.CreateImageEditAsync(chatId, telegramUserId, filePath, messageText), nameof(CreateImageEditAsync), telegramUserId);

    public async Task<(bool, string)> SetGPTModel(string? modelName, long? userId = null)
    {
        // Try to set model for the first provider that accepts it (or the preferred one)
        return await ExecuteWithFallback(s => s.SetGPTModel(modelName, userId), nameof(SetGPTModel), userId);
    }
}
