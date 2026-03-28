using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceLayer.Constans;
using ServiceLayer.Services.AudioTranscriptor;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.CodeDom.Compiler;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using Image = SixLabors.ImageSharp.Image;

namespace ServiceLayer.Services.Telegram;
public class UpdateHandler : BaseService, IUpdateHandler
{

    private readonly ITelegramBotClient _botClient;
    private readonly User _botInfo;
    private readonly MessageProcessor.MessageProcessor _messageProcessor;
    private readonly AudioTranscriptorService _audioTranscriptorService;
    private readonly IRepository<TelegramChatInfo>? _telegramChatInfoRepository;
    private readonly IRepository<TelegramUserInfo>? _telegramUserInfoRepository;
    private readonly IRepository<GptBilingItem>? _gptBilingItemRepository;
    private static Dictionary<string, string> gptModelsCosts = new Dictionary<string, string>()
    {
        {"gpt-4-1106-preview",  "$0.01/$0.03"},
        {"gpt-4-1106-vision-preview",  "$0.01/$0.03"},
        {"gpt-4",  "$0.03/$0.06"},
        {"gpt-4-32k",  "$0.06/$0.12"},
        {"gpt-3.5-turbo-1106",  "$0.001/$0.002"},
        {"gpt-3.5-turbo-instruct",  "$0.0015/$0.002"},
    };

    public UpdateHandler(IServiceProvider serviceProvider, ILogger<UpdateHandler> logger,
        ITelegramBotClient botClient, MessageProcessor.MessageProcessor messageProcessor,
        AudioTranscriptorService audioTranscriptorService)
        : base(serviceProvider, logger)
    {
        _botClient = botClient;
        _botInfo = botClient.GetMe().Result;
        _messageProcessor = messageProcessor;
        _audioTranscriptorService = audioTranscriptorService;
        _telegramChatInfoRepository = _serviceProvider.GetService<IRepository<TelegramChatInfo>>();
        _telegramUserInfoRepository = _serviceProvider.GetService<IRepository<TelegramUserInfo>>();
        _gptBilingItemRepository = _serviceProvider.GetService<IRepository<GptBilingItem>>();
    }

    public async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken cancellationToken)
    {
        var handler = update switch
        {
            // UpdateType.Unknown:
            // UpdateType.ChannelPost:
            // UpdateType.EditedChannelPost:
            // UpdateType.ShippingQuery:
            // UpdateType.PreCheckoutQuery:
            // UpdateType.Poll:
            { Message: { } message } => BotOnMessageReceived(message, cancellationToken),
            { EditedMessage: { } message } => BotOnMessageReceived(message, cancellationToken),
            { CallbackQuery: { } callbackQuery } => BotOnCallbackQueryReceived(callbackQuery, cancellationToken),
            { InlineQuery: { } inlineQuery } => BotOnInlineQueryReceived(inlineQuery, cancellationToken),
            { ChosenInlineResult: { } chosenInlineResult } => BotOnChosenInlineResultReceived(chosenInlineResult, cancellationToken),
            _ => UnknownUpdateHandlerAsync(update, cancellationToken)
        };

        await handler;
    }

    private async Task BotOnMessageReceived(Message message, CancellationToken cancellationToken)
    {
        Chat chat = message.Chat;
        User? user = message.From;
        await TrySaveMessageInfoAsync(chat, user);

        _logger.LogInformation("Receive message type: {MessageType}", message.Type);
        var voiceMessage = await VoiceMessageToText(_botClient, message);
        if (voiceMessage != null)
        {
            message.Text = voiceMessage;
            _botClient.SendMessage(
            chatId: message.Chat.Id,
            replyParameters: new ReplyParameters() { MessageId = message.MessageId },
            text: $"Вы сказали: {voiceMessage}",
            cancellationToken: cancellationToken);
        }
        if (message.Text is not { } && message.Caption is not { })
            return;
        var messageText = message.Text ?? message.Caption ?? string.Empty;
        _logger.LogInformation("Text: {MessageType}", messageText);
        if (!IsMe(message))
        {
            _logger.LogInformation("This message not for me.");
            return;
        }

        var action = messageText.Split(' ')[0].Replace($"@{_botInfo.Username}", string.Empty) switch
        {
            BotCommands.Billing => SendBillingInlineKeyboard(_botClient, message, cancellationToken),
            BotCommands.Model => SendInlineKeyboard(_botClient, message, cancellationToken),
            BotCommands.Provider => SendProviderInlineKeyboard(_botClient, message, cancellationToken),
            BotCommands.Restart => FailingHandler(_botClient, message, cancellationToken),

            "/keyboard" => SendReplyKeyboard(_botClient, message, cancellationToken),
            "/remove" => RemoveKeyboard(_botClient, message, cancellationToken),
            "/photo" => SendFile(_botClient, message, cancellationToken),
            "/request" => RequestContactAndLocation(_botClient, message, cancellationToken),
            "/inline_mode" => StartInlineQuery(_botClient, message, cancellationToken),
            "/throw" => FailingHandler(_botClient, message, cancellationToken),
            _ => ProcessMessage(_botClient, message, cancellationToken)

        }; ;
        Message sentMessage = await action;
        if (sentMessage != null)
        {
            _logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.MessageId);
        }

        // Send inline keyboard 
        // You can process responses in BotOnCallbackQueryReceived handler
        async Task<Message> SendInlineKeyboard(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            await botClient.SendChatAction(
                chatId: message.Chat.Id,
                action: ChatAction.Typing,
                cancellationToken: cancellationToken);
            IReadOnlyList<OpenAI.Models.Model> gptModels = await ActionWithShowTypeng(message.Chat.Id, cancellationToken,
                _messageProcessor.GetGPTModels(message.From.Id));

            var choises = new List<List<InlineKeyboardButton>>();
            var choisesRow = new List<InlineKeyboardButton>();
            int index = 0;
            foreach (OpenAI.Models.Model? model in gptModels.OrderBy(p => p.Id).ToList())
            {
                var data = $"{BotCommands.Model}:{model.Id}";
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
                    choisesRow = new List<InlineKeyboardButton>();
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
                text: "Выберите модель для ответов:",
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
            return result;

        }
        async Task<Message> SendBillingInlineKeyboard(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
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
            var choises = new List<List<InlineKeyboardButton>>();
            var choisesRow = new List<InlineKeyboardButton>();
            int index = 0;
            foreach (string date in dates.OrderBy(p => p).ToList())
            {
                var data = $"{BotCommands.Billing}:{date}";
                var choise = InlineKeyboardButton.WithCallbackData(date, data);
                if (index == 0 || (index % 2) == 0)
                {
                    if (choisesRow.Any())
                    {
                        choises.Add(choisesRow);
                    }
                    choisesRow = new List<InlineKeyboardButton>();
                }
                choisesRow.Add(choise);
                index++;
            }
            if (choisesRow.Any())
            {
                choises.Add(choisesRow);
            }

            InlineKeyboardMarkup replyMarkup = new(choises);
            Message result = await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Выберите дату за которую нужно отобразить билинг:",
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
            return result;
        }

        async Task<Message> SendProviderInlineKeyboard(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            await botClient.SendChatAction(
                chatId: message.Chat.Id,
                action: ChatAction.Typing,
                cancellationToken: cancellationToken);

            var choises = new List<List<InlineKeyboardButton>>();
            
            foreach (ChatStrategy strategy in Enum.GetValues(typeof(ChatStrategy)))
            {
                string text = strategy switch
                {
                    ChatStrategy.Auto => "🔄 Автоматическая ротация",
                    ChatStrategy.OpenAI => "OpenAI",
                    ChatStrategy.Gemini => "Gemini",
                    _ => strategy.ToString()
                };
                choises.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData(text, $"{BotCommands.Provider}:{(int)strategy}") });
            }

            InlineKeyboardMarkup replyMarkup = new(choises);
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Выберите стратегию работы с провайдерами:\n\n" +
                      "🔄 *Автоматическая ротация* — бот сам выбирает доступный сервис.\n" +
                      "🎯 *Конкретный провайдер* — бот будет использовать только выбранный сервис (без переключений при ошибках).",
                replyMarkup: replyMarkup,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> SendReplyKeyboard(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
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
                text: "Choose",
                replyMarkup: replyKeyboardMarkup,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> RemoveKeyboard(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Removing keyboard",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        }

        static async Task<Message> SendFile(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
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
                caption: "Nice Picture",
                cancellationToken: cancellationToken);
        }

        static async Task<Message> RequestContactAndLocation(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            ReplyKeyboardMarkup RequestReplyKeyboard = new(
                new[]
                {
                    KeyboardButton.WithRequestLocation("Location"),
                    KeyboardButton.WithRequestContact("Contact"),
                });

            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Who or Where are you?",
                replyMarkup: RequestReplyKeyboard,
                cancellationToken: cancellationToken);
        }

        static async Task<Message> Usage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            const string usage = "Usage:\n" +
                                 "/inline_keyboard - send inline keyboard\n" +
                                 "/keyboard    - send custom keyboard\n" +
                                 "/remove      - remove custom keyboard\n" +
                                 "/photo       - send a photo\n" +
                                 "/request     - request location or contact\n" +
                                 "/inline_mode - send keyboard with Inline Query";

            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: usage,
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);
        }
        async Task<Message> ProcessMessage(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
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
            Message message)
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
        static async Task<Message> StartInlineQuery(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            InlineKeyboardMarkup inlineKeyboard = new(
                InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("Inline Mode"));

            return await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "Press the button to start Inline Query",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }

#pragma warning disable RCS1163 // Unused parameter.
#pragma warning disable IDE0060 // Remove unused parameter
        static Task<Message> FailingHandler(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            Environment.Exit(1);
            throw new IndexOutOfRangeException();
        }
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore RCS1163 // Unused parameter.
    }

    private async Task TrySaveMessageInfoAsync(Chat chat, User? user)
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
                    Username = user.Username
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
        _logger.LogInformation("Received inline keyboard callback from: {CallbackQueryId}", callbackQuery.Id);
        var splitData = callbackQuery?.Data?.Split(':');
        if (splitData == null || splitData.Length < 2)
        {
            return;
        }
        string originalMessageType = splitData[0];
        string data = splitData[1];
        if (originalMessageType == BotCommands.Model)
        {
            var (isSuckes, errorMessage) = await _messageProcessor.SelectGPTModel(data, callbackQuery.From.Id);
            if (!isSuckes)
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQueryId: callbackQuery.Id,
                    text: $"Не удалось выбрать модель: {data}. {errorMessage}",
                    cancellationToken: cancellationToken);

                await _botClient.SendMessage(
                    chatId: callbackQuery.Message!.Chat.Id,
                    text: $"Не удалось выбрать: {data}. {errorMessage}",
                    cancellationToken: cancellationToken);
                return;
            }
            await _botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                text: $"Выбрана модель: {data}",
                cancellationToken: cancellationToken);

            await _botClient.SendMessage(
                chatId: callbackQuery.Message!.Chat.Id,
                text: $"Выбрана модель: {data}",
                cancellationToken: cancellationToken);
            return;
        }
        if (originalMessageType == BotCommands.Provider)
        {
            if (Enum.TryParse<ChatStrategy>(data, out var strategy))
            {
                await _messageProcessor.SetPreferredProvider(callbackQuery.From.Id, strategy);
                
                string strategyName = strategy switch
                {
                    ChatStrategy.Auto => "🔄 Автоматическая ротация",
                    ChatStrategy.OpenAI => "OpenAI",
                    ChatStrategy.Gemini => "Gemini",
                    _ => strategy.ToString()
                };

                string responseText = strategy == ChatStrategy.Auto 
                    ? $"Установлена стратегия: {strategyName}." 
                    : $"Установлена стратегия: 🎯 Только {strategyName}.";

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
        if (originalMessageType == BotCommands.Billing)
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
            sb.AppendLine("[UserName]([ChatName]) '[ModelName]': [PromptTokens] / [CompletionTokens] / [TotalTokens] / $[Cost]");
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
            sb.AppendLine("Donate to: 4731 1856 1625 8247");
            await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            text: "Billing: ",
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
            text: $"You chose result with Id: {chosenInlineResult.ResultId}",
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
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogInformation("HandleError: {ErrorMessage}", ErrorMessage);

        // Cooldown in case of network connection error
        if (exception is RequestException)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }

    private async Task<T> ActionWithShowTypeng<T>(ChatId chatId, CancellationToken cancellationToken, Task<T> action)

    {
        var result = action;
        var typingAction = new Func<Task>(() =>
        {
            Task result = _botClient.SendChatAction(
                chatId: chatId,
                action: ChatAction.Typing,
                cancellationToken: cancellationToken);
            return result;
        });

        try
        {
            Task[] tasks = new[] { typingAction(), result };
            while (true)
            {
                int completedTask = Task.WaitAny(tasks, cancellationToken);
                if (completedTask == 1 || result.Status == TaskStatus.RanToCompletion)
                {
                    break;
                }
                Thread.Sleep(1000);
                if (!action.IsCompleted)
                {
                    tasks[0] = typingAction();
                }
            }
            return result.Result;
        }
        catch (AggregateException ex)
        //catch (Exception ex)
        {
            var e = ex.Flatten() as Exception;
            while (e.InnerException != null)
            {
                e = ex.InnerException;
                _logger.LogError(e.Message);
            }
            await _botClient.SendMessage(
                chatId: chatId,
                text: ex.Flatten().Message,
                cancellationToken: cancellationToken);
            return await result;
        }
    }
}