namespace ServiceLayer.Utils;

/// <summary>
/// Интерфейс для статических строковых перечислений (мультитонов).
/// </summary>
/// <typeparam name="TSelf">Тип самого класса перечисления.</typeparam>
public interface IStaticStringEnum<TSelf> where TSelf : class, IStaticStringEnum<TSelf>
{
    /// <summary>
    /// Текстовое значение константы.
    /// </summary>
    string Value { get; }

    /// <summary>
    /// Возвращает список всех доступных в классе полей (инстансов).
    /// </summary>
    static abstract IEnumerable<TSelf> GetAll();

    /// <summary>
    /// Стратегия сравнения по умолчанию (регистрозависимость).
    /// </summary>
    static virtual bool DefaultIgnoreCase => false;

    /// <summary>
    /// Проверяет, является ли текстовое значение одним из полей класса.
    /// </summary>
    static abstract bool IsValid(string? value, bool? ignoreCase = null);

    /// <summary>
    /// Преобразует строку в инстанс класса. Возвращает null, если совпадений не найдено.
    /// </summary>
    static abstract TSelf? FromString(string? value, bool? ignoreCase = null);
}
