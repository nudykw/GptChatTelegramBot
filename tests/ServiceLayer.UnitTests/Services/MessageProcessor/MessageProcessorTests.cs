using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ServiceLayer.Services;
using ServiceLayer.Services.MessageProcessor;
using Telegram.Bot;
using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using ServiceLayer.Services.Localization;
using Xunit;

using MessageProcessorClass = ServiceLayer.Services.MessageProcessor.MessageProcessor;

namespace ServiceLayer.UnitTests.Services.MessageProcessor
{
    public class MessageProcessorTests
    {
        private readonly Mock<IServiceProvider> _serviceProviderMock = new();
        private readonly Mock<ILogger<MessageProcessorClass>> _loggerMock = new();
        private readonly Mock<IChatService> _chatServiceMock = new();
        private readonly Mock<ITelegramBotClient> _botClientMock = new();
        private readonly Mock<IRepository<TelegramUserInfo>> _userInfoRepositoryMock = new();
        private readonly Mock<IDynamicLocalizer> _localizerMock = new();
        private readonly Mock<IRepository<HistoryMessage>> _historyRepositoryMock = new();

        private readonly AppSettings _appSettings;
        private readonly MessageProcessorClass _sut;

        public MessageProcessorTests()
        {
            // Mock AppSettings
            _appSettings = new AppSettings { 
                TelegramBotConfiguration = new ServiceLayer.Services.Telegram.Configuretions.TelegramBotConfiguration
                {
                    OwnerId = 7342855906L,
                    InitialBalance = 0.1M
                }
            };
            var optionsMock = new Mock<IOptions<AppSettings>>();
            optionsMock.Setup(x => x.Value).Returns(_appSettings);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IOptions<AppSettings>)))
                .Returns(optionsMock.Object);

            _sut = new MessageProcessorClass(
                _serviceProviderMock.Object,
                _loggerMock.Object,
                _historyRepositoryMock.Object,
                _chatServiceMock.Object,
                _botClientMock.Object,
                _userInfoRepositoryMock.Object,
                _localizerMock.Object);
        }

        [Theory]
        [InlineData("немецкий", "de")]
        [InlineData("Spanish", "es")]
        [InlineData("fr", "fr")]
        [InlineData("Українська", "uk")]
        public async Task IdentifyLanguage_ShouldReturnCorrectCode_WhenAiResponds(string input, string expectedCode)
        {
            // Arrange
            var chatId = 1L;
            var userId = 2L;
            _chatServiceMock.Setup(c => c.Ask(chatId, userId, It.Is<string>(s => s.Contains(input))))
                .ReturnsAsync(expectedCode);

            // Act
            var result = await _sut.IdentifyLanguage(chatId, userId, input);

            // Assert
            Assert.Equal(expectedCode, result);
        }

        [Fact]
        public async Task IdentifyLanguage_ShouldReturnNull_WhenInputIsEmpty()
        {
            // Act
            var result = await _sut.IdentifyLanguage(1, 1, "");

            // Assert
            Assert.Null(result);
            _chatServiceMock.Verify(c => c.Ask(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task IdentifyLanguage_ShouldFallbackToEn_WhenAiReturnsInvalidResponse()
        {
            // Arrange
            _chatServiceMock.Setup(c => c.Ask(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync("I am not sure, but maybe it is Tolkien language");

            // Act
            var result = await _sut.IdentifyLanguage(1, 1, "Some gibberish");

            // Assert
            Assert.Equal("en", result);
        }

        [Fact]
        public async Task GetBalanceDisplay_ShouldReturnCorrectFormatForRegularUser()
        {
            // Arrange
            var userId = 123L;
            var user = new TelegramUserInfo { Id = userId, Balance = 0.005M, FirstName = "Test", IsBot = false };
            _userInfoRepositoryMock.Setup(r => r.Get(It.IsAny<System.Linq.Expressions.Expression<System.Func<TelegramUserInfo, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Act
            var result = await _sut.GetBalanceDisplay(userId);

            // Assert
            Assert.Equal(" | B: $0.0050", result);
        }

        [Fact]
        public async Task GetBalanceDisplay_ShouldReturnUnlimitedForOwner()
        {
            // Arrange
            var ownerId = 7342855906L;
            var user = new TelegramUserInfo { Id = ownerId, Balance = 0.0M, FirstName = "Owner", IsBot = false };
            _userInfoRepositoryMock.Setup(r => r.Get(It.IsAny<System.Linq.Expressions.Expression<System.Func<TelegramUserInfo, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Act
            var result = await _sut.GetBalanceDisplay(ownerId);

            // Assert
            Assert.Equal(" (Unlimited)", result);
        }
    }
}
