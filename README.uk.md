<div align="center">

🇺🇸 [English](README.md) | 🇺🇦 Українська

</div>

# GPTChatTelegramBot

Бот дозволяє спілкуватися з штучним інтелектом через Telegram-бота, ставити запитання та створювати зображення. Історія спілкування зберігається для відстеження контексту. Є можливість переглянути білінг.

---

## 🚀 Швидкий старт

Перед компіляцією проєкту додайте свої ключі до файлу `TelegramBotApp/appsettings.sample.json` і перейменуйте його на `appsettings.json`.

### 📝 Приклад конфігурації

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

## 🤖 Команди бота

У боті доступні наступні команди. Більшість команд пропонують інтерактивні меню, але деякі приймають параметри безпосередньо.

| Команда | Синтаксис | Параметр | Обов'язково | Опис | Приклад |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `/billing` | `/billing` | Немає | - | Показує меню вибору місяця для статистики використання. | `/billing` |
| `/model` | `/model` | Немає | - | Показує меню вибору моделі ШІ для майбутніх відповідей. | `/model` |
| `/provider` | `/provider` | Немає | - | Показує меню для вибору стратегії провайдера (**Авторотація** або **Конкретний**). | `/provider` |
| `/draw` | `/draw <опис>` | `<опис>` | **&lt;Так&gt;** | Генерує зображення на основі текстового опису. | `/draw кібер-кіт у космосі` |
| `/lang` | `/lang [language]` | `[language]` | [Ні] | Змінює мову інтерфейсу. Якщо параметр не вказано, відкривається меню вибору. | `/lang German` або `/lang` |
| `/help` | `/help` | Немає | - | Показує детальний список доступних команд та інструкції з їх використання. | `/help` |
| `/restart` | `/restart` | Немає | - | Завершує процес бота (корисно для Docker/Systemd перезапуску). | `/restart` |

> [!TIP]
> Якщо команда потребує параметр, але ви його не вказали (наприклад, `/draw`), бот або покаже помилку, або виведе меню допомоги.

## 🔑 Реєстрація та API ключі

### 1. Telegram Бот
- Відкрийте [Telegram](https://t.me/BotFather) і знайдіть `@BotFather`.
- Надішліть `/newbot` і дотримуйтесь інструкцій, щоб отримати **Bot Token**.

### 2. OpenAI API Key
- Перейдіть до [OpenAI Platform](https://platform.openai.com/api-keys).
- Створіть новий секретний ключ.

### 3. Google Gemini API Key
- Перейдіть до [Google AI Studio](https://aistudio.google.com/app/apikey).
- Створіть API ключ у новому проєкті.

### 4. xAI Grok API Key
- Перейдіть до [xAI Console](https://console.x.ai/).
- Створіть новий API ключ.
- У файлі `appsettings.json` встановіть `ProviderType` на `"OpenAI"` та `BaseUrl` на `https://api.x.ai/v1`.

### 5. DeepSeek API Key
- Перейдіть до [DeepSeek Platform](https://platform.deepseek.com/api_keys).
- Створіть новий API ключ.
- У файлі `appsettings.json` встановіть `ProviderType` на `"OpenAI"` та `BaseUrl` на `https://api.deepseek.com`.
