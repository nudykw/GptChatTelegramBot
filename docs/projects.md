# Project Descriptions

This document provides an overview of each project within the **GPTChatTelegramBot** solution.

## [TelegramBotApp](../TelegramBotApp)
**The Main Application Host**

This is the entry point of the application. It is responsible for:
- **Bot Initialization**: Setting up the `ITelegramBotClient` and configuring polling to receive messages.
- **Dependency Injection**: Orchestrating how services from `ServiceLayer` and `DataBaseLayer` are registered and used.
- **Configuration Management**: Reading and applying settings from `appsettings.json`.

---

## [ServiceLayer](../ServiceLayer)
**The Business Logic Heart**

Everything related to processing user requests happens here. Key responsibilities include:
- **AI Integrations**: Orchestrating calls to OpenAI, Gemini, and other providers via `IChatService`.
- **Factory Pattern**: Centralized service creation through `IChatServiceFactory` based on dynamic configuration.
- **Failover & Resilience**: Automatic provider switching via `ResilientChatService` wrapper.
- **Billing Logic**: Calculating the cost of AI usage (tokens) and preparing billing items for the database.
- **Audio Processing**: Handling voice-to-text transcription via Whisper/OpenAI.
- **Service Abstractions**: Defining the `IChatService` and other interfaces to ensure modularity.

### [Service Configuration](../TelegramBotApp/appsettings.sample.json)

The bot supports multiple AI providers. You can configure them in `appsettings.json`:

```json
"AppSettings": {
  "ChatProviders": [
    {
      "Name": "OpenAI",
      "ProviderType": "OpenAI",
      "ApiKey": "sk-...",
      "ModelName": "gpt-4o-mini"
    },
    {
      "Name": "Gemini",
      "ProviderType": "Gemini",
      "ApiKey": "AIza...",
      "ModelName": "gemini-pro"
    }
  ]
}
```

> [!TIP]
> Providers with API keys containing placeholders like `[YOUR_GEMINI_API_KEY]` will be automatically ignored by the `ChatServiceFactory`, so you can keep them in the configuration until you're ready to add the real keys.

---

## [DataBaseLayer](../DataBaseLayer)
**Data Persistence Layer**

Manages the SQLite database and all operations related to it.
- **Models**: Defines the data structure for `TelegramUserInfo`, `HistoryMessage`, and `GptBilingItem`.
- **Migrations**: Contains the EF Core migration history to keep the database schema up to date.
- **Context**: The `StoreContext` class which serves as the primary interface for database operations.

---

## [GeminiChat.DotNet](../ServiceLayer/Services/GeminiChat.DotNet)
**Internal Gemini Client**

A specialized project located within the `ServiceLayer` structure that focuses exclusively on the Google Gemini API.
- Provides a clean HTTP client wrapper for Gemini.
- Handles the specific JSON request/response formats required by Google.
