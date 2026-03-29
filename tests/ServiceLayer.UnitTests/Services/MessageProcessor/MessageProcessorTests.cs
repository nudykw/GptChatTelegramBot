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

        private readonly MessageProcessorClass _sut;

        public MessageProcessorTests()
        {
            // Mock AppSettings
            var appSettings = new AppSettings { 
                TelegramBotConfiguration = new ServiceLayer.Services.Telegram.Configuretions.TelegramBotConfiguration()
            };
            var optionsMock = new Mock<IOptions<AppSettings>>();
            optionsMock.Setup(x => x.Value).Returns(appSettings);
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
    }
}
