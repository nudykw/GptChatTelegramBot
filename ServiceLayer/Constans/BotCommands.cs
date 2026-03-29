using ServiceLayer.Utils;

namespace ServiceLayer.Constans;

/// <summary>
/// Статические команды бота.
/// </summary>
public sealed class BotCommand : StaticStringEnumBase<BotCommand>, IStaticStringEnum<BotCommand>
{
    private BotCommand(string value, string description) : base(value)
    {
        Description = description;
    }

    /// <summary>
    /// Описание команды для меню Telegram.
    /// </summary>
    public string Description { get; }

    public static readonly BotCommand Billing = new("/billing", "Показать статистику использования");
    public static readonly BotCommand Model = new("/model", "Выбрать модель GPT");
    public static readonly BotCommand Provider = new("/provider", "Выбрать провайдера ИИ");
    public static readonly BotCommand Draw = new("/draw", "Сгенерировать изображение по описанию");
    public static readonly BotCommand Restart = new("/restart", "Перезагрузить бота");

    /// <summary>
    /// Команды обычно не регистрозависимы.
    /// </summary>
    public static bool DefaultIgnoreCase => true;

    /// <summary>
    /// Неявное преобразование из строки (для удобства парсинга сообщений).
    /// </summary>
    public static implicit operator BotCommand?(string? value) => FromString(value);
}
