using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceLayer.Utils;

/// <summary>
/// Базовая реализация для статических перечислений (мультитонов).
/// </summary>
/// <typeparam name="TSelf">Тип наследника.</typeparam>
[JsonConverter(typeof(StaticStringEnumJsonConverterFactory))]
public abstract class StaticStringEnumBase<TSelf> : IStaticStringEnum<TSelf> 
    where TSelf : StaticStringEnumBase<TSelf>, IStaticStringEnum<TSelf>
{
    private static readonly Lazy<IEnumerable<TSelf>> _allInstances = new(() => 
        typeof(TSelf)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(TSelf))
            .Select(f => (TSelf?)f.GetValue(null))
            .Where(v => v != null)
            .Cast<TSelf>()
            .ToList());

    protected StaticStringEnumBase(string value)
    {
        Value = value;
    }

    public string Value { get; }

    // Мы не определяем DefaultIgnoreCase здесь как static, 
    // чтобы он брался из интерфейса IStaticStringEnum<TSelf>.

    public static IEnumerable<TSelf> GetAll() => _allInstances.Value;

    public static bool IsValid(string? value, bool? ignoreCase = null)
    {
        return FromString(value, ignoreCase) != null;
    }

    public static TSelf? FromString(string? value, bool? ignoreCase = null)
    {
        if (value == null) return null;
        
        bool useIgnoreCase = ignoreCase ?? TSelf.DefaultIgnoreCase;

        var comp = useIgnoreCase 
            ? StringComparison.OrdinalIgnoreCase 
            : StringComparison.Ordinal;

        return GetAll().FirstOrDefault(i => string.Equals(i.Value, value, comp));
    }

    public static TSelf Parse(string s, IFormatProvider? provider = null)
    {
        return FromString(s) ?? throw new FormatException($"Invalid value for {typeof(TSelf).Name}: {s}");
    }

    public static bool TryParse(string? value, out TSelf? result)
    {
        result = FromString(value);
        return result != null;
    }

    public static bool TryParse(string? value, IFormatProvider? provider, out TSelf? result)
    {
        return TryParse(value, out result);
    }

    private static bool GetDefaultIgnoreCase() => TSelf.DefaultIgnoreCase;

    // Неявное преобразование в строку
    public static implicit operator string(StaticStringEnumBase<TSelf>? instance) => instance?.Value ?? string.Empty;

    // Неявное преобразование из строки (возвращает базовый тип)
    public static implicit operator StaticStringEnumBase<TSelf>?(string? value) => FromString(value);

    // Операторы сравнения
    public static bool operator ==(StaticStringEnumBase<TSelf>? left, StaticStringEnumBase<TSelf>? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(StaticStringEnumBase<TSelf>? left, StaticStringEnumBase<TSelf>? right) => !(left == right);

    public override string ToString() => Value;

    public override bool Equals(object? obj)
    {
        if (obj is StaticStringEnumBase<TSelf> other)
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        return false;
    }

    public override int GetHashCode() => Value.GetHashCode();
}

/// <summary>
/// TypeConverter для поддержки ConfigurationBinder и других механизмов .NET.
/// Должен быть применен к конкретному наследнику: [TypeConverter(typeof(StaticStringEnumTypeConverter&lt;T&gt;))]
/// </summary>
public class StaticStringEnumTypeConverter<T> : TypeConverter where T : StaticStringEnumBase<T>, IStaticStringEnum<T>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
    {
        if (value is string s)
        {
            return T.FromString(s);
        }
        return base.ConvertFrom(context, culture, value);
    }
}

/// <summary>
/// Фабрика для создания JsonConverter для любых наследников StaticStringEnumBase.
/// </summary>
public class StaticStringEnumJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(StaticStringEnumBase<>) 
            || (typeToConvert.BaseType?.IsGenericType == true && typeToConvert.BaseType.GetGenericTypeDefinition() == typeof(StaticStringEnumBase<>));
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type tSelf = typeToConvert;
        Type converterType = typeof(StaticStringEnumJsonConverter<>).MakeGenericType(tSelf);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// JsonConverter для конкретного типа TSelf.
/// </summary>
public class StaticStringEnumJsonConverter<TSelf> : JsonConverter<TSelf> 
    where TSelf : StaticStringEnumBase<TSelf>, IStaticStringEnum<TSelf>
{
    public override TSelf? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        return TSelf.FromString(value);
    }

    public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
