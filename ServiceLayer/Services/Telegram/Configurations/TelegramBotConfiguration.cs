using Telegram.Bot.Types.Enums;

namespace ServiceLayer.Services.Telegram.Configuretions;

public class TelegramBotConfiguration
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public static readonly string Configuration = "TelegramBotConfiguration";

    /// <summary>
    /// Telegram bot API token.
    /// </summary>
    public string BotToken { get; set; } = "";

    /// <summary>
    /// Default message parsing mode (Markdown, HTML, etc.).
    /// </summary>
    public ParseMode DefaultParseMode { get; set; } = ParseMode.Markdown;

    /// <summary>
    /// Bot owner ID for access to administrative commands.
    /// </summary>
    public long? OwnerId { get; set; }
}
