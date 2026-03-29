using Telegram.Bot.Types.Enums;

namespace ServiceLayer.Services.Telegram.Configuretions;

public class TelegramBotConfiguration
{
    /// <summary>
    /// Имя секции конфигурации.
    /// </summary>
    public static readonly string Configuration = "TelegramBotConfiguration";

    /// <summary>
    /// API токен Telegram-бота.
    /// </summary>
    public string BotToken { get; set; } = "";

    /// <summary>
    /// Режим парсинга сообщений по умолчанию (Markdown, Html и т.д.).
    /// </summary>
    public ParseMode DefaultParseMode { get; set; } = ParseMode.Markdown;

    /// <summary>
    /// ID владельца бота для доступа к административным командам.
    /// </summary>
    public long? OwnerId { get; set; }
}
