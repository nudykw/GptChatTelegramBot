using System.Net;
using System.Security.Authentication;
using ServiceLayer.Utils;
using Xunit;
using Microsoft.Extensions.Logging;

namespace ServiceLayer.UnitTests;

public class AiErrorHelperTests
{
    [Fact]
    public void GetErrorDetails_HttpRequestException_Unauthorized_ReturnsCorrectionMessage()
    {
        // Arrange
        var ex = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);
        var provider = "TestProvider";

        // Act
        var result = AiErrorHelper.HandleAndGetException(null, ex, provider, "TestMethod");

        // Assert
        var aiEx = Assert.IsType<AiProviderException>(result);
        Assert.Contains("Ошибка авторизации", aiEx.Message);
        Assert.Contains("TestProvider", aiEx.ProviderName);
        Assert.Contains("401", aiEx.TechnicalDetails);
    }

    [Fact]
    public void GetErrorDetails_HttpRequestException_TooManyRequests_ReturnsCorrectMessage()
    {
        // Arrange
        var ex = new HttpRequestException("Rate limit", null, HttpStatusCode.TooManyRequests);
        var provider = "TestProvider";

        // Act
        var result = AiErrorHelper.HandleAndGetException(null, ex, provider, "TestMethod");

        // Assert
        var aiEx = Assert.IsType<AiProviderException>(result);
        Assert.Contains("Превышен лимит запросов", aiEx.Message);
    }

    [Fact]
    public void GetErrorDetails_AuthenticationException_ReturnsCorrectMessage()
    {
        // Arrange
        var ex = new AuthenticationException("Invalid API Key");
        var provider = "TestProvider";

        // Act
        var result = AiErrorHelper.HandleAndGetException(null, ex, provider, "TestMethod");

        // Assert
        var aiEx = Assert.IsType<AiProviderException>(result);
        Assert.Contains("Ошибка аутентификации", aiEx.Message);
    }

    [Fact]
    public void GetErrorDetails_GeneralException_ContainsGenericMessage()
    {
        // Arrange
        var ex = new Exception("Something went wrong");
        var provider = "TestProvider";

        // Act
        var result = AiErrorHelper.HandleAndGetException(null, ex, provider, "TestMethod");

        // Assert
        var aiEx = Assert.IsType<AiProviderException>(result);
        Assert.Contains("Произошла ошибка", aiEx.Message);
        Assert.Equal("Something went wrong", aiEx.TechnicalDetails);
    }
}
