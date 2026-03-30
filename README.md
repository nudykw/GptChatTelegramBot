<div align="center">

🇺🇸 English | 🇺🇦 [Українська](README.uk.md)

</div>

# GPTChatTelegramBot

The bot lets you chat with an AI via a Telegram bot, ask questions, and generate images. The chat history is saved to help you keep track of the conversation. You can also view your billing information.

---

## 🚀 Getting Started

Before compiling the project, add your keys to the `TelegramBotApp/appsettings.sample.json` file and rename it to `appsettings.json`.

> [!IMPORTANT]
> You **must** fill the `OwnerId` field with your Telegram User ID. To find your ID, use the [@userinfobot](https://t.me/userinfobot) bot in Telegram. Simply send a message to it and copy the numeric **ID**.


### 📝 Configuration Example

```json
{
  "AppSettings": {
    "TelegramBotConfiguration": {
      "BotToken": "YOUR_TELEGRAM_BOT_TOKEN",
      "OwnerId": "YOUR_OWNER_ID"
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

## 🤖 Bot Commands

- `/billing` — Show usage statistics. **(Admins & Owner only)**
- `/model` — Select AI model for responses.
- `/provider` — Select AI provider strategy (Auto/OpenAI/Gemini).
- `/draw <prompt>` — Generate an image from a text description. You can also just send a text prompt to generate an image. **<Required>**
- `/lang [language]` — Change the bot's interface language. **[Optional]**
- `/help` — Show the list of available commands.
- `/users_balance` — Show all users' balances. **(Admins only)**
- `/set_balance <userId> <amount>` — Set a user's balance. Format: `/set_balance 1234567 150.5`. **(Admins only)**
- `/restart` — Restart the bot service. **(Admins & Owner only)**

> [!TIP]
> **Group Conversations**: To maintain the conversation context (thread) in groups, you must **reply** to the bot's messages. Otherwise, the bot will treat each message as the start of a new conversation.

> [!TIP]
> If a command requires a parameter and you don't provide it (e.g., `/draw`), the bot will either show an error or a help message depending on the command logic.

## 🔑 Registration & API Keys

### 1. Telegram Bot
- Open [Telegram](https://t.me/BotFather) and find `@BotFather`.
- Send `/newbot` and follow the instructions to get your **Bot Token**.

### 2. Owner ID
To restrict administrative commands (like `/billing` and `/restart`) and identify yourself as the owner, you must fill the `OwnerId` field:
- Open [Telegram](https://t.me/userinfobot) and find `@userinfobot`.
- Send any message to it or just start it to get your **ID**.
- Copy the numeric ID and put it into the `TelegramBotConfiguration.OwnerId` field in `appsettings.json` (replacing `YOUR_OWNER_ID`).

### 3. OpenAI API Key
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

## 🗄️ Database & Migrations

The project supports multiple database providers (SQLite, PostgreSQL, MySQL, SQL Server). For information on how to manage migrations and switch between providers, see the [Migrations Documentation](docs/MIGRATIONS.md).

## 💳 Billing System

The bot includes a built-in billing system to manage AI usage costs.

### ⚙️ How it works:
- **Exempt Users**: The `OwnerId` and any IDs in `IgnoredBalanceUserIds` have unlimited access.
- **Consumption**: Each AI interaction (chat or image generation) deducts from the user's `Balance`.
- **Replenishment**: If a user's balance reaches 0, it is automatically replenished to the `InitialBalance` value **24 hours after their last interaction**.
- **Admin Management**: Admins can manually view all balances with `/users_balance` and adjust them with `/set_balance`.

### 🛡️ Disabling Billing
To disable the billing system entirely and allow all users unlimited access, set `InitialBalance` to `0` in your `appsettings.json`:

```json
"TelegramBotConfiguration": {
  "InitialBalance": 0
}
```

