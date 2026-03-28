namespace ServiceLayer.Constans;

public static class BotCommands
{
    public const string Billing = "/billing";
    public const string Model = "/model";
    public const string Provider = "/provider";
    public const string Restart = "/restart";

    public static readonly Dictionary<string, string> Descriptions = new()
    {
        { Billing, "Показать статистику использования" },
        { Model, "Выбрать модель GPT" },
        { Provider, "Выбрать провайдера ИИ" },
        { Restart, "Перезагрузить бота" }
    };
}
