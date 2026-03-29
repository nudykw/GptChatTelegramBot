using ServiceLayer.Constans;

namespace ServiceLayer.Services;

public class ChatProviderConfig
{
    /// <summary>
    /// Configuration placeholder prefix.
    /// </summary>
    public const string PlaceholderPrefix = "[YOUR_";

    /// <summary>
    /// Configuration placeholder suffix.
    /// </summary>
    public const string PlaceholderSuffix = "]";

    /// <summary>
    /// Provider name (e.g., "OpenAI", "Gemini").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Provider type for processing strategy selection.
    /// </summary>
    public required AiProvider ProviderType { get; set; }

    /// <summary>
    /// API key for service access.
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// Base API URL (if different from default).
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Model name for text requests.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Model name for image generation.
    /// </summary>
    public string? DrawingModelName { get; set; }

    public static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.StartsWith(PlaceholderPrefix) && value.EndsWith(PlaceholderSuffix);
    }
}
