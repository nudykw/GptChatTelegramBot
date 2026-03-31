using Telegram.Bot.Types.Enums;
using ServiceLayer.Models;

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

    /// <summary>
    /// Initial balance for new users and top-up amount.
    /// </summary>
    public decimal InitialBalance { get; set; } = 0.1M;

    /// <summary>
    /// List of user IDs whose balance should not be deducted.
    /// </summary>
    public List<long> IgnoredBalanceUserIds { get; set; } = new();
    
    /// <summary>
    /// Specialized AI task configurations.
    /// </summary>
    public AiSettings AiSettings { get; set; } = new AiSettings();

    /// <summary>
    /// AI model cache expiry in hours.
    /// </summary>
    public int ModelCacheExpiryHours { get; set; } = 48;
}

public class AiSettings
{
    /// <summary>
    /// Vision configuration containing model and provider names.
    /// </summary>
    public FullModelName? Vision { get; set; } = new FullModelName { ModelName = "gpt-4o", ProviderName = "OpenAI" };

    /// <summary>
    /// Drawing configuration containing model and provider names.
    /// </summary>
    public FullModelName? Drawing { get; set; } = new FullModelName { ModelName = "dall-e-3", ProviderName = "OpenAI" };

    /// <summary>
    /// Classification configuration for intent analysis and internal tasks.
    /// </summary>
    public FullModelName? Classification { get; set; } = new FullModelName { ModelName = "gpt-4o", ProviderName = "OpenAI" };

    /// <summary>
    /// List of configurations for AI providers (OpenAI, Gemini, etc.).
    /// </summary>
    public List<ChatProviderConfig> ChatProviders { get; set; } = new();
}
