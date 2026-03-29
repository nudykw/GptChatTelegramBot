using ServiceLayer.Services.Telegram.Configuretions;
using ServiceLayer.Services.Telegram.Configuretions;

namespace ServiceLayer.Services;

public class AppSettings
{
    /// <summary>
    /// Main section name in appsettings.json.
    /// </summary>
    public static readonly string Configuration = "AppSettings";

    /// <summary>
    /// Telegram bot configuration.
    /// </summary>
    public TelegramBotConfiguration TelegramBotConfiguration { get; set; } = null!;

    /// <summary>
    /// List of configurations for AI providers (OpenAI, Gemini, etc.).
    /// </summary>
    public List<ChatProviderConfig> ChatProviders { get; set; } = new();
}
