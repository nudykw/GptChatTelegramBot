using ServiceLayer.Constans;

namespace ServiceLayer.Services;

public class ChatProviderConfig
{
    public const string PlaceholderPrefix = "[YOUR_";
    public const string PlaceholderSuffix = "]";

    public required string Name { get; set; }
    public required AiProvider ProviderType { get; set; }
    public required string ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? ModelName { get; set; }
    public string? DrawingModelName { get; set; }

    public static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.StartsWith(PlaceholderPrefix) && value.EndsWith(PlaceholderSuffix);
    }
}
