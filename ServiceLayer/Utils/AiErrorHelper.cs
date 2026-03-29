using System.Net;
using System.Security.Authentication;
using Microsoft.Extensions.Logging;

namespace ServiceLayer.Utils;

public record AiErrorDetails(string UserMessage, string TechnicalDetails);

public class AiProviderException : Exception
{
    public string TechnicalDetails { get; }
    public string ProviderName { get; }

    public AiProviderException(string userMessage, string technicalDetails, string providerName, Exception innerException)
        : base(userMessage, innerException)
    {
        TechnicalDetails = technicalDetails;
        ProviderName = providerName;
    }
}

public static class AiErrorHelper
{
    public static AiErrorDetails GetErrorDetails(Exception ex, string providerName)
    {
        string userMessage = "Произошла ошибка при обращении к AI провайдеру.";
        string technicalDetails = ex.Message;

        if (ex is HttpRequestException httpEx)
        {
            technicalDetails = $"HTTP Error {(int?)httpEx.StatusCode}: {httpEx.Message}";
            
            switch (httpEx.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    userMessage = $"Ошибка авторизации у провайдера {providerName}. Проверьте API ключ.";
                    break;
                case HttpStatusCode.Forbidden:
                    userMessage = $"Доступ запрещен провайдером {providerName}. Возможно, ваш регион не поддерживается или ключ не имеет доступа к модели.";
                    break;
                case HttpStatusCode.TooManyRequests:
                    userMessage = $"Превышен лимит запросов или квота у провайдера {providerName}. Пожалуйста, попробуйте позже.";
                    break;
                case HttpStatusCode.BadRequest:
                    userMessage = $"Некорректный запрос к {providerName}. Возможно, модель не поддерживает данный формат или параметры.";
                    break;
                case HttpStatusCode.NotFound:
                    userMessage = $"Модель или ресурс не найдены у провайдера {providerName}.";
                    break;
                case HttpStatusCode.ServiceUnavailable:
                    userMessage = $"Сервис {providerName} временно недоступен. Попробуйте позже.";
                    break;
                default:
                    if ((int?)httpEx.StatusCode >= 500)
                    {
                        userMessage = $"Внутренняя ошибка сервера у провайдера {providerName}.";
                    }
                    break;
            }
        }
        else if (ex is AuthenticationException authEx)
        {
            userMessage = $"Ошибка аутентификации в {providerName}. Неверный API ключ.";
            technicalDetails = authEx.Message;
        }
        else if (ex.GetType().Name.Contains("ApiException") || ex.GetType().Name.Contains("GoogleApiException"))
        {
            // Handling Google.GenAI or similar
            technicalDetails = $"API Specific Error: {ex.Message}";
            
            var msg = ex.Message.ToLowerInvariant();
            if (msg.Contains("quota") || msg.Contains("limit") || msg.Contains("429"))
            {
                userMessage = $"Превышена квота или лимит запросов у {providerName}.";
            }
            else if (msg.Contains("key") || msg.Contains("auth") || msg.Contains("401"))
            {
                userMessage = $"Проблема с API ключом провайдера {providerName}.";
            }
            else if (msg.Contains("not found") || msg.Contains("404"))
            {
                userMessage = $"Ресурс или модель не найдены у провайдера {providerName}.";
            }
        }

        return new AiErrorDetails(userMessage, technicalDetails);
    }

    public static void LogDetailedError(ILogger? logger, Exception ex, string providerName, string methodName)
    {
        var details = GetErrorDetails(ex, providerName);
        logger?.LogError(ex, "Provider {Provider} failed in {Method}. UserMessage: {UserMessage}. TechnicalDetails: {TechnicalDetails}", 
            providerName, methodName, details.UserMessage, details.TechnicalDetails);
    }

    public static Exception HandleAndGetException(ILogger logger, Exception ex, string providerName, string methodName)
    {
        LogDetailedError(logger, ex, providerName, methodName);
        var details = GetErrorDetails(ex, providerName);
        return new AiProviderException(details.UserMessage, details.TechnicalDetails, providerName, ex);
    }
}
