# Core Files Description

This document highlights the most important files in the repository to help you understand the application's flow and logic.

## [Program.cs](../TelegramBotApp/Program.cs)
**The Main Executable**
- Located in `TelegramBotApp`.
- Sets up the dependency injection container.
- Configures how services like `IChatService` (registered as `ResilientChatService`) and bot polling are wired together and started.

## [ResilientChatService.cs](../ServiceLayer/Services/ResilientChatService.cs)
**The Resilience Wrapper**
- Located in `ServiceLayer`.
- Implements `IChatService`.
- Automatically fails over to the next available provider if the current one fails.
- Ensures high availability of the chat bot.

## [ChatServiceFactory.cs](../ServiceLayer/Services/ChatServiceFactory.cs)
**The Service Creator**
- Located in `ServiceLayer`.
- Dynamically instantiates the appropriate `IChatService` implementation (OpenAI or Gemini) based on name and configuration.
- **Intelligent Configuration Filtering**: Automatically ignores any chat provider configurations where the `ApiKey` contains a placeholder like `[YOUR_...]`, preventing errors from incomplete setups.

## [ChatProviderConfig.cs](../ServiceLayer/Services/ChatProviderConfig.cs)
**Provider Metadata**
- Defines the structure for a single AI provider (Name, Type, API Key, Base URL, Model Name).

## [ChatGptService.cs](../ServiceLayer/Services/GptChat/ChatGptService.cs)
**The Heart of GPT Integration**
- Located in `ServiceLayer`.
- Implements `IChatService` for OpenAI models.
- Manages the interaction with OpenAI's API (streaming text, using models like `gpt-4o-mini`).
- Calculates prices and generates billing items.

## [AudioTranscriptorService.cs](../ServiceLayer/Services/AudioTranscriptor/AudioTranscriptorService.cs)
**Voice Processing Core**
- Located in `ServiceLayer`.
- Handles voice message transcription using OpenAI's Whisper API.
- Converts audio files into text to be processed by the chat services.

## [StoreContext.cs](../DataBaseLayer/Contexts/StoreContext.cs)
**The Data Gateway**
- Located in `DataBaseLayer`.
- Extends `DbContext` and defines how everything is persisted in the SQLite database.
- Central point for managing chat history, user mapping, and billing stats.

## [AppSettings.cs](../ServiceLayer/AppSettings.cs)
**Configuration Structure**
- A POCO (Plain Old CLR Object) class that maps the `appsettings.json` content into a strongly-typed object.
- Includes the `ChatProviders` collection for multi-provider support.
- Highly useful for accessing secrets and limits across the entire solution.

## [ChatGeminiService.cs](../ServiceLayer/Services/GeminiChat.DotNet/ChatGeminiService.cs)
**Google AI Implementation**
- Bridges the gaps between the generic `IChatService` and Google's Gemini-specific API structure.
- Handles the conversion of message history into a format Gemini can digest.

## [BotCommands.cs](../ServiceLayer/Constans/BotCommands.cs)
**Command Registry**
- Centralized storage for all main bot commands (`/model`, `/provider`, `/billing`, `/restart`) and their Russian descriptions.
- Used for both message routing in `UpdateHandler` and automatic menu synchronization in `ReceiverServiceBase`.

## [ReceiverServiceBase.cs](../ServiceLayer/Services/Telegram/ReceiverServiceBase.cs)
**The Update Receiver**
- Now includes automatic command registration logic.
- Calls `SetMyCommandsAsync` upon bot startup to ensure the Telegram menu is always up-to-date with the supported commands.
