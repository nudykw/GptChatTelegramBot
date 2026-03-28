using Telegram.Bot.Types.Enums;

namespace ServiceLayer.Services.Telegram.Configuretions;

public class TelegramBotConfiguration
{
    public static readonly string Configuration = "TelegramBotConfiguration";
    public string BotToken { get; set; } = "";
    public ParseMode DefaultParseMode { get; set; } = ParseMode.Markdown;

}
