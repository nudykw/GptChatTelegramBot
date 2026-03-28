using Moq;
using Moq.Protected;
using Microsoft.Extensions.Logging;
using ServiceLayer.Services.GptChat;
using ServiceLayer.Services.GptChat.Configurations;
using ServiceLayer.Services;
using System.Net;
using System.Text.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ServiceLayer.Services.Telegram.Configuretions;
using ServiceLayer.Services.GeminiChat.DotNet.Configurations;

namespace ServiceLayer.UnitTests;

public class ChatGptPriceUpdateTests
{
    [Fact]
    public async Task RefreshModelPricesAsync_WithValidJson_UpdatesLiveModelsCosts()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<ChatGptService>>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockHandler = new Mock<HttpMessageHandler>();

        var json = "{\"gpt-4o-mini\": {\"input_cost_per_token\": 0.00000015, \"output_cost_per_token\": 0.0000006, \"litellm_provider\": \"openai\"}}";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync", 
                ItExpr.IsAny<HttpRequestMessage>(), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var appSettings = new AppSettings
        {
            GptChatConfiguration = new GptChatConfiguration { APIKey = "sk-test-key-1234567890", ModelName = "gpt-4o-mini" },
            TelegramBotConfiguration = new TelegramBotConfiguration { BotToken = "test-bot" },
            GeminiChatConfiguration = new GeminiChatConfiguration { APIKey = "test-gemini" }
        };

        // Clear the static cache before testing
        ChatGptService._liveModelsCosts.Clear();
        typeof(ChatGptService)
            .GetField("_lastPriceUpdate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(null, DateTime.MinValue);

        var service = new ChatGptService(mockServiceProvider.Object, mockLogger.Object, appSettings, mockHttpClientFactory.Object);

        // Act
        // We call it once manually (though the constructor already calls it)
        await service.RefreshModelPricesAsync();

        // Assert
        Assert.True(ChatGptService._liveModelsCosts.ContainsKey("gpt-4o-mini"));
        var cost = ChatGptService._liveModelsCosts["gpt-4o-mini"];
        Assert.Equal(0.00015M, cost.Input);
        Assert.Equal(0.0006M, cost.Output);
    }
}
