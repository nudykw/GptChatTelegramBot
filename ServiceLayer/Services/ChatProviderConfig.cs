using ServiceLayer.Constans;

namespace ServiceLayer.Services;

public class ChatProviderConfig
{
    /// <summary>
    /// Префикс для плейсхолдеров конфигурации.
    /// </summary>
    public const string PlaceholderPrefix = "[YOUR_";

    /// <summary>
    /// Суффикс для плейсхолдеров конфигурации.
    /// </summary>
    public const string PlaceholderSuffix = "]";

    /// <summary>
    /// Имя провайдера (например, "OpenAI", "Gemini").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Тип провайдера для выбора стратегии обработки.
    /// </summary>
    public required AiProvider ProviderType { get; set; }

    /// <summary>
    /// API ключ для доступа к сервису.
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// Базовый URL API (если отличается от стандартного).
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Имя модели для текстовых запросов.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Имя модели для генерации изображений.
    /// </summary>
    public string? DrawingModelName { get; set; }

    public static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.StartsWith(PlaceholderPrefix) && value.EndsWith(PlaceholderSuffix);
    }
}
