using System;

namespace ServiceLayer.Constans;

/// <summary>
/// Области видимости и прав для команд бота (названия соответствуют BotCommandScope в Telegram API).
/// </summary>
[Flags]
public enum BotCommandScope
{
    /// <summary>
    /// По умолчанию (все пользователи).
    /// </summary>
    Default = 1,

    /// <summary>
    /// Все личные чаты.
    /// </summary>
    AllPrivateChats = 2,

    /// <summary>
    /// Все групповые чаты.
    /// </summary>
    AllGroupChats = 4,

    /// <summary>
    /// Все администраторы групп и супергрупп.
    /// </summary>
    AllChatAdmins = 8,

    /// <summary>
    /// Конкретный чат.
    /// </summary>
    Chat = 16,

    /// <summary>
    /// Администраторы конкретного чата.
    /// </summary>
    ChatAdmins = 32,

    /// <summary>
    /// Конкретный участник конкретного чата.
    /// </summary>
    ChatMember = 64,

    /// <summary>
    /// Владелец бота (внутреннее расширение).
    /// </summary>
    Owner = 128,

    /// <summary>
    /// Любой администратор (группы или владелец).
    /// </summary>
    AnyAdmin = AllChatAdmins | Owner
}
