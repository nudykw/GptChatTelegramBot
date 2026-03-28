namespace ServiceLayer.Services.GptChat.Configurations;
public class GptChatConfiguration
{
    public static readonly string Configuration = "GptChatConfiguration";
    public string APIKey { get; set; } = "";
    public string ModelName { get; set; } = "gpt-4-1106-preview";
}
