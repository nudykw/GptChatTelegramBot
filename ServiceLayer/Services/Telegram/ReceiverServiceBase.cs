using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ServiceLayer.Constans;

namespace ServiceLayer.Services.Telegram
{
    /// <summary>
    /// An abstract class to compose Receiver Service and Update Handler classes
    /// </summary>
    /// <typeparam name="TUpdateHandler">Update Handler to use in Update Receiver</typeparam>
    public abstract class ReceiverServiceBase<TUpdateHandler> : BaseService, IReceiverService
        where TUpdateHandler : IUpdateHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly IUpdateHandler _updateHandler;

        internal ReceiverServiceBase(IServiceProvider serviceProvider,
            ILogger<ReceiverServiceBase<TUpdateHandler>> logger,
            ITelegramBotClient botClient,
            TUpdateHandler updateHandler)
            : base(serviceProvider, logger)
        {
            _botClient = botClient;
            _updateHandler = updateHandler;
            logger.LogInformation($"EnvironmentVersion: {Environment.Version}");
        }

        /// <summary>
        /// Start to service Updates with provided Update Handler class
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public async Task ReceiveAsync(CancellationToken stoppingToken)
        {
            // ToDo: we can inject ReceiverOptions through IOptions container
            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                DropPendingUpdates = true,
            };

            var me = await _botClient.GetMe(stoppingToken);
            _logger.LogInformation("Start receiving updates for {BotName}", me.Username ?? "My Awesome Bot");

            // Register bot commands
            var commands = BotCommands.Descriptions.Select(x => new BotCommand
            {
                Command = x.Key.TrimStart('/'),
                Description = x.Value
            });
            await _botClient.SetMyCommandsAsync(commands, cancellationToken: stoppingToken);
            _logger.LogInformation("Bot commands registered successfully.");

            // Start receiving updates
            await _botClient.ReceiveAsync(
                updateHandler: _updateHandler,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken);
        }
    }
}
