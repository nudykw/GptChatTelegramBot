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
    /// Database configuration.
    /// </summary>
    public DatabaseSettings Database { get; set; } = new();
}

public class DatabaseSettings
{
    public DataBaseLayer.Enums.DatabaseProvider Provider { get; set; } = DataBaseLayer.Enums.DatabaseProvider.Sqlite;
    public string ConnectionString { get; set; } = string.Empty;
}
