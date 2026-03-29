using ServiceLayer.Services.Telegram.Configuretions;

namespace ServiceLayer.Services;

public class AppSettings
{
    /// <summary>
    /// Имя основной секции в appsettings.json.
    /// </summary>
    public static readonly string Configuration = "AppSettings";

    /// <summary>
    /// Конфигурация Telegram-бота.
    /// </summary>
    public TelegramBotConfiguration TelegramBotConfiguration { get; set; } = null!;

    /// <summary>
    /// Список конфигураций для AI провайдеров (OpenAI, Gemini и др.).
    /// </summary>
    public List<ChatProviderConfig> ChatProviders { get; set; } = new();
}
