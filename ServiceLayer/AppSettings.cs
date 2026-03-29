using ServiceLayer.Services.Telegram.Configuretions;

namespace ServiceLayer.Services;

public class AppSettings
{
    public static readonly string Configuration = "AppSettings";
    public TelegramBotConfiguration TelegramBotConfiguration { get; set; } = null!;

    public List<ChatProviderConfig> ChatProviders { get; set; } = new();
}
