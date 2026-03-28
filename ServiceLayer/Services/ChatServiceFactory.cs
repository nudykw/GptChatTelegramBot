using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceLayer.Services.GptChat;
using ServiceLayer.Services.GeminiChat.DotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ServiceLayer.Services;

public class ChatServiceFactory : IChatServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppSettings _appSettings;
    private readonly Dictionary<string, ChatProviderConfig> _providers;

    public ChatServiceFactory(IServiceProvider serviceProvider, AppSettings appSettings)
    {
        _serviceProvider = serviceProvider;
        _appSettings = appSettings;
        _providers = new Dictionary<string, ChatProviderConfig>();

        ValidateAndLoadProviders();
    }

    private void ValidateAndLoadProviders()
    {
        var uniqueProviders = new HashSet<(ChatProviderType Type, string ApiKey, string? BaseUrl)>();

        foreach (var config in _appSettings.ChatProviders)
        {
            if (ChatProviderConfig.IsPlaceholder(config.ApiKey))
            {
                continue;
            }

            var identity = (config.ProviderType, config.ApiKey, config.BaseUrl);
            if (!uniqueProviders.Add(identity))
            {
                throw new Exception($"Duplicate provider configuration found: {config.ProviderType} with key {config.ApiKey[..Math.Min(10, config.ApiKey.Length)]}... and base path {config.BaseUrl ?? "default"}");
            }

            if (_providers.ContainsKey(config.Name))
            {
                throw new Exception($"Provider with name '{config.Name}' already exists.");
            }

            _providers.Add(config.Name, config);
        }
    }

    public IEnumerable<ChatProviderConfig> GetAvailableProviders()
    {
        return _providers.Values;
    }

    public IChatService CreateService(string providerName)
    {
        if (!_providers.TryGetValue(providerName, out var config))
        {
            throw new Exception($"Chat provider '{providerName}' not found.");
        }

        return config.ProviderType switch
        {
            ChatProviderType.OpenAI => ActivatorUtilities.CreateInstance<ChatGptService>(_serviceProvider, config),
            ChatProviderType.Gemini => ActivatorUtilities.CreateInstance<ChatGeminiService>(_serviceProvider, config),
            _ => throw new Exception($"Unsupported provider type: {config.ProviderType}")
        };
    }
}
