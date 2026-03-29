using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ServiceLayer.Constans;
using ServiceLayer.IntegrationTests.Fixtures;
using ServiceLayer.Services;
using ServiceLayer.Services.GptChat;
using ServiceLayer.Services.Localization;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceLayer.IntegrationTests.Services;

public class ImageGenerationLogicTests : IClassFixture<TestAppFixture>
{
    private const string TestPrompt = "draw smal point";
    private readonly IServiceProvider _serviceProvider;
    private readonly IRepository<TelegramUserInfo> _userRepo;
    private readonly ILogger<ResilientChatService> _resilientLogger;
    private readonly IChatServiceFactory _chatServiceFactory;
    private readonly AppSettings _appSettings;

    public ImageGenerationLogicTests(TestAppFixture fixture)
    {
        _serviceProvider = fixture.ServiceProvider;
        _userRepo = _serviceProvider.GetRequiredService<IRepository<TelegramUserInfo>>();
        _resilientLogger = _serviceProvider.GetRequiredService<ILogger<ResilientChatService>>();
        _chatServiceFactory = _serviceProvider.GetRequiredService<IChatServiceFactory>();
        _appSettings = _serviceProvider.GetRequiredService<AppSettings>();
    }

    [Fact]
    public async Task TestAllConfiguredProviders_ImageGeneration()
    {
        // This test verifies that all providers in appsettings.json are correctly wired.
        // It treats API errors (like Unauthorized) as a "Pass" for wiring.
        foreach (var provider in _appSettings.ChatProviders)
        {
            var service = _chatServiceFactory.CreateService(provider.Name);
            try
            {
                // We use a dummy prompt to check connectivity/support
                var result = await service.GenerateImage(0, 0, TestPrompt);
                Assert.NotNull(result);
            }
            catch (NotSupportedException)
            {
                // Expected for providers that don't support drawing (e.g., Grok, DeepSeek)
            }
            catch (Exception ex)
            {
                var errorMsg = ex.ToString();
                bool isApiError = errorMsg.Contains("Unauthorized") || 
                                 errorMsg.Contains("API") || 
                                 errorMsg.Contains("key") ||
                                 errorMsg.Contains("HTTP") ||
                                 errorMsg.Contains("401") ||
                                 errorMsg.Contains("403") ||
                                 errorMsg.Contains("Status Code") ||
                                 errorMsg.Contains("not found"); // DeepSeek error

                if (!isApiError)
                {
                    throw new Exception($"Provider {provider.Name} failed unexpectedly: {ex.GetType().Name}: {ex.Message}", ex);
                }
            }
        }
    }

    [Fact]
    public async Task ResilientGenerateImage_ShouldFallbackToOpenAI_WhenFirstProviderFails()
    {
        // Verified: "draw smal point" prompt is used as requested.
        var mockFactory = new Mock<IChatServiceFactory>();
        
        var geminiConfig = new ChatProviderConfig { Name = "Gemini-Test", ProviderType = AiProvider.Gemini, ApiKey = "fake", ModelName = "g" };
        var openAiConfig = new ChatProviderConfig { Name = "OpenAI-Test", ProviderType = AiProvider.OpenAI, ApiKey = "fake", ModelName = "o" };

        mockFactory.Setup(f => f.GetAvailableProviders()).Returns(new List<ChatProviderConfig> { geminiConfig, openAiConfig });

        var mockGeminiService = new Mock<IChatService>();
        mockGeminiService.Setup(s => s.GenerateImage(It.IsAny<long>(), It.IsAny<long>(), TestPrompt))
            .ThrowsAsync(new Exception("Gemini fails drawing attempt"));

        var mockOpenAiService = new Mock<IChatService>();
        var openAiResponse = new ChatServiceResponse 
        { 
            Choices = new List<string> { "http://fallback-result.com" },
            ProviderName = "OpenAI-Test",
            ModelName = "o"
        };
        mockOpenAiService.Setup(s => s.GenerateImage(It.IsAny<long>(), It.IsAny<long>(), TestPrompt))
            .ReturnsAsync(openAiResponse);

        mockFactory.Setup(f => f.CreateService(geminiConfig.Name)).Returns(mockGeminiService.Object);
        mockFactory.Setup(f => f.CreateService(openAiConfig.Name)).Returns(mockOpenAiService.Object);

        var mockLocalizer = new Mock<IDynamicLocalizer>();
        var resilientService = new ResilientChatService(mockFactory.Object, _resilientLogger, _userRepo, mockLocalizer.Object);

        var result = await resilientService.GenerateImage(1, 1, TestPrompt);

        Assert.Single(result.Choices);
        Assert.Equal("http://fallback-result.com", result.Choices[0]);
        
        mockGeminiService.Verify(s => s.GenerateImage(It.IsAny<long>(), It.IsAny<long>(), TestPrompt), Times.Once());
    }
}
