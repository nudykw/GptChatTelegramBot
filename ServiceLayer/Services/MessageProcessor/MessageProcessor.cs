using DataBaseLayer.Enums;
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
using System.Text.RegularExpressions;

namespace ServiceLayer.Services.MessageProcessor;
public class MessageProcessor : BaseService
{
    private readonly IRepository<HistoryMessage> _historyMessageRepository;
    private readonly IRepository<TelegramUserInfo> _telegramUserInfoRepository;
    private readonly IChatService _chatService;
    private readonly IChatServiceFactory _chatServiceFactory;
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IDynamicLocalizer _localizer;
    private readonly string[] drawWords = new[]
    {
        "draw", "рисуй", "покажи", "малю"
    };
    private readonly TelegramBotConfiguration _telegramBotConfiguration;

    public MessageProcessor(IServiceProvider serviceProvider, ILogger<MessageProcessor> logger,
        IRepository<HistoryMessage> historyMessageRepository, IChatService chatService,
        IChatServiceFactory chatServiceFactory,
        ITelegramBotClient telegramBotClient, IRepository<TelegramUserInfo> telegramUserInfoRepository,
        IDynamicLocalizer localizer)
        : base(serviceProvider, logger)
    {
        _historyMessageRepository = historyMessageRepository;
        _chatService = chatService;
        _chatServiceFactory = chatServiceFactory;
        _telegramBotClient = telegramBotClient;
        _telegramUserInfoRepository = telegramUserInfoRepository;
        _localizer = localizer;
        AppSettings? appConfig = _serviceProvider.GetConfiguration<AppSettings>();
        _telegramBotConfiguration = appConfig.TelegramBotConfiguration;
    }

    public async Task<string> ProcessMessage(long chatId, long messageId, long? parentMessageId,
        long fromUserId, string message, CancellationToken cancellationToken, bool isEdit = false)
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
        HistoryMessage? historyMessage = null;
        if (parentMessageId.HasValue)
        {
            historyMessage = await _historyMessageRepository.Get(p => p.ChatId == chatId && p.MessageId == parentMessageId.Value);
        }
        else
        {
            // Create user history for the current message if it's new
            historyMessage = new HistoryMessage
            {
                ChatId = chatId,
                MessageId = (int)messageId,
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
        
        var currentHistory = historyMessage;
        while (currentHistory != null && currentHistory.ParentMessageId != null)
        {
            currentHistory = await _historyMessageRepository.Get(p => p.ChatId == currentHistory.ChatId
            && p.MessageId == currentHistory.ParentMessageId);
            if (currentHistory != null)
            {
                query.Insert(0, new AiMessage((global::OpenAI.Role)currentHistory.RoleId, currentHistory.Text,
                    currentHistory.FromUserName));
            }
        }

        string toSendText = string.Empty;
        string responceText = string.Empty;
        string? usedProviderName = null;
        string? usedModelName = null;

        if (await IsNeedDrawImage(chatId, fromUserId, message))
        {
            await ProcessDrawCommand(chatId, messageId, fromUserId, message, cancellationToken);
            return "Image generated"; 
        }
        else if (parentMessageId.HasValue)
        {
            // NEW: Always try to find image context up the chain, instead of strict immediate parent check
            string? originalPrompt = null;
            string? imageUrl = null;
            string? botAnalysisText = null;
            string? telegramFileId = null;
            
            var promptRegex = new Regex(@"\[Image Prompt: (.*?) \| URL: (.*?)\]", RegexOptions.Singleline);
            var fileIdRegex = new Regex(@"\[Telegram FileId: (.*?)\]", RegexOptions.Singleline);
            var parentMsg = await _historyMessageRepository.Get(p => p.ChatId == chatId && p.MessageId == parentMessageId.Value);
            
            var currentChainMsg = parentMsg;
            int depth = 0;
            while (currentChainMsg != null && depth < 5)
            {
                var match = promptRegex.Match(currentChainMsg.Text);
                var fileIdMatch = fileIdRegex.Match(currentChainMsg.Text);

                if (fileIdMatch.Success)
                {
                    telegramFileId = fileIdMatch.Groups[1].Value;
                }

                if (match.Success)
                {
                    originalPrompt = match.Groups[1].Value;
                    imageUrl = match.Groups[2].Value;
                    
                    // Capture analysis if prompt is empty
                    if (string.IsNullOrEmpty(originalPrompt))
                    {
                        botAnalysisText = currentChainMsg.Text.Replace(match.Value, "").Replace(fileIdMatch.Value, "").Trim();
                    }
                    break;
                }
                else if (fileIdMatch.Success)
                {
                    // If we only have fileId, we can stop here too
                    break;
                }
                else if (currentChainMsg.Text.Contains("http"))
                {
                    var urlRegex = new Regex(@"http[^\s]+");
                    var urlMatch = urlRegex.Match(currentChainMsg.Text);
                    if (urlMatch.Success)
                    {
                        imageUrl = urlMatch.Value;
                        break;
                    }
                }
                
                if (currentChainMsg.ParentMessageId.HasValue)
                {
                    currentChainMsg = await _historyMessageRepository.Get(p => p.ChatId == chatId && p.MessageId == currentChainMsg.ParentMessageId.Value);
                    depth++;
                }
                else break;
            }

            if (!string.IsNullOrEmpty(imageUrl) || !string.IsNullOrEmpty(telegramFileId))
            {
                string? workingFilePath = null;
                bool isTempFile = false;

                try 
                {
                    if (!string.IsNullOrEmpty(telegramFileId))
                    {
                        // Download from Telegram
                        var file = await _telegramBotClient.GetFile(telegramFileId);
                        workingFilePath = Path.GetTempFileName() + ".png";
                        using (var stream = new FileStream(workingFilePath, FileMode.Create))
                        {
                            await _telegramBotClient.DownloadFile(file.FilePath, stream);
                        }
                        isTempFile = true;
                    }

                    // Proceed with Image Intent Classification
                bool isAnalysis = true; 
                try 
                {
                    var classificationProvider = _telegramBotConfiguration.AiSettings.Classification?.ProviderName ?? "OpenAI";
                    var classificationService = _chatServiceFactory.CreateService(classificationProvider);
                    
                    var classificationPrompt = $"Task: Classify user intent for the following image-related message.\n" +
                                             $"User Message: '{message}'\n\n" +
                                             $"Options:\n" +
                                             $"1. ANALYZE: If user asks what is on image, to describe, identify or just chat about context.\n" +
                                             $"2. EDIT: If user asks to change, modify, redraw, style or add something to the image.\n" +
                                             $"3. Russian clues: 'Сделай', 'Измени', 'Поменяй', 'Нарисуй', 'Добавь' are 100% EDIT.\n\n" +
                                             $"Return ONLY the word 'ANALYZE' or 'EDIT'. No explanations.";
                    
                    var classificationResponse = await classificationService.Ask(chatId, fromUserId, classificationPrompt);
                    var decision = classificationResponse.Trim();
                    _logger.LogInformation("Intent Classification Decision: Raw='{0}', UserMsg='{1}', FileId='{2}'", decision, message, telegramFileId);
                    isAnalysis = !decision.Contains("EDIT", StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Intent classification failed for message '{0}'. Defaulting to ANALYZE.", message);
                    isAnalysis = true; 
                }

                if (isAnalysis)
                {
                    // Proceed to ANALYZE mode (Vision API)
                    var visionProviderName = _telegramBotConfiguration.AiSettings.Vision?.ProviderName ?? "OpenAI";
                    var visionService = _chatServiceFactory.CreateService(visionProviderName);
                    
                    // Supress tutorial instructions in the prompt
                    var visionPrompt = $"{message}\n\nIMPORTANT: Describe or analyze the image directly. Do not give any instructions, advice, or tutorials on how to edit it. Just answer the user's question concisely.";

                    var result = await visionService.AnalyzeImageAsync(chatId, fromUserId, imageUrl, workingFilePath, visionPrompt);
                    var finalResText = string.Join(" ", result.Choices.Where(p => !string.IsNullOrWhiteSpace(p)));
                    
                    if (originalPrompt != null || imageUrl != null || telegramFileId != null)
                    {
                        string historyPrefix = "";
                        if (telegramFileId != null) historyPrefix += $"[Telegram FileId: {telegramFileId}]\n";
                        if (imageUrl != null) historyPrefix += $"[Image Prompt: {originalPrompt} | URL: {imageUrl}]\n";
                        finalResText = historyPrefix + finalResText;
                    }
                    
                    responceText = finalResText;
                    toSendText = finalResText;
                    usedProviderName = visionProviderName;
                    usedModelName = result.ModelName;
                }
                else
                {
                    // Proceed to EDIT mode (DALL-E 3 Refinement via Vision Prompt)
                    var drawingProviderName = _telegramBotConfiguration.AiSettings.Drawing?.ProviderName ?? "OpenAI"; 
                    var drawingModelName = _telegramBotConfiguration.AiSettings.Drawing?.ModelName ?? "dall-e-3";
                    var visionProviderName = _telegramBotConfiguration.AiSettings.Vision?.ProviderName ?? "OpenAI";
                    var visionService = _chatServiceFactory.CreateService(visionProviderName);
                    var drawingService = _chatServiceFactory.CreateService(drawingProviderName);

                    string basePrompt = originalPrompt;
                    if (string.IsNullOrEmpty(basePrompt) && !string.IsNullOrEmpty(imageUrl))
                    {
                        if (!string.IsNullOrEmpty(botAnalysisText))
                        {
                            _logger.LogInformation("Using bot's own analysis as base prompt for EDIT request.");
                            basePrompt = botAnalysisText;
                        }
                        else
                        {
                            _logger.LogInformation("No prompt found. Using Vision to describe image first.");
                            var descriptionResponse = await visionService.AnalyzeImageAsync(chatId, fromUserId, imageUrl, null, 
                                "Briefly and accurately describe this image specifically for a DALL-E prompt generation. Focus on subject, composition, and style.");
                            basePrompt = string.Join(" ", descriptionResponse.Choices.Where(p => !string.IsNullOrWhiteSpace(p)));
                        }
                    }

                    var refinementPrompt = $"Task: Generate a new DALL-E prompt based on an existing image description and user change request.\n" +
                                         $"Base image description: '{basePrompt}'\n" +
                                         $"User change request: '{message}'\n" +
                                         $"Return ONLY the final, detailed DALL-E prompt in English. Just the prompt text.";
                    
                    var newPrompt = await visionService.Ask(chatId, fromUserId, refinementPrompt);
                    await ProcessDrawCommand(chatId, messageId, fromUserId, newPrompt, cancellationToken);
                    return "Refining image with DALL-E 3 based on context description";
                }
            }
            finally
                {
                    if (isTempFile && workingFilePath != null && File.Exists(workingFilePath))
                    {
                        try { File.Delete(workingFilePath); } catch { }
                    }
                }
            }
        }
        
        if (string.IsNullOrEmpty(toSendText))
        {
            // Clean up history technical prefixes [Image Prompt: ...] from all messages in the query
            // Since OpenAI.Chat.Message.Content is read-only, we create a new list with cleaned messages
            var cleanedQuery = new List<AiMessage>();
            var cleanRegex = new Regex(@"^\[Image Prompt:.*?\| URL:.*?\]\n?", RegexOptions.Singleline);
            
            foreach (var aiMsg in query)
            {
                string originalText = aiMsg.Content;
                if (!string.IsNullOrEmpty(originalText) && originalText.Contains("[Image Prompt:"))
                {
                    string cleanedText = cleanRegex.Replace(originalText, "");
                    cleanedQuery.Add(new AiMessage(aiMsg.Role, cleanedText, aiMsg.Name));
                }
                else
                {
                    cleanedQuery.Add(aiMsg);
                }
            }

            var aiResponse = await _chatService.SendMessages2ChatAsync(chatId, fromUserId, cleanedQuery);
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

            sb.Clear();
            sb.AppendLine(responceText);
            sb.AppendLine(summary.WrapWithSpoiler(_telegramBotConfiguration.DefaultParseMode));
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
                if (isEdit)
                {
                    // Find the previous response to this message
                    var previousResponse = await _historyMessageRepository.Get(p => p.ChatId == chatId && p.ParentMessageId == messageId && p.RoleId == (int)global::OpenAI.Role.Assistant);
                    if (previousResponse != null)
                    {
                        telegramResponce = await _telegramBotClient.EditMessageText(
                            chatId: chatId,
                            messageId: (int)previousResponse.MessageId,
                            text: chunk,
                            parseMode: _telegramBotConfiguration.DefaultParseMode,
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // Fallback to send if not found or if we have multiple chunks and only one was in history
                        telegramResponce = await _telegramBotClient.SendMessage(
                            chatId: chatId,
                            text: chunk,
                            parseMode: _telegramBotConfiguration.DefaultParseMode,
                            replyParameters: new ReplyParameters() { MessageId = (int)messageId },
                            cancellationToken: cancellationToken);
                    }
                }
                else
                {
                    telegramResponce = await _telegramBotClient.SendMessage(
                            chatId: chatId,
                            text: chunk,
                            parseMode: _telegramBotConfiguration.DefaultParseMode,
                            replyParameters: new ReplyParameters() { MessageId = (int)messageId },
                            replyMarkup: new ReplyKeyboardRemove(),
                            cancellationToken: cancellationToken);
                }
            }
            catch (global::Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.ErrorCode == 400 && (ex.Message.Contains("can't parse entities") || ex.Message.Contains("bad request")))
            {
                _logger.LogWarning(ex, "Failed to send chunk with formatting, falling back to None. Message: {Message}", ex.Message);
                if (isEdit && telegramResponce != null) // If we already started editing or know it's an edit
                {
                     // Typically if edit fails for formatting, we might want to try Send as fallback or just SendMessage without formatting
                }
                
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
            await SaveHistory(telegramResponce, responceText, usedProviderName, usedModelName, (int)global::OpenAI.Role.Assistant);
        }
        return responceText;
    }

    private async Task SaveHistory(TgMessage telegramMessage, string text, string? providerName, string? modelName, int roleId, long? originalMessageId = null)
    {
        var historyMessage = new HistoryMessage
        {
            ChatId = telegramMessage.Chat.Id,
            CreationDate = DateTime.UtcNow,
            MessageId = telegramMessage.MessageId,
            ModifiedDate = DateTime.UtcNow,
            ParentMessageId = (long?)(telegramMessage.ReplyToMessage?.MessageId ?? originalMessageId),
            RoleId = roleId,
            Text = text,
            ProviderName = providerName,
            ModelName = modelName
        };
        _historyMessageRepository.Add(historyMessage);
        await _historyMessageRepository.SaveChanges();
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
        var response = await InternalProcessDrawImage(chatId, fromUserId, prompt);
        if (response.Choices != null && response.Choices.Any())
        {
            var imageUrl = response.Choices.First();
            if (!string.IsNullOrEmpty(imageUrl))
            {
                var summary = $"{response.ProviderName} | {response.ModelName} | " +
                              $"L:{response.LatencySeconds:F1}s | ${response.Cost:F4}" +
                              await GetBalanceDisplay(fromUserId);
                
                var spoilerSummary = summary.WrapWithSpoiler(_telegramBotConfiguration.DefaultParseMode);

                using var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                using var ms = new MemoryStream(imageBytes);

                var telegramResponce = await _telegramBotClient.SendPhoto(
                    chatId: chatId,
                    photo: new InputFileStream(ms, "image.png"),
                    caption: spoilerSummary,
                    parseMode: _telegramBotConfiguration.DefaultParseMode,
                    replyParameters: new ReplyParameters() { MessageId = (int)messageId },
                    cancellationToken: cancellationToken);
                    
                if (telegramResponce != null)
                {
                    // New format: [Image Prompt: {prompt} | URL: {url}]
                    string historyText = $"[Image Prompt: {prompt} | URL: {imageUrl}]";
                    await SaveHistory(telegramResponce, historyText, response.ProviderName, response.ModelName, (int)global::OpenAI.Role.Assistant, messageId);
                }
                
                return telegramResponce;
            }
        }

        var errorResponce = await _telegramBotClient.SendMessage(
                chatId: chatId,
                text: _localizer["Error_ImageGenerationFailed"],
                replyParameters: new ReplyParameters() { MessageId = (int)messageId },
                cancellationToken: cancellationToken);
                
        return errorResponce;
    }

    private async Task<ChatServiceResponse> InternalProcessDrawImage(long chatId, long fromUserId, string message)
    {
        var botInfo = await _telegramBotClient.GetMe();
        var msg = message.Replace(botInfo.Username ?? "", string.Empty);
        try
        {
            // Use pinned Drawing provider and model from config
            var drawingProviderName = _telegramBotConfiguration.AiSettings.Drawing?.ProviderName ?? "OpenAI";
            var drawingModelName = _telegramBotConfiguration.AiSettings.Drawing?.ModelName ?? "dall-e-3";
            
            var drawingService = _chatServiceFactory.CreateService(drawingProviderName);
            return await drawingService.GenerateImage(chatId, fromUserId, msg, drawingModelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate image.");
            return new ChatServiceResponse { ProviderName = "Error", ModelName = "Error", Choices = new List<string>() };
        }
    }

    private async Task SaveHistory(long chatId, long messageId, long? parentMessageId, int roleId, string text, string? providerName, string? modelName)
    {
        var historyMessage = new HistoryMessage
        {
            ChatId = chatId,
            MessageId = (int)messageId,
            ParentMessageId = parentMessageId,
            RoleId = roleId,
            Text = text,
            ProviderName = providerName,
            ModelName = modelName,
            CreationDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };
        _historyMessageRepository.Add(historyMessage);
        await _historyMessageRepository.SaveChanges();
    }

    internal async Task<IReadOnlyList<global::OpenAI.Models.Model>> GetAIModels(long userId)
    {
        IReadOnlyList<global::OpenAI.Models.Model> result = await _chatService.GetAvailibleModels(userId);
        return result;
    }

    internal async Task<TgMessage> ProcessImage(long chatId, int messageId, int? replyToMessagemessageId,
        long userId, string? messageText, string filePath, string telegramFileId, CancellationToken cancellationToken)
    {
        // First, save the user's message with the image to history
        var userHistory = new HistoryMessage
        {
            ChatId = chatId,
            MessageId = messageId,
            ParentMessageId = replyToMessagemessageId,
            RoleId = (int)global::OpenAI.Role.User,
            Text = $"[Telegram FileId: {telegramFileId}]\n" + (messageText ?? "[Image]"),
            CreationDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            FromUserName = userId.ToString()
        };
        _historyMessageRepository.Add(userHistory);
        await _historyMessageRepository.SaveChanges();

        // Determine intent: Analyze (Vision) or Edit (DALL-E)
        var appSettings = _serviceProvider.GetConfiguration<AppSettings>();
        var visionModel = appSettings?.TelegramBotConfiguration?.AiSettings?.Vision?.ModelName ?? "gpt-4o";
        var visionProvider = appSettings?.TelegramBotConfiguration?.AiSettings?.Vision?.ProviderName ?? "OpenAI";

        bool isAnalysis = true;
        if (!string.IsNullOrWhiteSpace(messageText))
        {
            try 
            {
                var classificationProvider = _telegramBotConfiguration.AiSettings.Classification?.ProviderName ?? "OpenAI";
                var classificationModel = _telegramBotConfiguration.AiSettings.Classification?.ModelName ?? "gpt-4o";
                var classificationService = _chatServiceFactory.CreateService(classificationProvider);
                
                var classificationPrompt = $"Task: Classify user intent for an image. \n" +
                                         $"Input: '{messageText}'\n" +
                                         $"Options: 'ANALYZE' (describe, identify, answer questions) or 'EDIT' (change, redraw, modify, style).\n" +
                                         $"Return ONLY 'ANALYZE' or 'EDIT'.";
                var classification = await classificationService.Ask(chatId, userId, classificationPrompt);
                isAnalysis = !classification.Contains("EDIT", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Intent classification in ProcessImage failed for text '{0}'. Defaulting to ANALYZE.", messageText);
                isAnalysis = true;
            }
        }

        ChatServiceResponse aiImageResponse;
        if (isAnalysis)
        {
            // Use Vision for analysis with pinned provider
            var visionProviderName = _telegramBotConfiguration.AiSettings.Vision?.ProviderName ?? "OpenAI";
            var visionModelName = _telegramBotConfiguration.AiSettings.Vision?.ModelName ?? "gpt-4o";
            var visionService = _chatServiceFactory.CreateService(visionProviderName);
            aiImageResponse = await visionService.AnalyzeImageAsync(chatId, userId, null, filePath, messageText, visionModelName);
        }
        else
        {
            // NEW: Use intelligent DALL-E 3 Refinement for direct photo uploads too
            var visionProviderName = _telegramBotConfiguration.AiSettings.Vision?.ProviderName ?? "OpenAI";
            var visionService = _chatServiceFactory.CreateService(visionProviderName);
            
            _logger.LogInformation("Photo upload with EDIT intent. Describing image first.");
            var descriptionResponse = await visionService.AnalyzeImageAsync(chatId, userId, null, filePath, 
                "Briefly and accurately describe this image specifically for a DALL-E prompt generation. Focus on subject, composition, and style.");
            var basePrompt = string.Join(" ", descriptionResponse.Choices.Where(p => !string.IsNullOrWhiteSpace(p)));
            
            var refinementPrompt = $"Task: Generate a new DALL-E prompt based on an image description and user change request.\n" +
                                 $"Base image: '{basePrompt}'\n" +
                                 $"Change: '{messageText}'\n" +
                                 $"Return ONLY the final, detailed DALL-E prompt in English.";
            
            var newPrompt = await visionService.Ask(chatId, userId, refinementPrompt);
            return await ProcessDrawCommand(chatId, messageId, userId, newPrompt, cancellationToken);
        }

        var sb = new StringBuilder();
        foreach (var choice in aiImageResponse.Choices.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            sb.AppendLine(choice);
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
                replyParameters: new ReplyParameters() { MessageId = messageId },
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: cancellationToken);

        if (telegramResponce != null)
        {
            string historyText = isAnalysis ? toSendText : (messageText ?? "[Image Generation]");
            historyText = $"[Telegram FileId: {telegramFileId}]\n" + historyText;
            await SaveHistory(telegramResponce, historyText, aiImageResponse.ProviderName, aiImageResponse.ModelName, (int)global::OpenAI.Role.Assistant);
        }

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
        
        var classificationProvider = _telegramBotConfiguration.AiSettings.Classification?.ProviderName ?? "OpenAI";
        var classificationService = _chatServiceFactory.CreateService(classificationProvider);
        var result = await classificationService.Ask(chatId, userId, prompt);
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

        var classificationProvider = _telegramBotConfiguration.AiSettings.Classification?.ProviderName ?? "OpenAI";
        var classificationService = _chatServiceFactory.CreateService(classificationProvider);
        var isNeewDrawResponce = await classificationService.Ask(charId, userId, prompt);
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

        var classificationProvider = _telegramBotConfiguration.AiSettings.Classification?.ProviderName ?? "OpenAI";
        var classificationService = _chatServiceFactory.CreateService(classificationProvider);
        string isNeewDrawResponce = await classificationService.Ask(charId, userId, prompt);
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
