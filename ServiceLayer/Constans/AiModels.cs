using ServiceLayer.Utils;

namespace ServiceLayer.Constans;

/// <summary>
/// Стандартные названия моделей ИИ.
/// </summary>
public sealed class AiModel : StaticStringEnumBase<AiModel>, IStaticStringEnum<AiModel>
{
    private AiModel(string value) : base(value) { }

    // OpenAI Models
    public static readonly AiModel Gpt4o = new("gpt-4o");
    public static readonly AiModel Gpt4oMini = new("gpt-4o-mini");
    public static readonly AiModel Gpt4Turbo = new("gpt-4-turbo");
    public static readonly AiModel Gpt35Turbo = new("gpt-3.5-turbo");

    // Gemini Models
    public static readonly AiModel GeminiPro = new("gemini-pro");
    public static readonly AiModel GeminiFlash = new("gemini-1.5-flash");

    // DeepSeek Models
    public static readonly AiModel DeepSeekChat = new("deepseek-chat");
    public static readonly AiModel DeepSeekReasoner = new("deepseek-reasoner");

    // Grok Models
    public static readonly AiModel Grok2 = new("grok-2");

    public static implicit operator AiModel?(string? value) => FromString(value);
}
