using Microsoft.Extensions.Configuration;
using ServiceLayer.Services;
using System.Collections.Generic;
using Xunit;

namespace ServiceLayer.BindingTests;

public class AppSettingsBindingTests
{
    [Fact]
    public void TestAppSettingsBinding()
    {
        var myConfiguration = new Dictionary<string, string?> {
            {"AppSettings:TelegramBotConfiguration:BotToken", "abc"},
            {"AppSettings:ChatProviders:0:Name", "Test"},
            {"AppSettings:ChatProviders:0:ProviderType", "OpenAI"},
            {"AppSettings:ChatProviders:0:ApiKey", "key"}
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();

        var appSection = configuration.GetSection("AppSettings");
        var appSettings = appSection.Get<AppSettings>();

        Assert.NotNull(appSettings);
        Assert.Equal("abc", appSettings.TelegramBotConfiguration.BotToken);
        Assert.Single(appSettings.ChatProviders);
        Assert.Equal("OpenAI", appSettings.ChatProviders[0].ProviderType.Value);
    }
}
