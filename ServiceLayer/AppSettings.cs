using ServiceLayer.Services.GeminiChat.DotNet.Configurations;
using ServiceLayer.Services.GptChat.Configurations;
using ServiceLayer.Services.Telegram.Configuretions;

namespace ServiceLayer.Services;

public class AppSettings
{
    public static readonly string Configuration = "AppSettings";
    public required TelegramBotConfiguration TelegramBotConfiguration { get; set; }
    public required GptChatConfiguration GptChatConfiguration { get; set; }
    public required GeminiChatConfiguration GeminiChatConfiguration { get; set; }
    public List<ChatProviderConfig> ChatProviders { get; set; } = new();
}
