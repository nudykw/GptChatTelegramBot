using System.Globalization;
using DataBaseLayer.Models;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using ServiceLayer.Resources;
using ServiceLayer.Services;
using ServiceLayer.Services.Localization;
using ServiceLayer.Utils;
using Xunit;

namespace ServiceLayer.UnitTests.Services.Localization
{
    public class DynamicLocalizerTests
    {
        private readonly Mock<IStringLocalizer<BotMessages>> _localizerMock = new();
        private readonly Mock<IStringLocalizerFactory> _factoryMock = new();
        private readonly Mock<IRepository<CachedTranslation>> _cacheRepoMock = new();
        private readonly Mock<IRepository<GptBilingItem>> _billingRepoMock = new();
        private readonly Mock<IChatService> _chatServiceMock = new();
        private readonly Mock<IUserContext> _userContextMock = new();
        private readonly Mock<ILogger<DynamicLocalizer>> _loggerMock = new();

        private readonly DynamicLocalizer _sut;

        public DynamicLocalizerTests()
        {
            _sut = new DynamicLocalizer(
                _localizerMock.Object,
                _factoryMock.Object,
                _cacheRepoMock.Object,
                _billingRepoMock.Object,
                _chatServiceMock.Object,
                _userContextMock.Object,
                _loggerMock.Object);
        }

        [Theory]
        [InlineData("en")]
        [InlineData("uk")]
        public void GetString_ShouldUseNativeLocalizer_WhenLanguageIsNative(string langCode)
        {
            // Arrange
            var culture = new CultureInfo(langCode);
            CultureInfo.CurrentUICulture = culture;
            var key = "Welcome";
            var expectedValue = "Native Welcome";

            _localizerMock.Setup(l => l[key]).Returns(new LocalizedString(key, expectedValue));

            // Act
            var result = _sut.GetString(key);

            // Assert
            Assert.Equal(expectedValue, result);
            _chatServiceMock.Verify(c => c.Ask(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void GetString_ShouldReturnCachedValue_WhenCacheExistsAndMatchesOriginal()
        {
            // Arrange
            CultureInfo.CurrentUICulture = new CultureInfo("fr");
            var key = "Welcome";
            var englishValue = "Welcome English";
            var cachedValue = "Bienvenue";

            _localizerMock.Setup(l => l[key]).Returns(new LocalizedString(key, englishValue));
            
            var cache = new List<CachedTranslation>
            {
                new CachedTranslation 
                { 
                    LanguageCode = "fr", 
                    ResourceKey = key, 
                    OriginalText = englishValue, 
                    TranslatedText = cachedValue 
                }
            }.AsQueryable();

            _cacheRepoMock.Setup(r => r.GetAll()).Returns(cache);

            // Act
            var result = _sut.GetString(key);

            // Assert
            Assert.Equal(cachedValue, result);
            _chatServiceMock.Verify(c => c.Ask(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void GetString_ShouldCallAiAndCache_WhenCacheIsMissing()
        {
            // Arrange
            CultureInfo.CurrentUICulture = new CultureInfo("de");
            var key = "Hello";
            var englishValue = "Hello English";
            var translatedValue = "Hallo";
            var userId = 12345L;

            _localizerMock.Setup(l => l[key]).Returns(new LocalizedString(key, englishValue));
            _cacheRepoMock.Setup(r => r.GetAll()).Returns(new List<CachedTranslation>().AsQueryable());
            _userContextMock.Setup(u => u.UserId).Returns(userId);
            _userContextMock.Setup(u => u.ChatId).Returns(0); // For now
            _chatServiceMock.Setup(c => c.Ask(0, userId, It.IsAny<string>())).ReturnsAsync(translatedValue);

            // Act
            var result = _sut.GetString(key);

            // Assert
            Assert.Equal(translatedValue, result);
            _cacheRepoMock.Verify(r => r.Add(It.Is<CachedTranslation>(t => 
                t.LanguageCode == "de" && 
                t.ResourceKey == key && 
                t.OriginalText == englishValue && 
                t.TranslatedText == translatedValue)), Times.Once);
            _cacheRepoMock.Verify(r => r.SaveChanges(), Times.Once);
        }

        [Fact]
        public void GetString_ShouldReTranslate_WhenOriginalTextInResourcesChanged()
        {
            // Arrange
            CultureInfo.CurrentUICulture = new CultureInfo("es");
            var key = "Bye";
            var oldEnglishValue = "Goodbye Old";
            var newEnglishValue = "Goodbye New";
            var translatedValue = "Adiós";

            _localizerMock.Setup(l => l[key]).Returns(new LocalizedString(key, newEnglishValue));
            
            var existingCache = new CachedTranslation 
            { 
                LanguageCode = "es", 
                ResourceKey = key, 
                OriginalText = oldEnglishValue, 
                TranslatedText = "Old Translation" 
            };

            _cacheRepoMock.Setup(r => r.GetAll()).Returns(new List<CachedTranslation> { existingCache }.AsQueryable());
            _chatServiceMock.Setup(c => c.Ask(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>())).ReturnsAsync(translatedValue);

            // Act
            var result = _sut.GetString(key);

            // Assert
            Assert.Equal(translatedValue, result);
            _cacheRepoMock.Verify(r => r.Update(It.Is<CachedTranslation>(t => 
                t.OriginalText == newEnglishValue && 
                t.TranslatedText == translatedValue)), Times.Once);
        }

        [Fact]
        public void GetString_ShouldFallbackToEnglish_WhenAiTranslationFails()
        {
            // Arrange
            CultureInfo.CurrentUICulture = new CultureInfo("it");
            var key = "Error";
            var englishValue = "English Error Message";

            _localizerMock.Setup(l => l[key]).Returns(new LocalizedString(key, englishValue));
            _cacheRepoMock.Setup(r => r.GetAll()).Returns(new List<CachedTranslation>().AsQueryable());
            _chatServiceMock.Setup(c => c.Ask(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>())).ThrowsAsync(new Exception("AI Offline"));

            // Act
            var result = _sut.GetString(key);

            // Assert
            Assert.Equal(englishValue, result);
            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)), Times.Once);
        }
    }
}
