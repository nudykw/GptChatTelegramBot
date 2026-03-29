using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Images;
using ServiceLayer.Constans;
using ServiceLayer.Services.GptChat;
using ServiceLayer.Utils;

using ServiceLayer.Services.Telegram.Configuretions;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Message = OpenAI.Chat.Message;

namespace ServiceLayer.Services.MessageProcessor;
public class MessageProcessor : BaseService
{
    private readonly IRepository<HistoryMessage> _historyMessageRepository;
    private readonly IChatService _chatService;
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly string[] drawWords = new[]
    {
        "draw", "рисуй", "покажи", "малю"
    };
    private readonly TelegramBotConfiguration _telegramBotConfiguration;

    public MessageProcessor(IServiceProvider serviceProvider, ILogger<MessageProcessor> logger,
        IRepository<HistoryMessage> historyMessageRepository, IChatService chatService,
        ITelegramBotClient telegramBotClient)
        : base(serviceProvider, logger)
    {
        _historyMessageRepository = historyMessageRepository;
        _chatService = chatService;
        _telegramBotClient = telegramBotClient;
        AppSettings? appConfig = _serviceProvider.GetConfiguration<AppSettings>();
        _telegramBotConfiguration = appConfig.TelegramBotConfiguration;
    }

    public async Task<string> ProcessMessage(long chatId, long messageId, long? parentMessageId,
        long fromUserId, string message, CancellationToken cancellationToken)
    {
        if (message.StartsWith(MessageMarkers.VoiceMessage))
        {
            var isPrivateMessage = message.StartsWith(MessageMarkers.VoiceMessageToBot);
            message = message.Replace(MessageMarkers.VoiceMessageToBot, string.Empty)
                .Replace(MessageMarkers.VoiceMessage, string.Empty);
            if (!(isPrivateMessage || await IsVoiceMessageToBot(chatId, fromUserId, message)))
            {
                _logger.LogInformation("This voice message not for me.");
                return string.Empty;
            }
        }
        var historyMessage =
            await _historyMessageRepository.Get(p => p.ChatId == chatId && p.MessageId == messageId);
        if (historyMessage != null)
        {
            historyMessage.ParentMessageId = parentMessageId;
            historyMessage.Text = message;
            historyMessage.ModifiedDate = DateTime.UtcNow;
            _historyMessageRepository.Update(historyMessage);
        }
        else
        {
            historyMessage = new HistoryMessage
            {
                ChatId = chatId,
                MessageId = messageId,
                ParentMessageId = parentMessageId,
                RoleId = (int)OpenAI.Role.User,
                Text = message,
                CreationDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                FromUserName = fromUserId.ToString()
            };
            _historyMessageRepository.Add(historyMessage);
        }
        await _historyMessageRepository.SaveChanges();
        var query = new List<Message>()
        {
            new Message((OpenAI.Role)historyMessage.RoleId, message, fromUserId.ToString())
        };
        while (historyMessage != null && historyMessage.ParentMessageId != null)
        {
            historyMessage = await _historyMessageRepository.Get(p => p.ChatId == historyMessage.ChatId
            && p.MessageId == historyMessage.ParentMessageId);
            if (historyMessage != null)
            {
                query.Insert(0, new Message((OpenAI.Role)historyMessage.RoleId, historyMessage.Text,
                    historyMessage.FromUserName));
            }
        }
        string toSendText = string.Empty;
        string responceText = string.Empty;
        string? usedProviderName = null;
        string? usedModelName = null;

        if (await IsNeedDrawImage(chatId, fromUserId, message))
        {
            var botInfo = await _telegramBotClient.GetMe();
            var msg = message.Replace(botInfo.Username, string.Empty);
            var sb = new StringBuilder();
            try
            {
                var gptImageResponce = await _chatService.GenerateImage(chatId, fromUserId, msg);
                foreach (var image in gptImageResponce.Choices.Where(p => !string.IsNullOrWhiteSpace(p)))
                {
                    sb.AppendLine(image);
                }

                var summary = $"{gptImageResponce.ProviderName} | {gptImageResponce.ModelName} | " +
                              $"In:{gptImageResponce.PromptTokens} Out:{gptImageResponce.CompletionTokens} | " +
                              $"L:{gptImageResponce.LatencySeconds:F1}s | ${gptImageResponce.Cost:F4}";

                if (_telegramBotConfiguration.DefaultParseMode == ParseMode.Html)
                {
                    sb.AppendLine($"<tg-spoiler><i>{summary}</i></tg-spoiler>");
                }
                else
                {
                    sb.AppendLine($"||_{summary}_||");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine(ex.Message);
            }
            toSendText = sb.ToString();
        }
        else
        {
            var gptChatResponce = await _chatService.SendMessages2ChatAsync(chatId, fromUserId, query);
            var sb = new StringBuilder();
            foreach (var choice in gptChatResponce.Choices)
            {
                sb.AppendLine(choice);
            }
            responceText = sb.ToString();
            _logger.LogInformation("Message from gpt: {0}", responceText);
            var summary = $"{gptChatResponce.ProviderName} | {gptChatResponce.ModelName} | " +
                          $"In:{gptChatResponce.PromptTokens} Out:{gptChatResponce.CompletionTokens}" +
                          (gptChatResponce.ReasoningTokens > 0 ? $" (R:{gptChatResponce.ReasoningTokens})" : "") +
                          $" | T:{gptChatResponce.TotalTokens} | {gptChatResponce.TokensPerSecond:F1}t/s | {gptChatResponce.LatencySeconds:F1}s | ${gptChatResponce.Cost:F4}";

            if (_telegramBotConfiguration.DefaultParseMode == ParseMode.Html)
            {
                responceText = responceText.ConvertMarkdownToHtml();
                responceText = responceText.ConvertHtmlToTelegramHtml();
                sb.AppendLine(responceText);
                sb.AppendLine($"<tg-spoiler><i>{summary}</i></tg-spoiler>");
            }
            else
            {
                var responceHtml = responceText.ConvertMarkdownToHtml();
                responceHtml = responceHtml.ConvertHtmlToTelegramHtml();
                responceText = responceHtml.ConvertHtmlToMarkdown();
                sb.AppendLine(responceText);
                sb.AppendLine($"||_{summary}_||"); // Markdown spoiler and italic
            }
            toSendText = sb.ToString();
            usedProviderName = gptChatResponce.ProviderName;
            usedModelName = gptChatResponce.ModelName;
        }
        var telegramResponce = await _telegramBotClient.SendMessage(
                chatId: chatId,
                text: toSendText,
                parseMode: _telegramBotConfiguration.DefaultParseMode,
                replyParameters: new ReplyParameters() { MessageId = (int)messageId },
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        historyMessage = new HistoryMessage
        {
            ChatId = telegramResponce.Chat.Id,
            CreationDate = DateTime.UtcNow,
            MessageId = telegramResponce.MessageId,
            ModifiedDate = DateTime.UtcNow,
            ParentMessageId = telegramResponce.ReplyToMessage.MessageId,
            RoleId = (int)OpenAI.Role.Assistant,
            Text = responceText,
            ProviderName = usedProviderName,
            ModelName = usedModelName
        };
        _historyMessageRepository.Add(historyMessage);
        await _historyMessageRepository.SaveChanges();
        return responceText;
    }

    internal async Task<IReadOnlyList<OpenAI.Models.Model>> GetGPTModels(long userId)
    {
        IReadOnlyList<OpenAI.Models.Model> result = await _chatService.GetAvailibleModels(userId);
        return result;
    }

    internal async Task<global::Telegram.Bot.Types.Message> ProcessImage(long chatId, int messageId, int? replyToMessagemessageId,
        long userId, string? messageText, string filePath, CancellationToken cancellationToken)
    {
        var gptImageResponce = await _chatService.CreateImageEditAsync(chatId, userId, filePath, messageText);
        var sb = new StringBuilder();
        foreach (var image in gptImageResponce.Choices.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            sb.AppendLine(image);
        }

        var summary = $"{gptImageResponce.ProviderName} | {gptImageResponce.ModelName} | " +
                      $"In:{gptImageResponce.PromptTokens} Out:{gptImageResponce.CompletionTokens} | " +
                      $"L:{gptImageResponce.LatencySeconds:F1}s | ${gptImageResponce.Cost:F4}";

        if (_telegramBotConfiguration.DefaultParseMode == ParseMode.Html)
        {
            sb.AppendLine($"<tg-spoiler><i>{summary}</i></tg-spoiler>");
        }
        else
        {
            sb.AppendLine($"||_{summary}_||");
        }

        var toSendText = sb.ToString();
        var telegramResponce = await _telegramBotClient.SendMessage(
                chatId: chatId,
                text: toSendText,
                parseMode: _telegramBotConfiguration.DefaultParseMode,
                replyParameters: replyToMessagemessageId.HasValue ? new ReplyParameters() { MessageId = replyToMessagemessageId.Value } : null,
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        return telegramResponce;
    }

    internal async Task<(bool, string)> SelectGPTModel(string? modelName, long userId)
    {
        return await _chatService.SetGPTModel(modelName, userId);
    }

    internal async Task SetPreferredProvider(long userId, ChatStrategy strategy)
    {
        var userRepository = _serviceProvider.GetRequiredService<IRepository<TelegramUserInfo>>();
        var user = await userRepository.Get(p => p.Id == userId);
        if (user != null)
        {
            user.PreferredProvider = strategy;
            userRepository.Update(user);
            await userRepository.SaveChanges();
        }
    }

    private async Task<bool> IsNeedDrawImage(long charId, long userId, string text)
    {
        var isContainsWords = drawWords.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
        if (!isContainsWords)
        {
            return false;
        }
        const string confirmFrase = "скорее да";
        var isNeewDrawResponce = await _chatService.Ask(charId, userId,
            $"Эта фраза '{text}' содержит просьбу нарисовать что-то? Ответь: '{confirmFrase}, чем нет' или 'скорее нет, чем да'");
        _logger.LogInformation(confirmFrase);
        return isNeewDrawResponce.Contains(confirmFrase, StringComparison.OrdinalIgnoreCase);

    }
    private async Task<bool> IsVoiceMessageToBot(long charId, long userId, string text)
    {
        const string confirmFrase = "скорее да";
        string isNeewDrawResponce = await _chatService.Ask(charId, userId,
            $"Эта фраза '{text}' обращена к тебе? Ответь: '{confirmFrase}, чем нет' или 'скорее нет, чем да'");
        _logger.LogInformation($"GPT Bot responce: {isNeewDrawResponce}");
        return isNeewDrawResponce.Contains(confirmFrase, StringComparison.OrdinalIgnoreCase);

    }
}
