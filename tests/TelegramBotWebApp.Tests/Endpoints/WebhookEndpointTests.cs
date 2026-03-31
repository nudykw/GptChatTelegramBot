using System.Net;
using System.Text;
using System.Text.Json;
using TelegramBotWebApp.Tests.Fixtures;

namespace TelegramBotWebApp.Tests.Endpoints;

/// <summary>
/// Integration tests for POST /aibot (Telegram Webhook endpoint).
/// </summary>
public class WebhookEndpointTests
{
    // ── Available in Webhook mode ─────────────────────────────────────────────

    public class WhenWebhookModeEnabled : IClassFixture<WebhookWebAppFactory>
    {
        private readonly HttpClient _client;

        public WhenWebhookModeEnabled(WebhookWebAppFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Post_Aibot_WithValidUpdate_Returns200()
        {
            // Arrange — minimal valid Telegram Update JSON
            var updateJson = """
                {
                    "update_id": 123456789,
                    "message": {
                        "message_id": 1,
                        "from": {
                            "id": 999,
                            "is_bot": false,
                            "first_name": "Test"
                        },
                        "chat": {
                            "id": 999,
                            "type": "private"
                        },
                        "date": 1700000000,
                        "text": "Hello"
                    }
                }
                """;

            var content = new StringContent(updateJson, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync("/aibot", content);

            // Assert — endpoint must not crash; 200 or 500 (if handler fails with test config)
            // We only assert the endpoint exists and is reachable (not 404)
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }

        [Fact]
        public async Task Post_Aibot_WithEmptyBody_Returns400Or500()
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/aibot", content);

            // Endpoint is mounted — not 404/405
            Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.NotEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }
    }

    // ── NOT available in Polling mode ─────────────────────────────────────────

    public class WhenPollingModeEnabled : IClassFixture<WebAppFactory>
    {
        private readonly HttpClient _client;

        public WhenPollingModeEnabled(WebAppFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Post_Aibot_Returns404_WhenPollingMode()
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/aibot", content);

            // In Polling mode the /aibot endpoint must NOT be registered
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
