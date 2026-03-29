using System.ComponentModel;
using ServiceLayer.Utils;

namespace ServiceLayer.Constans;

/// <summary>
/// Провайдеры ИИ.
/// </summary>
[TypeConverter(typeof(StaticStringEnumTypeConverter<AiProvider>))]
public sealed class AiProvider : StaticStringEnumBase<AiProvider>, IStaticStringEnum<AiProvider>
{
    private AiProvider(string value, string displayName, string? defaultDrawingModel = null) : base(value)
    {
        DisplayName = displayName;
        DefaultDrawingModel = defaultDrawingModel;
    }

    public string DisplayName { get; }

    /// <summary>
    /// Модель для рисования по умолчанию для этого провайдера.
    /// </summary>
    public string? DefaultDrawingModel { get; }

    public static readonly AiProvider OpenAI = new("openai", "OpenAI (GPT-4o, GPT-3.5)", AiModel.DallE3);
    public static readonly AiProvider Gemini = new("gemini", "Google Gemini (1.5 Pro/Flash)", AiModel.Imagen3);
    public static readonly AiProvider DeepSeek = new("deepseek", "DeepSeek (V3, R1)");
    public static readonly AiProvider Grok = new("grok", "xAI Grok");

    /// <summary>
    /// Провайдеры обычно регистронезависимы в конфигах.
    /// </summary>
    public static bool DefaultIgnoreCase => true;

    /// <summary>
    /// Неявное преобразование из строки (для парсинга из конфига или БД).
    /// </summary>
    public static implicit operator AiProvider?(string? value) => FromString(value);
}
