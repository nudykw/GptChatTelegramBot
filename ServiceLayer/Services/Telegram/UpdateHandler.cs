using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceLayer.Constans;
using BotCommand = ServiceLayer.Constans.BotCommand;
using ServiceLayer.Services.AudioTranscriptor;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Microsoft.Extensions.Localization;
using ServiceLayer.Resources;
using ServiceLayer.Constans;
using ServiceLayer.Services.Localization;
using System.CodeDom.Compiler;
using System.Globalization;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using ServiceLayer.Utils;
using Image = SixLabors.ImageSharp.Image;

namespace ServiceLayer.Services.Telegram;
public class UpdateHandler : BaseService, IUpdateHandler
{

    private readonly ITelegramBotClient _botClient;
    private static TgUser? _botInfo;
    private static readonly SemaphoreSlim _botInfoSemaphore = new(1, 1);
    private readonly MessageProcessor.MessageProcessor _messageProcessor;
    private readonly AudioTranscriptorService _audioTranscriptorService;
    private readonly IRepository<TelegramChatInfo>? _telegramChatInfoRepository;
    private readonly IRepository<TelegramUserInfo>? _telegramUserInfoRepository;
    private readonly IRepository<GptBilingItem>? _gptBilingItemRepository;
    private readonly IRepository<BalanceHistory>? _balanceHistoryRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDynamicLocalizer _localizer;
    private readonly IUserContext _userContext;
    private readonly AppSettings _appSettings;
    private readonly IChatService _chatService;
    private static Dictionary<string, string> gptModelsCosts = new Dictionary<string, string>()
    {
        {AiModel.Gpt4Turbo,  "$0.01/$0.03"},
        {AiModel.Gpt4o,  "$0.01/$0.03"},
        {"gpt-4-32k",  "$0.06/$0.12"},
        {"gpt-3.5-turbo-1106",  "$0.001/$0.002"},
        {AiModel.Gpt35Turbo,  "$0.0015/$0.002"},
    };

    public UpdateHandler(IServiceProvider serviceProvider, ILogger<UpdateHandler> logger,
        ITelegramBotClient botClient, MessageProcessor.MessageProcessor messageProcessor,
        AudioTranscriptorService audioTranscriptorService,
        IServiceScopeFactory scopeFactory,
        IDynamicLocalizer localizer,
        IUserContext userContext,
        AppSettings appSettings,
        IChatService chatService)
        : base(serviceProvider, logger)
    {
        _botClient = botClient;
        // _botInfo is now initialized asynchronously in ProcessUpdateInternalAsync
        _messageProcessor = messageProcessor;
        _audioTranscriptorService = audioTranscriptorService;
        _scopeFactory = scopeFactory;
        _localizer = localizer;
        _userContext = userContext;
        _appSettings = appSettings;
        _chatService = chatService;
        _telegramChatInfoRepository = _serviceProvider.GetService<IRepository<TelegramChatInfo>>();
        _telegramUserInfoRepository = _serviceProvider.GetService<IRepository<TelegramUserInfo>>();
        _gptBilingItemRepository = _serviceProvider.GetService<IRepository<GptBilingItem>>();
        _balanceHistoryRepository = _serviceProvider.GetService<IRepository<BalanceHistory>>();
    }

    public Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // We do NOT await this Task.Run, allowing the Telegram polling loop
        // to receive the next update immediately.
        _ = Task.Run(async () =>
        {
            try
            {
                // Create a new scope for EACH update to ensure separate DbContext instances
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<UpdateHandler>();
                await processor.ProcessUpdateInternalAsync(botClient, update, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detailed error processing update {UpdateId}: {Message}", update.Id, ex.Message);
            }
        });

        return Task.CompletedTask;
    }

    private async Task ProcessUpdateInternalAsync(ITelegramBotClient _, Update update, CancellationToken cancellationToken)
    {
        if (_botInfo == null)
        {
            await _botInfoSemaphore.WaitAsync(cancellationToken);
            try
            {
                _botInfo ??= await _botClient.GetMe(cancellationToken);
            }
            finally
            {
                _botInfoSemaphore.Release();
            }
        }

        var user = update switch
        {
            { Message.From: { } f } => f,
            { EditedMessage.From: { } f } => f,
            { CallbackQuery.From: { } f } => f,
            { InlineQuery.From: { } f } => f,
            { ChosenInlineResult.From: { } f } => f,
            _ => null
        };

        if (user != null)
        {
            _userContext.UserId = user.Id;
            var dbUser = await _telegramUserInfoRepository.Get(p => p.Id == user.Id);
            var langCode = dbUser?.LanguageCode ?? user.LanguageCode ?? "en";
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(langCode);
            }
            catch
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(LanguageCode.English);
            }
        }

        try
        {
            var handler = update switch
            {
                { Message: { } message } => BotOnMessageReceived(message, cancellationToken),
                { EditedMessage: { } message } => BotOnMessageReceived(message, cancellationToken),
                { CallbackQuery: { } callbackQuery } => BotOnCallbackQueryReceived(callbackQuery, cancellationToken),
                { InlineQuery: { } inlineQuery } => BotOnInlineQueryReceived(inlineQuery, cancellationToken),
                { ChosenInlineResult: { } chosenInlineResult } => BotOnChosenInlineResultReceived(chosenInlineResult, cancellationToken),
                _ => UnknownUpdateHandlerAsync(update, cancellationToken)
            };

            await handler;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in HandleUpdateAsync for update {UpdateId}", update.Id);
            try
            {
                long? chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
                if (chatId.HasValue)
                {
                    var errorHeader = _localizer["GenericError"];
                    var fullErrorMsg = $"{errorHeader}\n\n{_localizer["Error_Details"]} {ex.Message}";
                    await _botClient.SendMessage(chatId.Value, fullErrorMsg, cancellationToken: cancellationToken);
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to send error message to user");
            }
        }
    }

    private async Task BotOnMessageReceived(TgMessage message, CancellationToken cancellationToken)
    {
        _userContext.UserId = message.From.Id;
        _userContext.ChatId = message.Chat.Id;

        Chat chat = message.Chat;
        TgUser? user = message.From;
        await TrySaveMessageInfoAsync(chat, user);

        _logger.LogInformation("Receive message type: {MessageType}", message.Type);
        
        // Voice messages always need AI if we want to transcribe them
        if (message.Voice != null)
        {
            if (!await CheckBalanceAndReplenish(message.From.Id, message.Chat.Id, cancellationToken))
            {
                return;
            }
        }

        var voiceMessage = await VoiceMessageToText(_botClient, message);
        if (voiceMessage != null)
        {
            message.Text = voiceMessage;
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                replyParameters: new ReplyParameters() { MessageId = message.MessageId },
                text: _localizer["YouSaid", voiceMessage],
                cancellationToken: cancellationToken);
        }

        if (message.Text is not { } && message.Caption is not { } && message.Photo == null && message.Document == null)
            return;
        
        var messageText = message.Text ?? message.Caption ?? string.Empty;
        _logger.LogInformation("Text: {MessageType}", messageText);

        // Check if message is a reply to language prompt
        if (message.ReplyToMessage != null && 
            (message.ReplyToMessage.Text?.Contains(_localizer["EnterLanguagePrompt"]) == true))
        {
            await ProcessLanguageCommand(_botClient, message, string.Empty, cancellationToken);
            return;
        }

        if (!IsMe(message))
        {
            _logger.LogInformation("This message not for me.");
            return;
        }

        var actionText = messageText.Split(' ')[0].Replace($"@{_botInfo.Username}", string.Empty);
        var command = BotCommand.FromString(actionText);

        if (command != null)
        {
            var userScopes = await GetUserScopesAsync(user, chat);
            if ((command.RequiredScope & userScopes) == 0)
            {
                _logger.LogWarning("User {UserId} tried to execute restricted command {Command} in chat {ChatId}", user.Id, actionText, chat.Id);
                // Return null or send a message if in private chat
                if (chat.Type == ChatType.Private)
                {
                    await _botClient.SendMessage(chat.Id, _localizer["PermissionDenied"], cancellationToken: cancellationToken);
                }
                return;
            }
        }

        // Identify if this is an AI action that costs balance
        bool isAiAction = (command?.Value == BotCommand.Draw) || 
                         (message.Photo != null) || 
                         (message.Document != null && message.Document.MimeType.Contains("image")) ||
                         (command == null && !new[] { "/keyboard", "/remove", "/photo", "/request", "/inline_mode", "/throw", "/help" }.Contains(actionText));

        if (isAiAction)
        {
            if (!await CheckBalanceAndReplenish(message.From.Id, message.Chat.Id, cancellationToken))
            {
                return;
            }
        }

        var action = command?.Value switch
        {
            var v when v == BotCommand.Billing => SendBillingInlineKeyboard(_botClient, message, cancellationToken),
            var v when v == BotCommand.Model => SendInlineKeyboard(_botClient, message, cancellationToken),
            var v when v == BotCommand.Provider => SendProviderInlineKeyboard(_botClient, message, cancellationToken),
            var v when v == BotCommand.Lang => ProcessLanguageCommand(_botClient, message, actionText, cancellationToken),
            var v when v == BotCommand.Draw => _messageProcessor.ProcessDrawCommand(message.Chat.Id, message.MessageId, message.From.Id, messageText.Replace(actionText, string.Empty).Trim(), cancellationToken),
            var v when v == BotCommand.Help => Usage(_botClient, message, cancellationToken),
            var v when v == BotCommand.Restart => FailingHandler(_botClient, message, cancellationToken),
            var v when v == BotCommand.UsersBalance => ShowUsersBalance(_botClient, message, cancellationToken),
            var v when v == BotCommand.SetBalance => SetUserBalance(_botClient, message, cancellationToken),
            _ => actionText switch
            {
                "/keyboard" => SendReplyKeyboard(_botClient, message, cancellationToken),
                "/remove" => RemoveKeyboard(_botClient, message, cancellationToken),
                "/photo" => SendFile(_botClient, message, cancellationToken),
                "/request" => RequestContactAndLocation(_botClient, message, cancellationToken),
                "/inline_mode" => StartInlineQuery(_botClient, message, cancellationToken),
                "/throw" => FailingHandler(_botClient, message, cancellationToken),
                "/help" => Usage(_botClient, message, cancellationToken),
                _ => ProcessMessage(_botClient, message, cancellationToken)
            }
        };
        TgMessage sentMessage = await action;
        if (sentMessage != null)
        {
            _logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.MessageId);
        }

        // Send inline keyboard 
        // You can process responses in BotOnCallbackQueryReceived handler
        async Task<TgMessage> SendInlineKeyboard(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            await botClient.SendChatAction(
                chatId: message.Chat.Id,
                action: ChatAction.Typing,
                cancellationToken: cancellationToken);
            IReadOnlyList<OpenAI.Models.Model> gptModels = await ActionWithShowTypeng(message.Chat.Id, cancellationToken,
                _messageProcessor.GetGPTModels(message.From.Id));

            var choises = new TgKeyboardGrid();
            var choisesRow = new TgKeyboardRow();
            int index = 0;
            foreach (OpenAI.Models.Model? model in gptModels.OrderBy(p => p.Id).ToList())
            {
                var data = $"{BotCommand.Model}:{model.Id}";
                var choise = InlineKeyboardButton.WithCallbackData($"{model.Id} ({model.CreatedAt.ToShortDateString()})",
                    data);
                if (gptModelsCosts.TryGetValue(model.Id, out var costs))
                {
                    choise = InlineKeyboardButton.WithCallbackData($"{model.Id} ({model.CreatedAt.ToShortDateString()}. {costs})",
                        data);
                }
                //if (index == 0 || (index % 2) == 0)
                {
                    if (choisesRow.Any())
                    {
                        choises.Add(choisesRow);
                    }
                    choisesRow = new TgKeyboardRow();
                }
                choisesRow.Add(choise);
                index++;
            }
            if (choisesRow.Any())
            {
                choises.Add(choisesRow);
            }

            InlineKeyboardMarkup replyMarkup = new(choises);
            var result = await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["SelectModel"],
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
            return result;

        }
        async Task<TgMessage> SendBillingInlineKeyboard(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            await botClient.SendChatAction(
                chatId: message.Chat.Id,
                action: ChatAction.Typing,
                cancellationToken: cancellationToken);
            var dates = _gptBilingItemRepository
            .GetAll()
                .Select(p => string.Concat(p.CreationDate.Year, "-", p.CreationDate.Month))
                .Distinct()
                .ToList();
            var choises = new TgKeyboardGrid();
            var choisesRow = new TgKeyboardRow();
            int index = 0;
            foreach (string date in dates.OrderBy(p => p).ToList())
            {
                var data = $"{BotCommand.Billing}:{date}";
                var choise = InlineKeyboardButton.WithCallbackData(date, data);
                if (index == 0 || (index % 2) == 0)
                {
                    if (choisesRow.Any())
                    {
                        choises.Add(choisesRow);
                    }
                    choisesRow = new TgKeyboardRow();
                }
                choisesRow.Add(choise);
                index++;
            }
            if (choisesRow.Any())
            {
                choises.Add(choisesRow);
            }

            InlineKeyboardMarkup replyMarkup = new(choises);
            TgMessage result = await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["SelectBillingDate"],
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
            return result;
        }

        async Task<TgMessage> ProcessLanguageCommand(ITelegramBotClient botClient, TgMessage message, string actionText, CancellationToken cancellationToken)
        {
            var messageText = message.Text ?? message.Caption ?? string.Empty;
            var argument = string.IsNullOrEmpty(actionText) 
                ? messageText 
                : messageText.Replace(actionText, string.Empty).Trim();

            if (string.IsNullOrEmpty(argument))
            {
                return await SendLanguageInlineKeyboard(botClient, message, cancellationToken);
            }

            await botClient.SendChatAction(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);
            var langCode = await _messageProcessor.IdentifyLanguage(message.Chat.Id, message.From.Id, argument);

            if (langCode == LanguageCode.English || langCode == LanguageCode.Ukrainian)
            {
                // Native language, update immediately
                var dbUser = await _telegramUserInfoRepository.Get(p => p.Id == message.From.Id);
                if (dbUser != null)
                {
                    dbUser.LanguageCode = langCode;
                    _telegramUserInfoRepository.Update(dbUser);
                    await _telegramUserInfoRepository.SaveChanges();
                }

                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(langCode);
                }
                catch
                {
                    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(LanguageCode.English);
                }
                
                string nativeName = langCode;
                
                var oldCulture = CultureInfo.CurrentUICulture;
                string englishText;
                try {
                    CultureInfo.CurrentUICulture = new CultureInfo(LanguageCode.English);
                    englishText = _localizer["LanguageChanged", nativeName];
                } finally {
                    CultureInfo.CurrentUICulture = oldCulture;
                }
                
                var translatedText = _localizer["LanguageChanged", nativeName];
                var confirmation = (langCode == LanguageCode.English) ? translatedText : $"{englishText} {translatedText}";
                
                return await botClient.SendMessage(message.Chat.Id, confirmation, cancellationToken: cancellationToken);
            }

            // Non-native language, ask for confirmation
            var confirmKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(_localizer["Confirm"], $"{BotCommand.Lang}:confirm:{langCode}"),
                    InlineKeyboardButton.WithCallbackData(_localizer["Cancel"], $"{BotCommand.Lang}:cancel")
                }
            });

            var warning = _localizer["DynamicLanguageWarning", argument, langCode];
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: warning,
                replyMarkup: confirmKeyboard,
                cancellationToken: cancellationToken);
        }

        async Task<TgMessage> SendLanguageInlineKeyboard(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            await botClient.SendChatAction(
                chatId: message.Chat.Id,
                action: ChatAction.Typing,
                cancellationToken: cancellationToken);

            var choises = new TgKeyboardGrid
            {
                new TgKeyboardRow
                {
                    InlineKeyboardButton.WithCallbackData("🇬🇧 English", $"{BotCommand.Lang}:{LanguageCode.English}"),
                    InlineKeyboardButton.WithCallbackData("🇺🇦 Українська", $"{BotCommand.Lang}:{LanguageCode.Ukrainian}")
                },
                new TgKeyboardRow
                {
                     InlineKeyboardButton.WithCallbackData("🌍 " + _localizer["OtherLanguage"], $"{BotCommand.Lang}:prompt")
                }
            };

            InlineKeyboardMarkup replyMarkup = new(choises);
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["SelectLanguage"],
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }

        async Task<TgMessage> SendProviderInlineKeyboard(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            await botClient.SendChatAction(
                chatId: message.Chat.Id,
                action: ChatAction.Typing,
                cancellationToken: cancellationToken);

            var choises = new TgKeyboardGrid();
            
            foreach (ChatStrategy strategy in Enum.GetValues(typeof(ChatStrategy)))
            {
                string text = strategy switch
                {
                    ChatStrategy.Auto => _localizer["AutoRotation"],
                    ChatStrategy.OpenAI => AiProvider.OpenAI.DisplayName,
                    ChatStrategy.Gemini => AiProvider.Gemini.DisplayName,
                    ChatStrategy.DeepSeek => AiProvider.DeepSeek.DisplayName,
                    ChatStrategy.Grok => AiProvider.Grok.DisplayName,
                    _ => strategy.ToString()
                };
                choises.Add(new TgKeyboardRow { InlineKeyboardButton.WithCallbackData(text, $"{BotCommand.Provider}:{(int)strategy}") });
            }

            InlineKeyboardMarkup replyMarkup = new(choises);
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["SelectProvider"] + "\n\n" + _localizer["SelectProviderHelp"],
                replyMarkup: replyMarkup,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }

        async Task<TgMessage> SendReplyKeyboard(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            ReplyKeyboardMarkup replyKeyboardMarkup = new(
                new[]
                {
                        new KeyboardButton[] { "1.1", "1.2" },
                        new KeyboardButton[] { "2.1", "2.2" },
                })
            {
                ResizeKeyboard = true
            };

            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["Choose"],
                replyMarkup: replyKeyboardMarkup,
                cancellationToken: cancellationToken);
        }

        async Task<TgMessage> RemoveKeyboard(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["RemovingKeyboard"],
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        }

        async Task<TgMessage> SendFile(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            await botClient.SendChatAction(
                message.Chat.Id,
                ChatAction.UploadPhoto,
                cancellationToken: cancellationToken);

            const string filePath = "Files/tux.png";
            await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileName = filePath.Split(Path.DirectorySeparatorChar).Last();

            return await botClient.SendPhoto(
                chatId: message.Chat.Id,
                photo: new InputFileStream(fileStream, fileName),
                caption: _localizer["NicePicture"],
                cancellationToken: cancellationToken);
        }

        async Task<TgMessage> RequestContactAndLocation(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            ReplyKeyboardMarkup RequestReplyKeyboard = new(
                new[]
                {
                    KeyboardButton.WithRequestLocation(_localizer["Location"]),
                    KeyboardButton.WithRequestContact(_localizer["Contact"]),
                });

            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["WhoWhereAreYou"],
                replyMarkup: RequestReplyKeyboard,
                cancellationToken: cancellationToken);
        }

        async Task<TgMessage> Usage(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            var userScopes = await GetUserScopesAsync(message.From, message.Chat);
            var availableCommands = BotCommand.GetAll()
                .Where(cmd => (cmd.RequiredScope & userScopes) != 0)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine(_localizer["HelpHeader"]);
            sb.AppendLine();

            var dbUser = await _telegramUserInfoRepository.Get(p => p.Id == message.From.Id);

            foreach (var cmd in availableCommands)
            {
                var descriptionKey = "Desc_" + cmd.Value.TrimStart('/');
                var line = $"{cmd.Value} - {_localizer[descriptionKey]}";
                
                string? currentValue = null;
                if (cmd.Value == BotCommand.Model.Value)
                {
                    currentValue = await _chatService.GetSelectedModel(message.From.Id);
                }
                else if (cmd.Value == BotCommand.Provider.Value)
                {
                    var provider = dbUser?.PreferredProvider ?? ChatStrategy.Auto;
                    currentValue = provider == ChatStrategy.Auto 
                        ? _localizer["AutoRotation"] 
                        : provider.ToString();
                }
                else if (cmd.Value == BotCommand.Lang.Value)
                {
                    currentValue = dbUser?.LanguageCode ?? message.From.LanguageCode;
                }

                if (!string.IsNullOrEmpty(currentValue))
                {
                    line += _localizer["CurrentValue", currentValue];
                }

                sb.AppendLine(line);
            }

            sb.AppendLine();
            sb.AppendLine(_localizer["GroupContextInfo"]);

            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: sb.ToString(),
                parseMode: ParseMode.Markdown,
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        }
        async Task<TgMessage> ProcessMessage(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            string messageText = message.Text;
            if (message.Voice != null)
            {
                var voice = message.Voice;
                message.Voice = null;
                messageText = $"{(IsMe(message) ? MessageMarkers.VoiceMessageToBot : MessageMarkers.VoiceMessage)}{messageText}";
                message.Voice = voice;
            }
            if (message.Document != null && message.Document.MimeType.Contains("image"))
            {
                var file = await botClient.GetFile(message.Document.FileId);
                using (TempFileCollection tempFiles = new TempFileCollection())
                {
                    string temFileName = tempFiles.AddExtension(Path.GetExtension(message.Document.FileName));
                    using (var tempFileStream = new FileStream(temFileName, FileMode.Create))
                    {
                        await botClient.DownloadFile(temFileName, tempFileStream);
                        tempFileStream.Close();
                    }
                    await ActionWithShowTypeng(message.Chat.Id, cancellationToken, _messageProcessor.ProcessImage(message.Chat.Id,
                            message.MessageId, message.ReplyToMessage?.MessageId, (long)(message.From?.Id),
                            message.Caption, temFileName, cancellationToken));
                }

                var filePath = file.FilePath;
                //var fileStream = System.IO.File.OpenRead(filePath);
                return null;
            }
            if (message.Photo != null)
            {
                var photo = message.Photo.Last(p => p.FileSize < 4 * 1024000);
                var file = await botClient.GetFile(photo.FileId);
                using (TempFileCollection tempFiles = new TempFileCollection())
                {
                    string temFileName = tempFiles.AddExtension("png");
                    try
                    {
                        //using (var tempFileStream = new FileStream(temFileName, FileMode.Create))
                        {
                            using (var telegramImageStream = new MemoryStream())
                            {
                                await botClient.DownloadFile(file.FilePath, telegramImageStream);
                                telegramImageStream.Flush();
                                telegramImageStream.Seek(0, SeekOrigin.Begin);
                                using (var image = Image.Load<Rgba32>(telegramImageStream))
                                {
                                    image.Save(temFileName);
                                }
                            }
                            //tempFileStream.Close();
                        }
                        await ActionWithShowTypeng(message.Chat.Id, cancellationToken, _messageProcessor.ProcessImage(message.Chat.Id,
                                message.MessageId, message.ReplyToMessage?.MessageId, (long)(message.From?.Id),
                                message.Caption, temFileName, cancellationToken)
                        );
                    }
                    finally
                    {
                        System.IO.File.Delete(temFileName);
                    }
                }

                var filePath = file.FilePath;
                //var fileStream = System.IO.File.OpenRead(filePath);
                return null;
            }
            string responce = await ActionWithShowTypeng(message.Chat.Id, cancellationToken, _messageProcessor.ProcessMessage(message.Chat.Id,
                    message.MessageId, message.ReplyToMessage?.MessageId, message.From.Id,
                    messageText,
                    cancellationToken));
            return null;
            /*
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: responce,
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
            */
        }
        async Task<string> VoiceMessageToText(ITelegramBotClient botClient,
            TgMessage message)
        {
            if (message.Voice != null)
            {
                Voice voiceMessage = message.Voice;
                var file = await botClient.GetFile(voiceMessage.FileId);
                var voiceFilePath = $"{voiceMessage.FileId}.ogg";

                using (var saveVoiceStream = new FileStream(voiceFilePath, FileMode.Create))
                {
                    await botClient.DownloadFile(file.FilePath, saveVoiceStream);
                    saveVoiceStream.Seek(0, SeekOrigin.Begin);
                    var messageText = await _audioTranscriptorService
                        .AudioTranscription(message.Chat.Id, message.From.Id, saveVoiceStream, voiceFilePath);
                    return messageText;
                }
            }
            return null;
        }
        async Task<TgMessage> StartInlineQuery(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            InlineKeyboardMarkup inlineKeyboard = new(
                InlineKeyboardButton.WithSwitchInlineQueryCurrentChat(_localizer["InlineMode"]));

            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["StartInlineQuery"],
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }

#pragma warning disable RCS1163 // Unused parameter.
#pragma warning disable IDE0060 // Remove unused parameter
        static Task<TgMessage> FailingHandler(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
        {
            Environment.Exit(1);
            throw new IndexOutOfRangeException();
        }
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore RCS1163 // Unused parameter.
    }

    private async Task<TgMessage> ShowUsersBalance(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
    {
        await botClient.SendChatAction(
            chatId: message.Chat.Id,
            action: ChatAction.Typing,
            cancellationToken: cancellationToken);

        var users = _telegramUserInfoRepository.GetAll()
            .OrderBy(u => u.Id)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(_localizer["UsersBalanceHeader"]);
        sb.AppendLine();

        foreach (var u in users)
        {
            var timeAgo = FormatTimeAgo(u.BalanceModifiedAt);
            // UsersBalanceItem format: {0} ({1}): {2} (last changed: {3})
            sb.AppendLine(_localizer["UsersBalanceItem", 
                u.FirstName + (string.IsNullOrEmpty(u.LastName) ? "" : " " + u.LastName), 
                u.Id, 
                u.Balance.ToString("0.######"), 
                timeAgo]);
        }

        return await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: sb.ToString(),
            cancellationToken: cancellationToken);
    }

    private async Task<TgMessage> SetUserBalance(ITelegramBotClient botClient, TgMessage message, CancellationToken cancellationToken)
    {
        await botClient.SendChatAction(
            chatId: message.Chat.Id,
            action: ChatAction.Typing,
            cancellationToken: cancellationToken);

        var text = message.Text ?? string.Empty;
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3)
        {
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["SetBalance_Usage"],
                cancellationToken: cancellationToken);
        }

        bool userIdParsed = long.TryParse(parts[1], out long targetUserId);
        bool amountParsed = decimal.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal amount)
                           || decimal.TryParse(parts[2], out amount);

        if (!userIdParsed || !amountParsed)
        {
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["SetBalance_InvalidFormat"],
                cancellationToken: cancellationToken);
        }

        var dbUser = await _telegramUserInfoRepository.Get(u => u.Id == targetUserId);
        if (dbUser == null)
        {
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: _localizer["SetBalance_UserNotFound", targetUserId],
                cancellationToken: cancellationToken);
        }

        var oldBalance = dbUser.Balance;
        dbUser.Balance = amount;
        dbUser.BalanceModifiedAt = DateTime.UtcNow;
        _telegramUserInfoRepository.Update(dbUser);

        if (_balanceHistoryRepository != null)
        {
            var historyRecord = new BalanceHistory
            {
                UserId = targetUserId,
                Amount = amount - oldBalance, // delta
                ModifiedById = message.From?.Id,
                CreatedAt = DateTime.UtcNow,
                Source = "Admin"
            };
            _balanceHistoryRepository.Add(historyRecord);
            await _balanceHistoryRepository.SaveChanges();
        }

        await _telegramUserInfoRepository.SaveChanges();

        return await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: _localizer["SetBalance_Success", 
                dbUser.FirstName + (string.IsNullOrEmpty(dbUser.LastName) ? "" : " " + dbUser.LastName), 
                dbUser.Id, 
                dbUser.Balance.ToString("0.######")],
            cancellationToken: cancellationToken);
    }

    private string FormatTimeAgo(DateTime? dateTime)
    {
        if (!dateTime.HasValue) return _localizer["TimeAgo_Never"];
        
        var diff = DateTime.UtcNow - dateTime.Value;
        if (diff.TotalSeconds < 60) return _localizer["TimeAgo_JustNow"];
        if (diff.TotalMinutes < 60) return _localizer["TimeAgo_Minutes", (int)diff.TotalMinutes];
        if (diff.TotalHours < 24) return _localizer["TimeAgo_Hours", (int)diff.TotalHours];
        return _localizer["TimeAgo_Days", (int)diff.TotalDays];
    }

    private async Task TrySaveMessageInfoAsync(Chat chat, TgUser? user)
    {
        TelegramChatInfo? dbChat = await _telegramChatInfoRepository.Get(p => p.Id == chat.Id);
        if (dbChat == null)
        {
            dbChat = new TelegramChatInfo
            {
                Id = chat.Id,
                ChatType = chat.Type.ToString(),
                Description = null,
                InviteLink = null,
                Title = chat.Title,
                Username = chat.Username
            };
            _telegramChatInfoRepository.Add(dbChat);
        }
        if (user != null)
        {
            TelegramUserInfo? dbUser = await _telegramUserInfoRepository.Get(p => p.Id == user.Id);
            if (dbUser == null)
            {
                dbUser = new TelegramUserInfo
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    IsBot = user.IsBot,
                    IsPremium = user.IsPremium,
                    LastName = user.LastName,
                    Username = user.Username,
                    LanguageCode = user.LanguageCode,
                    Balance = _appSettings.TelegramBotConfiguration.InitialBalance,
                    BalanceModifiedAt = DateTime.UtcNow
                };
                _telegramUserInfoRepository.Add(dbUser);
            }
            else
            {
                dbUser.FirstName = user.FirstName;
                dbUser.IsBot = user.IsBot;
                dbUser.IsPremium = user.IsPremium;
                dbUser.LastName = user.LastName;
                dbUser.Username = user.Username;
                // Only update LanguageCode if it's not set yet to respect manual override
                if (string.IsNullOrEmpty(dbUser.LanguageCode))
                {
                    dbUser.LanguageCode = user.LanguageCode;
                }
                _telegramUserInfoRepository.Update(dbUser);
            }
            await _telegramUserInfoRepository.SaveChanges();
        }
    }

    private bool IsMe(Message message)
    {
        if (message == null)
        {
            return false;
        }
        if (message.Voice != null)
        {
            _logger.LogInformation("It's voice message");
            return true;
        }
        if (message.Chat.Type == ChatType.Private)
        {
            _logger.LogInformation("It's private message");
            return true;
        }
        var messageText = message.Text ?? message.Caption;
        if (messageText != null && messageText.Contains($"@{_botInfo.Username}", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Message for this bot");
            return true;
        }
        if (message.ReplyToMessage?.From?.Id == _botInfo.Id)
        {
            _logger.LogInformation("Reply to this bot message");
            return true;
        }
        return false;
    }

    // Process Inline Keyboard callback data
    private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        _userContext.UserId = callbackQuery.From.Id;
        _userContext.ChatId = callbackQuery.Message?.Chat.Id ?? 0;

        _logger.LogInformation("Received inline keyboard callback from: {CallbackQueryId}", callbackQuery.Id);
        var dataSet = callbackQuery?.Data?.Split(':');
        if (dataSet == null || dataSet.Length < 2)
        {
            return;
        }
        var originalMessageType = dataSet[0];
        var data = string.Join(":", dataSet.Skip(1));
        if (originalMessageType == BotCommand.Model)
        {
            var (isSuckes, errorMessage) = await _messageProcessor.SelectGPTModel(data, callbackQuery.From.Id);
            if (!isSuckes)
            {
                var errorText = _localizer["ModelSelectionError", data, errorMessage ?? string.Empty];
                await _botClient.AnswerCallbackQuery(
                    callbackQueryId: callbackQuery.Id,
                    text: errorText,
                    cancellationToken: cancellationToken);

                await _botClient.SendMessage(
                    chatId: callbackQuery.Message!.Chat.Id,
                    text: errorText,
                    cancellationToken: cancellationToken);
                return;
            }
            await _botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: _localizer["ModelSelected", data],
                cancellationToken: cancellationToken);

            await _botClient.SendMessage(
                chatId: callbackQuery.Message!.Chat.Id,
                text: _localizer["ModelSelected", data],
                cancellationToken: cancellationToken);
            return;
        }
        if (originalMessageType == BotCommand.Provider)
        {
            if (Enum.TryParse<ChatStrategy>(data, out var strategy))
            {
                await _messageProcessor.SetPreferredProvider(callbackQuery.From.Id, strategy);
                
                string strategyName = strategy switch
                {
                    ChatStrategy.Auto => _localizer["AutoRotation"],
                    ChatStrategy.OpenAI => AiProvider.OpenAI.DisplayName,
                    ChatStrategy.Gemini => AiProvider.Gemini.DisplayName,
                    ChatStrategy.DeepSeek => AiProvider.DeepSeek.DisplayName,
                    ChatStrategy.Grok => AiProvider.Grok.DisplayName,
                    _ => strategy.ToString()
                };

                string responseText = _localizer["ProviderSelected", strategyName];

                await _botClient.AnswerCallbackQuery(
                    callbackQueryId: callbackQuery.Id,
                    text: responseText,
                    cancellationToken: cancellationToken);

                await _botClient.SendMessage(
                    chatId: callbackQuery.Message!.Chat.Id,
                    text: responseText,
                    cancellationToken: cancellationToken);
            }
            return;
        }
        if (originalMessageType == BotCommand.Lang)
        {
            if (data == "cancel")
            {
                await _botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                await _botClient.DeleteMessage(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
                return;
            }

            if (data == "prompt")
            {
                await _botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
                await _botClient.SendMessage(
                    chatId: callbackQuery.Message!.Chat.Id,
                    text: _localizer["EnterLanguagePrompt"],
                    replyMarkup: new ForceReplyMarkup { Selective = true },
                    cancellationToken: cancellationToken);
                return;
            }

            string langCode = data;
            if (data.StartsWith("confirm:"))
            {
                langCode = data.Replace("confirm:", string.Empty);
                // Here we could add a billing record for "Language Setup" if we wanted, 
                // but for now we'll just set the language and the localizer will bill for subsequent translations.
            }

            var dbUser = await _telegramUserInfoRepository.Get(p => p.Id == callbackQuery.From.Id);
            if (dbUser != null)
            {
                dbUser.LanguageCode = langCode;
                _telegramUserInfoRepository.Update(dbUser);
                await _telegramUserInfoRepository.SaveChanges();
            }

            // Update current culture for the immediate response
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(langCode);
            }
            catch
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(LanguageCode.English);
            }

            string nativeName = langCode;

            var oldCulture = CultureInfo.CurrentUICulture;
            string englishText;
            try {
                CultureInfo.CurrentUICulture = new CultureInfo(LanguageCode.English);
                englishText = _localizer["LanguageChanged", nativeName];
            } finally {
                CultureInfo.CurrentUICulture = oldCulture;
            }
            
            var translatedText = _localizer["LanguageChanged", nativeName];
            var confirmation = (langCode == LanguageCode.English) ? translatedText : $"{englishText} {translatedText}";

            await _botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: confirmation,
                cancellationToken: cancellationToken);

            await _botClient.SendMessage(
                chatId: callbackQuery.Message!.Chat.Id,
                text: confirmation,
                cancellationToken: cancellationToken);
            
            if (data.StartsWith("confirm:"))
            {
                await _botClient.DeleteMessage(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, cancellationToken);
            }
            return;
        }
        if (originalMessageType == BotCommand.Billing)
        {
            var strData = data.Split('-');
            var startData = new DateTime(int.Parse(strData[0]), int.Parse(strData[1]), 1);
            var endData = startData.AddMonths(1);

            var billingUsersIds = new List<long>() { callbackQuery.Message.From.Id };
            // ActiveUsernames properties no longer exist on Chat
            var bilingByUsers = _gptBilingItemRepository.GetAll()
                .AsNoTracking()
                .Include(p => p.TelegramUserInfo)
                .Include(p => p.TelegramChatInfo)
                //.Where(p => billingUsersIds.Contains(p.TelegramUserInfoId))
                .Where(p => p.CreationDate >= startData && p.CreationDate < endData)
                .GroupBy(p => new { p.TelegramUserInfoId, p.TelegramChatInfoId, p.ModelName });
            var sb = new StringBuilder();
            sb.AppendLine(_localizer["BillingHeader"]);
            foreach (var b in bilingByUsers)
            {
                var f = b.First();
                var user = f.TelegramUserInfo;
                var chat = f.TelegramChatInfo;
                var model = f.ModelName;

                var totalTokens = b.Sum(p => p.TotalTokens);
                var promptTokens = b.Sum(p => p.PromptTokens);
                var completionTokens = b.Sum(p => p.CompletionTokens);
                var cost = b.Sum(p => p.Cost);
                sb.AppendLine($"{user.FirstName} {user.LastName}({string.Concat(chat.Description, chat.Title)}) '{model}': {promptTokens} / {completionTokens} / {totalTokens} / ${cost}");
            }
            var tottalCostsByUser = _gptBilingItemRepository.GetAll()
                .AsNoTracking()
                .Include(p => p.TelegramUserInfo)
                .Where(p => p.CreationDate >= startData && p.CreationDate < endData)
                .GroupBy(p => p.TelegramUserInfo);
            sb.AppendLine();
            foreach (var user in tottalCostsByUser)
            {
                var cost = user.Sum(p => p.Cost);
                sb.AppendLine($"{user.Key.FirstName} {user.Key.LastName}: ${cost}");
            }
            sb.AppendLine();
            sb.AppendLine(_localizer["DonateTo"] + " 4731 1856 1625 8247");
            await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: _localizer["Billing"],
                cancellationToken: cancellationToken);

            await _botClient.SendMessage(
                chatId: callbackQuery.Message!.Chat.Id,
                text: sb.ToString(),
                cancellationToken: cancellationToken);
        }
    }

    #region Inline Mode

    private async Task BotOnInlineQueryReceived(InlineQuery inlineQuery, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received inline query from: {InlineQueryFromId}", inlineQuery.From.Id);

        InlineQueryResult[] results = {
            // displayed result
            new InlineQueryResultArticle(
                id: "1",
                title: "TgBots",
                inputMessageContent: new InputTextMessageContent("hello"))
        };

        await _botClient.AnswerInlineQuery(
            inlineQueryId: inlineQuery.Id,
            results: results,
            cacheTime: 0,
            isPersonal: true,
            cancellationToken: cancellationToken);
    }

    private async Task BotOnChosenInlineResultReceived(ChosenInlineResult chosenInlineResult, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received inline result: {ChosenInlineResultId}", chosenInlineResult.ResultId);

        await _botClient.SendMessage(
            chatId: chosenInlineResult.From.Id,
            text: _localizer["YouChoseResult", chosenInlineResult.ResultId],
            cancellationToken: cancellationToken);
    }

    #endregion

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable RCS1163 // Unused parameter.
    private Task UnknownUpdateHandlerAsync(Update update, CancellationToken cancellationToken)
#pragma warning restore RCS1163 // Unused parameter.
#pragma warning restore IDE0060 // Remove unused parameter
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }

    public async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"{_localizer["TelegramApiError"]}\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogInformation("HandleError: {ErrorMessage}", ErrorMessage);

        // Cooldown in case of network connection error
        if (exception is RequestException)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }

    private async Task<T> ActionWithShowTypeng<T>(ChatId chatId, CancellationToken cancellationToken, Task<T> action)

    {
        try
        {
            // Send initial typing action
            await _botClient.SendChatAction(chatId: chatId, action: ChatAction.Typing, cancellationToken: cancellationToken);

            // While the main action is running, periodically refresh the typing indicator
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!action.IsCompleted && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(4000, cancellationToken);
                        if (!action.IsCompleted)
                        {
                            await _botClient.SendChatAction(chatId: chatId, action: ChatAction.Typing, cancellationToken: cancellationToken);
                        }
                    }
                }
                catch { /* Ignore typing errors */ }
            }, cancellationToken);

            return await action;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ActionWithShowTypeng for chat {ChatId}", chatId);
            
            string errorMessage = ex is AggregateException agg ? agg.Flatten().Message : ex.Message;
            await _botClient.SendMessage(
                chatId: chatId,
                text: _localizer["ErrorOccurred", errorMessage],
                cancellationToken: cancellationToken);
                
            throw; 
        }
    }

    private async Task<ServiceLayer.Constans.BotCommandScope> GetUserScopesAsync(TgUser? user, Chat chat)
    {
        var scope = ServiceLayer.Constans.BotCommandScope.Default;

        if (user == null) return scope;

        // Check private/group
        if (chat.Type == ChatType.Private)
            scope |= ServiceLayer.Constans.BotCommandScope.AllPrivateChats;
        else
            scope |= ServiceLayer.Constans.BotCommandScope.AllGroupChats;

        // Check owner
        if (_appSettings.TelegramBotConfiguration.OwnerId.HasValue && user.Id == _appSettings.TelegramBotConfiguration.OwnerId.Value)
        {
            scope |= ServiceLayer.Constans.BotCommandScope.Owner;
        }

        // Check group admins
        if (chat.Type != ChatType.Private)
        {
            try
            {
                var member = await _botClient.GetChatMember(chat.Id, user.Id);
                if (member.Status == ChatMemberStatus.Administrator || member.Status == ChatMemberStatus.Creator)
                {
                    scope |= ServiceLayer.Constans.BotCommandScope.AllChatAdmins;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get chat member status for user {UserId} in chat {ChatId}", user.Id, chat.Id);
            }
        }

        return scope;
    }

    internal async Task<bool> CheckBalanceAndReplenish(long userId, long chatId, CancellationToken cancellationToken)
    {
        var config = _appSettings.TelegramBotConfiguration;
        if ((config.IgnoredBalanceUserIds != null && config.IgnoredBalanceUserIds.Contains(userId)) || 
            config.OwnerId == userId || 
            config.InitialBalance == 0)
        {
            return true;
        }

        var dbUser = await _telegramUserInfoRepository.Get(p => p.Id == userId);
        if (dbUser == null) return true; // Should not happen

        if (dbUser.Balance > 0)
        {
            return true;
        }

        // Check for 24h replenishment
        if (dbUser.LastAiInteraction.HasValue && (DateTime.UtcNow - dbUser.LastAiInteraction.Value).TotalHours >= 24)
        {
            dbUser.Balance = config.InitialBalance;
            dbUser.BalanceModifiedAt = DateTime.UtcNow;
            _telegramUserInfoRepository.Update(dbUser);
            await _telegramUserInfoRepository.SaveChanges();
            return true;
        }
        else if (!dbUser.LastAiInteraction.HasValue)
        {
             // First interaction case if migration didn't set it
             dbUser.Balance = config.InitialBalance;
             dbUser.BalanceModifiedAt = DateTime.UtcNow;
             _telegramUserInfoRepository.Update(dbUser);
             await _telegramUserInfoRepository.SaveChanges();
             return true;
        }

        // Inform user about insufficient credits
        var message = $"{_localizer["InsufficientCredits"]}\n\n{_localizer["TryAgainLater"]}";
        await _botClient.SendMessage(chatId, message, cancellationToken: cancellationToken);
        return false;
    }
}