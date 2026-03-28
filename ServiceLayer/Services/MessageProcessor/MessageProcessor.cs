using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Images;
using ServiceLayer.Constans;
using ServiceLayer.Services.GptChat;
using ServiceLayer.Services.GptChat.Configurations;
using ServiceLayer.Services.Telegram.Configuretions;
using ServiceLayer.Utils;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Message = OpenAI.Chat.Message;

namespace ServiceLayer.Services.MessageProcessor;
public class MessageProcessor : BaseService
{
    private readonly IRepository<HistoryMessage> _historyMessageRepository;
    private readonly ChatGptService _chatGptService;
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly string[] drawWords = new[]
    {
        "draw", "рисуй", "покажи", "малю"
    };
    private readonly TelegramBotConfiguration _telegramBotConfiguration;
    private readonly GptChatConfiguration _gptChatConfiguration;

    public MessageProcessor(IServiceProvider serviceProvider, ILogger<MessageProcessor> logger,
        IRepository<HistoryMessage> historyMessageRepository, ChatGptService chatGptService,
        ITelegramBotClient telegramBotClient)
        : base(serviceProvider, logger)
    {
        _historyMessageRepository = historyMessageRepository;
        _chatGptService = chatGptService;
        _telegramBotClient = telegramBotClient;
        AppSettings? appConfig = _serviceProvider.GetConfiguration<AppSettings>();
        _telegramBotConfiguration = appConfig.TelegramBotConfiguration;
        _gptChatConfiguration = appConfig.GptChatConfiguration;
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
        if (await IsNeedDrawImage(chatId, fromUserId, message))
        {
            var botInfo = await _telegramBotClient.GetMeAsync();
            var msg = message.Replace(botInfo.Username, string.Empty);
            var sb = new StringBuilder();
            try
            {
                IReadOnlyList<string> imageResults = await _chatGptService.GenerateImage(chatId, fromUserId, msg);
                foreach (var image in imageResults.Where(p => !string.IsNullOrWhiteSpace(p)))
                {
                    sb.AppendLine(image);
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
            var gptChatResponce = await _chatGptService.SendMessages2ChatAsync(chatId, fromUserId, query);
            var sb = new StringBuilder();
            foreach (var choice in gptChatResponce.Choices)
            {
                sb.AppendLine(choice.Message.ToString());
            }
            responceText = sb.ToString();
            _logger.LogInformation("Message from gpt: {0}", responceText);
            sb = new StringBuilder();
            if (_telegramBotConfiguration.DefaultParseMode == ParseMode.Html)
            {
                responceText = responceText.ConvertMarkdownToHtml();
                responceText = responceText.ConvertHtmlToTelegramHtml();
                sb.AppendLine(responceText);
                sb.AppendLine($"<i><u>Usage:</u>{gptChatResponce.GetUsage()}</i>");
            }
            else
            {
                var responceHtml = responceText.ConvertMarkdownToHtml();
                responceHtml = responceHtml.ConvertHtmlToTelegramHtml();
                responceText = responceHtml.ConvertHtmlToMarkdown();
                sb.AppendLine(responceText);
                sb.AppendLine($"Usage:{gptChatResponce.GetUsage().EncodeToMarkdown()}");
            }
            toSendText = sb.ToString();

        }
        var telegramResponce = await _telegramBotClient.SendTextMessageAsync(
                chatId: chatId,
                text: toSendText,
                parseMode: _telegramBotConfiguration.DefaultParseMode,
                replyToMessageId: (int?)messageId,
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
            Text = responceText
        };
        _historyMessageRepository.Add(historyMessage);
        await _historyMessageRepository.SaveChanges();
        return responceText;
    }

    internal async Task<IReadOnlyList<OpenAI.Models.Model>> GetGPTModels()
    {
        IReadOnlyList<OpenAI.Models.Model> result = await _chatGptService.GetAvailibleModels();
        return result;
    }

    internal async Task<global::Telegram.Bot.Types.Message> ProcessImage(long chatId, int messageId, int? replyToMessagemessageId,
        long userId, string? messageText, string filePath, CancellationToken cancellationToken)
    {
        var results = await _chatGptService.CreateImageEditAsync(chatId, userId, filePath, messageText, ImageSize.Medium);
        var sb = new StringBuilder();
        foreach (var image in results.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            sb.AppendLine(image);
        }
        var toSendText = sb.ToString();
        var telegramResponce = await _telegramBotClient.SendTextMessageAsync(
                chatId: chatId,
                text: toSendText,
                parseMode: _telegramBotConfiguration.DefaultParseMode,
                replyToMessageId: (int?)messageId,
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        return telegramResponce;
    }

    internal async Task<(bool, string)> SelectGPTModel(string? modelName)
    {
        return await _chatGptService.SetGPTModel(modelName);
    }

    private async Task<bool> IsNeedDrawImage(long charId, long userId, string text)
    {
        var isContainsWords = drawWords.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
        if (!isContainsWords)
        {
            return false;
        }
        const string confirmFrase = "скорее да";
        var isNeewDrawResponce = await _chatGptService.Asc(charId, userId,
            $"Эта фраза '{text}' содержит просьбу нарисовать что-то? Ответь: '{confirmFrase}, чем нет' или 'скорее нет, чем да'");
        _logger.LogInformation(confirmFrase);
        return isNeewDrawResponce.Contains(confirmFrase, StringComparison.OrdinalIgnoreCase);

    }
    private async Task<bool> IsVoiceMessageToBot(long charId, long userId, string text)
    {
        const string confirmFrase = "скорее да";
        string isNeewDrawResponce = await _chatGptService.Asc(charId, userId,
            $"Эта фраза '{text}' обращена к тебе? Ответь: '{confirmFrase}, чем нет' или 'скорее нет, чем да'");
        _logger.LogInformation($"GPT Bot responce: {isNeewDrawResponce}");
        return isNeewDrawResponce.Contains(confirmFrase, StringComparison.OrdinalIgnoreCase);

    }
}
