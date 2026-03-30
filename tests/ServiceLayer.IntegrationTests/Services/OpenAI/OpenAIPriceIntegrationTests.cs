using ServiceLayer.IntegrationTests.Fixtures;
using ServiceLayer.Services.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Threading.Tasks;

namespace ServiceLayer.IntegrationTests.Services.OpenAI;

[Trait("Service", "OpenAIService")]
public class OpenAIPriceIntegrationTests : IClassFixture<TestAppFixture>
{
    private readonly OpenAIService _service;

    public OpenAIPriceIntegrationTests(TestAppFixture fixture)
    {
        _service = fixture.ServiceProvider.GetRequiredService<OpenAIService>();
    }

    [Fact]
    public async Task RefreshModelPricesAsync_RealFetch_Succeeds()
    {
        // Arrange
        // Clear the static cache and reset update throttle to ensure a fresh fetch
        OpenAIService._liveModelsCosts.Clear();
        OpenAIService._lastPriceUpdate = DateTime.MinValue;

        // Act
        // The service constructor already starts the refresh, but we'll call it explicitly to wait for completion
        await _service.RefreshModelPricesAsync();

        // Assert
        Assert.NotEmpty(OpenAIService._liveModelsCosts);
        
        // Check for some common models that should be in the LiteLLM JSON
        // Note: Key names in LiteLLM might vary slightly, but OpenAI usually uses standard names.
        Assert.True(OpenAIService._liveModelsCosts.ContainsKey("gpt-4o-mini"), "Should contain gpt-4o-mini");
        
        var miniCost = OpenAIService._liveModelsCosts["gpt-4o-mini"];
        Assert.True(miniCost.Input > 0, "Input cost should be positive");
        Assert.True(miniCost.Output > 0, "Output cost should be positive");
    }
}
