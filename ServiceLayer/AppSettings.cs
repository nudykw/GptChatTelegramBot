using ServiceLayer.Services.Telegram.Configuretions;

namespace ServiceLayer.Services;

public class AppSettings
{
    public static readonly string Configuration = "AppSettings";
    public required TelegramBotConfiguration TelegramBotConfiguration { get; set; }

    public List<ChatProviderConfig> ChatProviders { get; set; } = new();
}
