using ServiceLayer.Utils;

namespace ServiceLayer.Constans;

/// <summary>
/// Статические команды бота.
/// </summary>
public sealed class BotCommand : StaticStringEnumBase<BotCommand>, IStaticStringEnum<BotCommand>
{
    private BotCommand(string value, string description, BotCommandScope requiredScope = BotCommandScope.Default | BotCommandScope.AllPrivateChats | BotCommandScope.AllGroupChats) : base(value)
    {
        Description = description;
        RequiredScope = requiredScope;
    }

    /// <summary>
    /// Описание команды для меню Telegram.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Требуемая область видимости/права для доступа к команде.
    /// </summary>
    public BotCommandScope RequiredScope { get; }

    public static readonly BotCommand Billing = new("/billing", "Show usage statistics", BotCommandScope.AnyAdmin | BotCommandScope.Owner);
    public static readonly BotCommand Model = new("/model", "Select GPT model");
    public static readonly BotCommand Provider = new("/provider", "Select AI provider");
    public static readonly BotCommand Draw = new("/draw", "Generate image by description. You can also just send a text prompt asking to draw something, and the image will be generated.");
    public static readonly BotCommand Lang = new("/lang", "Change language (e.g. /lang German)");
    public static readonly BotCommand Help = new("/help", "Show help info");
    public static readonly BotCommand Restart = new("/restart", "Restart the bot", BotCommandScope.Owner | BotCommandScope.AnyAdmin);

    /// <summary>
    /// Команды обычно не регистрозависимы.
    /// </summary>
    public static bool DefaultIgnoreCase => true;

    /// <summary>
    /// Неявное преобразование из строки (для удобства парсинга сообщений).
    /// </summary>
    public static implicit operator BotCommand?(string? value) => FromString(value);
}
