using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using ServiceLayer.Constans;
using ServiceLayer.Services;
using ServiceLayer.Services.AudioTranscriptor;
using ServiceLayer.Services.Localization;
using ServiceLayer.Services.Telegram;
using ServiceLayer.Services.Telegram.Configuretions;
using ServiceLayer.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Xunit;
using Microsoft.Extensions.Localization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Reflection;

namespace ServiceLayer.UnitTests.Services.Telegram
{
    public class HelpCommandTests
    {
        private readonly Mock<ITelegramBotClient> _botClientMock = new();
        private readonly Mock<ILogger<UpdateHandler>> _loggerMock = new();
        private readonly Mock<IDynamicLocalizer> _localizerMock = new();
        private readonly Mock<IUserContext> _userContextMock = new();
        private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
        private readonly Mock<IServiceProvider> _serviceProviderMock = new();
        private readonly AppSettings _appSettings;

        private readonly Mock<IRepository<TelegramUserInfo>> _userInfoRepoMock = new();
        private readonly Mock<IRepository<TelegramChatInfo>> _chatInfoRepoMock = new();
        private readonly Mock<IRepository<AIBilingItem>> _aiBilingItemRepoMock = new();
        private readonly Mock<IChatService> _chatServiceMock = new();

        public HelpCommandTests()
        {
            _appSettings = new AppSettings
            {
                TelegramBotConfiguration = new TelegramBotConfiguration
                {
                    BotToken = "test_token",
                    OwnerId = 111 // Test Owner ID
                }
            };

            _localizerMock.Setup(l => l["HelpHeader"]).Returns("🤖 Available Bot Commands:");
            _localizerMock.Setup(l => l["PermissionDenied"]).Returns("❌ Permission Denied");
            
            // Mock GetMe
            _botClientMock.Setup(b => b.SendRequest(It.IsAny<global::Telegram.Bot.Requests.GetMeRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new User { Id = 999, Username = "test_bot" });

            // Setup ServiceProvider to return mocks
            _serviceProviderMock.Setup(x => x.GetService(typeof(IRepository<TelegramUserInfo>))).Returns(_userInfoRepoMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IRepository<TelegramChatInfo>))).Returns(_chatInfoRepoMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IRepository<AIBilingItem>))).Returns(_aiBilingItemRepoMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IUserContext))).Returns(_userContextMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IDynamicLocalizer))).Returns(_localizerMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IChatService))).Returns(_chatServiceMock.Object);

            // Mock repository Get
            _userInfoRepoMock.Setup(x => x.Get(It.IsAny<Expression<Func<TelegramUserInfo, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TelegramUserInfo { Id = 1, LanguageCode = "en", IsBot = false, FirstName = "Test" });
        }

        private UpdateHandler CreateHandler()
        {
            return new UpdateHandler(
                _serviceProviderMock.Object,
                _loggerMock.Object,
                _botClientMock.Object,
                null, // MessageProcessor
                null, // AudioTranscriptor
                _scopeFactoryMock.Object,
                _localizerMock.Object,
                _userContextMock.Object,
                _appSettings,
                _chatServiceMock.Object
            );
        }

        [Fact]
        public async Task Usage_ShouldShowOnlyPublicCommands_ForRegularUser()
        {
            var user = new User { Id = 222, FirstName = "Regular" };
            var chat = new Chat { Id = 333, Type = ChatType.Private };
            var message = new Message { From = user, Chat = chat, Text = "/help" };
            var handler = CreateHandler();

            string capturedText = "";
            _botClientMock.Setup(b => b.SendRequest(It.IsAny<global::Telegram.Bot.Requests.SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .Callback<object, CancellationToken>((req, ct) => {
                    if (req is global::Telegram.Bot.Requests.SendMessageRequest m) capturedText = m.Text;
                })
                .ReturnsAsync(new Message());

            var update = new Update { Message = message };
            var method = typeof(UpdateHandler).GetMethod("ProcessUpdateInternalAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)method.Invoke(handler, new object[] { _botClientMock.Object, update, CancellationToken.None });

            Assert.Contains("/help", capturedText);
            Assert.DoesNotContain("/billing", capturedText);
        }

        [Fact]
        public async Task Usage_ShouldShowOwnerCommands_ForOwner()
        {
            var user = new User { Id = 111, FirstName = "Owner" };
            var chat = new Chat { Id = 333, Type = ChatType.Private };
            var message = new Message { From = user, Chat = chat, Text = "/help" };
            var handler = CreateHandler();

            string capturedText = "";
            _botClientMock.Setup(b => b.SendRequest(It.IsAny<global::Telegram.Bot.Requests.SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .Callback<object, CancellationToken>((req, ct) => {
                    if (req is global::Telegram.Bot.Requests.SendMessageRequest m) capturedText = m.Text;
                })
                .ReturnsAsync(new Message());

            var update = new Update { Message = message };
            var method = typeof(UpdateHandler).GetMethod("ProcessUpdateInternalAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)method.Invoke(handler, new object[] { _botClientMock.Object, update, CancellationToken.None });

            Assert.Contains("/restart", capturedText);
            Assert.Contains("/billing", capturedText);
        }

        [Fact]
        public async Task Usage_ShouldShowAdminCommands_ForGroupAdmin()
        {
            var user = new User { Id = 444, FirstName = "Admin" };
            var chat = new Chat { Id = -555, Type = ChatType.Group };
            var message = new Message { From = user, Chat = chat, Text = "/help@test_bot" };
            var handler = CreateHandler();

            // Setup GetChatMember mock BEFORE the test execution
            _botClientMock.Setup(b => b.SendRequest(It.IsAny<global::Telegram.Bot.Requests.GetChatMemberRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ChatMemberAdministrator { User = user });

            string capturedText = "";
            _botClientMock.Setup(b => b.SendRequest(It.IsAny<global::Telegram.Bot.Requests.SendMessageRequest>(), It.IsAny<CancellationToken>()))
                .Callback<object, CancellationToken>((req, ct) => {
                    if (req is global::Telegram.Bot.Requests.SendMessageRequest m) capturedText = m.Text;
                })
                .ReturnsAsync(new Message());

            var update = new Update { Message = message };
            var method = typeof(UpdateHandler).GetMethod("ProcessUpdateInternalAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            await (Task)method.Invoke(handler, new object[] { _botClientMock.Object, update, CancellationToken.None });

            Assert.Contains("/billing", capturedText);
            Assert.Contains("/restart", capturedText); // Now available to AnyAdmin
            Assert.Contains("/help", capturedText);
        }
    }
}
