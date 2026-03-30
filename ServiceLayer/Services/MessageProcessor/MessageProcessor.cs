using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Images;
using ServiceLayer.Constans;
using ServiceLayer.Services.OpenAI;
using ServiceLayer.Utils;
using ServiceLayer.Services.Localization;

using ServiceLayer.Services.Telegram.Configuretions;
using System.Globalization;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ServiceLayer.Services.MessageProcessor;
public class MessageProcessor : BaseService
{
    private readonly IRepository<HistoryMessage> _historyMessageRepository;
    private readonly IRepository<TelegramUserInfo> _telegramUserInfoRepository;
    private readonly IChatService _chatService;
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IDynamicLocalizer _localizer;
    private readonly string[] drawWords = new[]
    {
        "draw", "рисуй", "покажи", "малю"
    };
    private readonly TelegramBotConfiguration _telegramBotConfiguration;

    public MessageProcessor(IServiceProvider serviceProvider, ILogger<MessageProcessor> logger,
        IRepository<HistoryMessage> historyMessageRepository, IChatService chatService,
        ITelegramBotClient telegramBotClient, IRepository<TelegramUserInfo> telegramUserInfoRepository,
        IDynamicLocalizer localizer)
        : base(serviceProvider, logger)
    {
        _historyMessageRepository = historyMessageRepository;
        _chatService = chatService;
        _telegramBotClient = telegramBotClient;
        _telegramUserInfoRepository = telegramUserInfoRepository;
        _localizer = localizer;
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
                RoleId = (int)global::OpenAI.Role.User,
                Text = message,
                CreationDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                FromUserName = fromUserId.ToString()
            };
            _historyMessageRepository.Add(historyMessage);
        }
        await _historyMessageRepository.SaveChanges();
        // Identify preferred language for prompt attachment
        var user = await _telegramUserInfoRepository.Get(p => p.Id == fromUserId);
        var langCode = user?.LanguageCode ?? LanguageCode.English;
        var culture = new CultureInfo(langCode);
        var languageName = culture.EnglishName.Split(' ')[0]; // Use English name for AI prompt stability

        var query = new List<AiMessage>()
        {
            new AiMessage((global::OpenAI.Role)historyMessage.RoleId, $"{message} (Respond in {languageName})", fromUserId.ToString())
        };
        while (historyMessage != null && historyMessage.ParentMessageId != null)
        {
            historyMessage = await _historyMessageRepository.Get(p => p.ChatId == historyMessage.ChatId
            && p.MessageId == historyMessage.ParentMessageId);
            if (historyMessage != null)
            {
                query.Insert(0, new AiMessage((global::OpenAI.Role)historyMessage.RoleId, historyMessage.Text,
                    historyMessage.FromUserName));
            }
        }

        string toSendText = string.Empty;
        string responceText = string.Empty;
        string? usedProviderName = null;
        string? usedModelName = null;

        if (await IsNeedDrawImage(chatId, fromUserId, message))
        {
            toSendText = await InternalProcessDrawImage(chatId, fromUserId, message);
        }
        else
        {
            var aiResponse = await _chatService.SendMessages2ChatAsync(chatId, fromUserId, query);
            var sb = new StringBuilder();
            foreach (var choice in aiResponse.Choices)
            {
                sb.AppendLine(choice);
            }
            responceText = sb.ToString();
            _logger.LogInformation("Message from AI: {0}", responceText);
            var balanceDisplay = await GetBalanceDisplay(fromUserId);
            var summary = $"{aiResponse.ProviderName} | {aiResponse.ModelName} | " +
                          $"In:{aiResponse.PromptTokens} Out:{aiResponse.CompletionTokens}" +
                          (aiResponse.ReasoningTokens > 0 ? $" (R:{aiResponse.ReasoningTokens})" : "") +
                          $" | T:{aiResponse.TotalTokens} | {aiResponse.TokensPerSecond:F1}t/s | {aiResponse.LatencySeconds:F1}s | ${aiResponse.Cost:F4}{balanceDisplay}";

            if (_telegramBotConfiguration.DefaultParseMode == ParseMode.Html)
            {
                responceText = responceText.ConvertMarkdownToHtml();
                responceText = responceText.ConvertHtmlToTelegramHtml();
                // Avoid duplicating the response text twice if sb already has it
                // Actually sb was empty before, then responceText was added, then summary.
                sb.Clear(); // Clear to rebuild with correct HTML
                sb.AppendLine(responceText);
                sb.AppendLine($"<tg-spoiler><i>{summary}</i></tg-spoiler>");
            }
            else
            {
                var responceHtml = responceText.ConvertMarkdownToHtml();
                responceHtml = responceHtml.ConvertHtmlToTelegramHtml();
                responceText = responceHtml.ConvertHtmlToMarkdown();
                sb.Clear();
                sb.AppendLine(responceText);
                sb.AppendLine($"||_{summary}_||"); // Markdown spoiler and italic
            }
            toSendText = sb.ToString();
            usedProviderName = aiResponse.ProviderName;
            usedModelName = aiResponse.ModelName;
        }
        var chunks = SplitText(toSendText, 4000).ToList();
        TgMessage telegramResponce = null;
        
        foreach (var chunk in chunks)
        {
            try
            {
                telegramResponce = await _telegramBotClient.SendMessage(
                        chatId: chatId,
                        text: chunk,
                        parseMode: _telegramBotConfiguration.DefaultParseMode,
                        replyParameters: new ReplyParameters() { MessageId = (int)messageId },
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken);
            }
            catch (global::Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 400 && ex.Message.Contains("can't parse entities"))
            {
                _logger.LogWarning(ex, "Failed to parse entities in chunk, sending without formatting");
                telegramResponce = await _telegramBotClient.SendMessage(
                        chatId: chatId,
                        text: chunk,
                        parseMode: global::Telegram.Bot.Types.Enums.ParseMode.None,
                        replyParameters: new ReplyParameters() { MessageId = (int)messageId },
                        replyMarkup: new ReplyKeyboardRemove(),
                        cancellationToken: cancellationToken);
            }
        }

        if (telegramResponce != null)
        {
            historyMessage = new HistoryMessage
            {
                ChatId = telegramResponce.Chat.Id,
                CreationDate = DateTime.UtcNow,
                MessageId = telegramResponce.MessageId,
                ModifiedDate = DateTime.UtcNow,
                ParentMessageId = telegramResponce.ReplyToMessage?.MessageId,
                RoleId = (int)global::OpenAI.Role.Assistant,
                Text = responceText,
                ProviderName = usedProviderName,
                ModelName = usedModelName
            };
            _historyMessageRepository.Add(historyMessage);
            await _historyMessageRepository.SaveChanges();
        }
        return responceText;
    }

    private IEnumerable<string> SplitText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        
        for (int i = 0; i < text.Length; )
        {
            int length = Math.Min(maxLength, text.Length - i);
            
            if (i + length < text.Length)
            {
                int lastNewline = text.LastIndexOf('\n', i + length - 1, length);
                if (lastNewline > i)
                {
                    length = lastNewline - i + 1;
                }
            }
            
            yield return text.Substring(i, length);
            i += length;
        }
    }

    public async Task<TgMessage> ProcessDrawCommand(long chatId, long messageId, long fromUserId, string prompt, CancellationToken cancellationToken)
    {
        var toSendText = await InternalProcessDrawImage(chatId, fromUserId, prompt);
        var telegramResponce = await _telegramBotClient.SendMessage(
                chatId: chatId,
                text: toSendText,
                parseMode: _telegramBotConfiguration.DefaultParseMode,
                replyParameters: new ReplyParameters() { MessageId = (int)messageId },
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        return telegramResponce;
    }

    private async Task<string> InternalProcessDrawImage(long chatId, long fromUserId, string message)
    {
        var botInfo = await _telegramBotClient.GetMe();
        var msg = message.Replace(botInfo.Username, string.Empty);
        var sb = new StringBuilder();
        try
        {
            var aiImageResponse = await _chatService.GenerateImage(chatId, fromUserId, msg);
            foreach (var image in aiImageResponse.Choices.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                sb.AppendLine(image);
            }

            var balanceDisplay = await GetBalanceDisplay(fromUserId);
            var summary = $"{aiImageResponse.ProviderName} | {aiImageResponse.ModelName} | " +
                          $"In:{aiImageResponse.PromptTokens} Out:{aiImageResponse.CompletionTokens} | " +
                          $"L:{aiImageResponse.LatencySeconds:F1}s | ${aiImageResponse.Cost:F4}{balanceDisplay}";

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
        return sb.ToString();
    }

    internal async Task<IReadOnlyList<global::OpenAI.Models.Model>> GetAIModels(long userId)
    {
        IReadOnlyList<global::OpenAI.Models.Model> result = await _chatService.GetAvailibleModels(userId);
        return result;
    }

    internal async Task<TgMessage> ProcessImage(long chatId, int messageId, int? replyToMessagemessageId,
        long userId, string? messageText, string filePath, CancellationToken cancellationToken)
    {
        var aiImageResponse = await _chatService.CreateImageEditAsync(chatId, userId, filePath, messageText);
        var sb = new StringBuilder();
        foreach (var image in aiImageResponse.Choices.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            sb.AppendLine(image);
        }

        var balanceDisplay = await GetBalanceDisplay(userId);
        var summary = $"{aiImageResponse.ProviderName} | {aiImageResponse.ModelName} | " +
                      $"In:{aiImageResponse.PromptTokens} Out:{aiImageResponse.CompletionTokens} | " +
                      $"L:{aiImageResponse.LatencySeconds:F1}s | ${aiImageResponse.Cost:F4}{balanceDisplay}";

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

    internal async Task<(bool, string)> SelectAIModel(string? modelName, long userId)
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

    public async Task<string?> IdentifyLanguage(long chatId, long userId, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }
        
        var normalizedInput = input.Trim().ToLowerInvariant();
        if (normalizedInput == LanguageCode.UkraineShorthand || normalizedInput == LanguageCode.Ukrainian) return LanguageCode.Ukrainian;
        if (normalizedInput == LanguageCode.Russian) return LanguageCode.Russian;
        if (normalizedInput == LanguageCode.English) return LanguageCode.English;

        var prompt = $"Identify the TARGET language being requested or mentioned in this input: '{input}'. " +
                     $"Return ONLY the BCP-47 language code (e.g., '{LanguageCode.English}', '{LanguageCode.Ukrainian}', 'de', 'fr', 'es', '{LanguageCode.Russian}'). " +
                     "Do NOT return the language of the input word itself if it's naming a different language (e.g., if input is 'Spanish' or 'Испанский', return 'es'). " +
                     "Do NOT translate. Just return the 2-letter or standard code. " +
                     $"If you are not sure, return '{LanguageCode.English}'.";
        
        var result = await _chatService.Ask(chatId, userId, prompt);
        var langCode = result.Trim().ToLowerInvariant();
        
        if (langCode == LanguageCode.UkraineShorthand) langCode = LanguageCode.Ukrainian;
        
        // Basic validation
        if (langCode.Length > 10 || langCode.Any(char.IsWhiteSpace))
        {
            return LanguageCode.English;
        }

        return langCode;
    }

    private async Task<bool> IsNeedDrawImage(long charId, long userId, string text)
    {
        var isContainsWords = drawWords.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
        if (!isContainsWords)
        {
            return false;
        }
        // We use the localizer to get the English version of the prompt 
        // to ensure AI stability, regardless of the user's current culture.
        var oldCulture = CultureInfo.CurrentUICulture;
        string confirmPhrase;
        string prompt;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(LanguageCode.English);
            confirmPhrase = _localizer["ConfirmPhrase_Yes"];
            prompt = _localizer["Prompt_IsNeedDraw", text, confirmPhrase];
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldCulture;
        }

        var isNeewDrawResponce = await _chatService.Ask(charId, userId, prompt);
        _logger.LogInformation("Intent detection response: {Response}", isNeewDrawResponce);
        return isNeewDrawResponce.Contains(confirmPhrase, StringComparison.OrdinalIgnoreCase);

    }
    private async Task<bool> IsVoiceMessageToBot(long charId, long userId, string text)
    {
        var oldCulture = CultureInfo.CurrentUICulture;
        string confirmPhrase;
        string prompt;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(LanguageCode.English);
            confirmPhrase = _localizer["ConfirmPhrase_Yes"];
            prompt = _localizer["Prompt_IsBotMentioned", text, confirmPhrase];
        }
        finally
        {
            CultureInfo.CurrentUICulture = oldCulture;
        }

        string isNeewDrawResponce = await _chatService.Ask(charId, userId, prompt);
        _logger.LogInformation($"GPT Bot response: {isNeewDrawResponce}");
        return isNeewDrawResponce.Contains(confirmPhrase, StringComparison.OrdinalIgnoreCase);

    }

    internal async Task<string> GetBalanceDisplay(long userId)
    {
        var user = await _telegramUserInfoRepository.Get(p => p.Id == userId);
        if (user == null) return string.Empty;
        
        bool isIgnored = _telegramBotConfiguration.IgnoredBalanceUserIds?.Contains(userId) == true ||
                         _telegramBotConfiguration.OwnerId == userId;
        
        if (isIgnored) return " (Unlimited)";
        
        return $" | B: ${user.Balance.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
