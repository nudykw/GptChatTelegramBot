using System.Reflection;

namespace ServiceLayer.Utils;

/// <summary>
/// Базовая реализация для статических перечислений (мультитонов).
/// </summary>
/// <typeparam name="TSelf">Тип наследника.</typeparam>
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
