<div align="center">

🇺🇸 English | 🇺🇦 [Українська](README.uk.md)

</div>

# GPTChatTelegramBot

The bot lets you chat with an AI via a Telegram bot, ask questions, and generate images. The chat history is saved to help you keep track of the conversation. You can also view your billing information.

---

## 🚀 Getting Started

Before compiling the project, add your keys to the `TelegramBotApp/appsettings.sample.json` file and rename it to `appsettings.json`.

### 📝 Configuration Example

```json
{
  "AppSettings": {
    "TelegramBotConfiguration": {
      "BotToken": "YOUR_TELEGRAM_BOT_TOKEN"
    },
    "ChatProviders": [
      {
        "Name": "OpenAI",
        "ProviderType": "OpenAI",
        "ApiKey": "YOUR_OPENAI_API_KEY",
        "ModelName": "gpt-4o-mini"
      },
      {
        "Name": "Gemini",
        "ProviderType": "Gemini",
        "ApiKey": "YOUR_GEMINI_API_KEY",
        "ModelName": "gemini-pro"
      },
      {
        "Name": "Grok",
        "ProviderType": "OpenAI",
        "ApiKey": "YOUR_GROK_API_KEY",
        "BaseUrl": "https://api.x.ai/v1"
      },
      {
        "Name": "DeepSeek",
        "ProviderType": "OpenAI",
        "ApiKey": "YOUR_DEEPSEEK_API_KEY",
        "BaseUrl": "https://api.deepseek.com"
      }
    ]
  }
}
```

## 🔑 Registration & API Keys

### 1. Telegram Bot
- Open [Telegram](https://t.me/BotFather) and find `@BotFather`.
- Send `/newbot` and follow the instructions to get your **Bot Token**.

### 2. OpenAI API Key
- Go to [OpenAI Platform](https://platform.openai.com/api-keys).
- Create a new secret key.

### 3. Google Gemini API Key
- Go to [Google AI Studio](https://aistudio.google.com/app/apikey).
- Create an API key in a new project.

### 4. xAI Grok API Key
- Go to [xAI Console](https://console.x.ai/).
- Create a new API key.
- In `appsettings.json`, set `ProviderType` to `"OpenAI"` and `BaseUrl` to `https://api.x.ai/v1`.

### 5. DeepSeek API Key
- Go to [DeepSeek Platform](https://platform.deepseek.com/api_keys).
- Create a new API key.
- In `appsettings.json`, set `ProviderType` to `"OpenAI"` and `BaseUrl` to `https://api.deepseek.com`.
