using Moq;
using Moq.Protected;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using ServiceLayer.Services.GptChat;
using ServiceLayer.Services.GptChat.Configurations;
using ServiceLayer.Services.GeminiChat.DotNet.Configurations;
using ServiceLayer.Services.Telegram.Configuretions;
using ServiceLayer.Services;
using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using System.Text.Json;
using System.Net;
using System.Text;
using System.Reflection;

namespace ServiceLayer.UnitTests.Services.GptChat;

public class ChatGptServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ILogger<ChatGptService>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IRepository<GptBilingItem>> _mockBillingRepository;
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly AppSettings _appSettings;

    public ChatGptServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<ILogger<ChatGptService>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockBillingRepository = new Mock<IRepository<GptBilingItem>>();
        _mockHandler = new Mock<HttpMessageHandler>();

        _appSettings = new AppSettings
        {
            GptChatConfiguration = new GptChatConfiguration
            {
                APIKey = "sk-test-key",
                ModelName = "gpt-4o-mini"
            },
            GeminiChatConfiguration = new GeminiChatConfiguration { APIKey = "test-gemini-key" },
            TelegramBotConfiguration = new TelegramBotConfiguration { BotToken = "test-bot-token" }
        };

        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IRepository<GptBilingItem>)))
            .Returns(_mockBillingRepository.Object);

        var httpClient = new HttpClient(_mockHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    private ChatGptService CreateService(OpenAIClient? api = null)
    {
        var config = new ChatProviderConfig
        {
            Name = "OpenAI",
            ProviderType = ChatProviderType.OpenAI,
            ApiKey = _appSettings.GptChatConfiguration.APIKey,
            ModelName = _appSettings.GptChatConfiguration.ModelName
        };

        return new ChatGptService(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            config,
            _mockHttpClientFactory.Object,
            api);
    }

    [Fact]
    public async Task Ask_ShouldReturnCombinedText()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new 
        {
            id = "chatcmpl-123",
            @object = "chat.completion",
            created = 1677652288,
            model = "gpt-4o-mini",
            choices = new[]
            {
                new { message = new { role = "assistant", content = "Hello world!" }, finish_reason = "stop", index = 0 }
            },
            usage = new { prompt_tokens = 9, completion_tokens = 12, total_tokens = 21 }
        });

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        // Use OpenAISettings as recommended with property initialization
        var api = new OpenAIClient(new OpenAIAuthentication("sk-test-key"), client: new HttpClient(_mockHandler.Object));
        
        var service = CreateService(api);

        // Act
        var result = await service.Ask(1, 1, "Hi");

        // Assert
        Assert.Contains("Hello world!", result);
        _mockBillingRepository.Verify(r => r.Add(It.IsAny<GptBilingItem>()), Times.Once);
        _mockBillingRepository.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task SendMessages2ChatAsync_ShouldPassCorrectParametersAndSaveBilling()
    {
        // Arrange
        var jsonResponse = JsonSerializer.Serialize(new 
        {
            id = "chatcmpl-123",
            @object = "chat.completion",
            created = 1677652288,
            model = "gpt-4o-mini",
            choices = new[]
            {
                new { message = new { role = "assistant", content = "Response" }, finish_reason = "stop", index = 0 }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 20, total_tokens = 30 }
        });

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        var api = new OpenAIClient(new OpenAIAuthentication("sk-test-key"), client: new HttpClient(_mockHandler.Object));
        
        var service = CreateService(api);

        var messages = new List<Message> { new Message(Role.User, "Test message") };

        // Act
        var response = await service.SendMessages2ChatAsync(123, 456, messages);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("gpt-4o-mini", response.Model);
        Assert.Equal(30, response.Usage.TotalTokens);

        _mockBillingRepository.Verify(r => r.Add(It.Is<GptBilingItem>(item => 
            item.TelegramChatInfoId == 123 && 
            item.TelegramUserInfoId == 456 && 
            item.PromptTokens == 10 && 
            item.CompletionTokens == 20 && 
            item.TotalTokens == 30)), Times.Once);
    }

    [Fact]
    public async Task GetAvailibleModels_ShouldReturnFilteredModels()
    {
        // Arrange
        var modelsJson = JsonSerializer.Serialize(new 
        {
            data = new[]
            {
                new { id = "gpt-4", @object = "model", created = 1677652288, owned_by = "openai" },
                new { id = "unknown-model", @object = "model", created = 1677652288, owned_by = "openai" },
                new { id = "gpt-4o-mini", @object = "model", created = 1677652288, owned_by = "openai" }
            }
        });

        // The method also performs a test completion for each model
        var chatJson = JsonSerializer.Serialize(new 
        {
            id = "chatcmpl-test",
            @object = "chat.completion",
            created = 1677652288,
            model = "gpt-4",
            choices = new[] { new { message = new { role = "assistant", content = "Hi" }, finish_reason = "stop", index = 0 } }
        });

        _mockHandler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(modelsJson, Encoding.UTF8, "application/json")
            })
            // Response for gpt-4 test
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(chatJson, Encoding.UTF8, "application/json")
            })
             // Response for gpt-4o-mini test (unknown-model is skipped by pre-filter)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(chatJson, Encoding.UTF8, "application/json")
            });

        var api = new OpenAIClient(new OpenAIAuthentication("sk-test-key"), client: new HttpClient(_mockHandler.Object));
        
        var service = CreateService(api);

        // Act
        var result = await service.GetAvailibleModels();

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result, m => m.Id == "gpt-4");
        Assert.Contains(result, m => m.Id == "gpt-4o-mini");
        Assert.DoesNotContain(result, m => m.Id == "unknown-model");
    }
}
