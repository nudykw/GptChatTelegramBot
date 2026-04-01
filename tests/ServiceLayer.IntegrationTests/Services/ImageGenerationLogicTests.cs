using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceLayer.IntegrationTests.Fixtures;
using ServiceLayer.Services;
using ServiceLayer.Services.Localization;
using Xunit;

namespace ServiceLayer.IntegrationTests.Services;

public class ImageGenerationLogicTests : IClassFixture<TestAppFixture>
{
    private const string TestPrompt = "draw small point";
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

    /// <summary>
    /// Verifies that each configured provider either:
    ///   - successfully generates an image (returns a URL), or
    ///   - explicitly declares it does not support image generation (NotSupportedException).
    /// Any other exception (auth errors, network errors, etc.) fails the test.
    /// </summary>
    [Fact]
    public async Task TestAllConfiguredProviders_ImageGeneration()
    {
        var providers = _appSettings.TelegramBotConfiguration.AiSettings.ChatProviders;

        foreach (var provider in providers)
        {
            var service = _chatServiceFactory.CreateService(provider.Name);
            try
            {
                var result = await service.GenerateImage(0, 0, TestPrompt);
                Assert.NotNull(result);
                Assert.NotEmpty(result.Choices);
                Assert.StartsWith("http", result.Choices[0]);
            }
            catch (NotSupportedException)
            {
                // Expected for providers that don't support image generation (Gemini, Grok, DeepSeek, etc.)
            }
        }
    }

    /// <summary>
    /// Real integration test of the fallback logic in ResilientChatService.
    /// Uses real providers: the service iterates through all configured providers,
    /// skips those that throw NotSupportedException, and succeeds when it reaches
    /// one that supports image generation (typically OpenAI/dall-e).
    /// </summary>
    [Fact]
    public async Task ResilientGenerateImage_ShouldSucceedViaProviderFallback()
    {
        // Arrange — all real dependencies, no mocks
        var localizer = _serviceProvider.GetRequiredService<IDynamicLocalizer>();
        var resilientService = new ResilientChatService(_chatServiceFactory, _resilientLogger, _userRepo, localizer);

        // Act — ResilientChatService will try providers in order until one supports image generation
        var result = await resilientService.GenerateImage(1, 1, TestPrompt);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Choices);
        Assert.StartsWith("http", result.Choices[0]);
    }
}
