# Core Files Description

This document highlights the most important files in the repository to help you understand the application's flow and logic.

## [Program.cs](../TelegramBotApp/Program.cs)
**The Main Executable**
- Located in `TelegramBotApp`.
- Sets up the dependency injection container.
- Configures how services like `IChatService` and bot polling are wired together and started.

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
- Highly useful for accessing secrets and limits across the entire solution.

## [ChatGeminiService.cs](../ServiceLayer/Services/GeminiChat.DotNet/ChatGeminiService.cs)
**Google AI Implementation**
- Bridges the gaps between the generic `IChatService` and Google's Gemini-specific API structure.
- Handles the conversion of message history into a format Gemini can digest.
