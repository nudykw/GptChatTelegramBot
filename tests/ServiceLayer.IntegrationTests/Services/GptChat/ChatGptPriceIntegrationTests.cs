using ServiceLayer.IntegrationTests.Fixtures;
using ServiceLayer.Services.GptChat;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Threading.Tasks;

namespace ServiceLayer.IntegrationTests.Services.GptChat;

[Trait("Service", "ChatGptService")]
public class ChatGptPriceIntegrationTests : IClassFixture<TestAppFixture>
{
    private readonly ChatGptService _service;

    public ChatGptPriceIntegrationTests(TestAppFixture fixture)
    {
        _service = fixture.ServiceProvider.GetRequiredService<ChatGptService>();
    }

    [Fact]
    public async Task RefreshModelPricesAsync_RealFetch_Succeeds()
    {
        // Arrange
        // Clear the static cache and reset update throttle to ensure a fresh fetch
        ChatGptService._liveModelsCosts.Clear();
        ChatGptService._lastPriceUpdate = DateTime.MinValue;

        // Act
        // The service constructor already starts the refresh, but we'll call it explicitly to wait for completion
        await _service.RefreshModelPricesAsync();

        // Assert
        Assert.NotEmpty(ChatGptService._liveModelsCosts);
        
        // Check for some common models that should be in the LiteLLM JSON
        // Note: Key names in LiteLLM might vary slightly, but OpenAI usually uses standard names.
        Assert.True(ChatGptService._liveModelsCosts.ContainsKey("gpt-4o-mini"), "Should contain gpt-4o-mini");
        
        var miniCost = ChatGptService._liveModelsCosts["gpt-4o-mini"];
        Assert.True(miniCost.Input > 0, "Input cost should be positive");
        Assert.True(miniCost.Output > 0, "Output cost should be positive");
    }
}
