<div align="center">

🇺🇸 [English](README.md) | 🇺🇦 Українська

</div>

# GPTChatTelegramBot

Бот дозволяє спілкуватися з штучним інтелектом через Telegram-бота, ставити запитання та створювати зображення. Історія спілкування зберігається для відстеження контексту. Є можливість переглянути білінг.

---

## 🚀 Швидкий старт

Перед компіляцією проєкту додайте свої ключі до файлу `TelegramBotApp/appsettings.sample.json` і перейменуйте його на `appsettings.json`.

> [!IMPORTANT]
> Ви **обов'язково** повинні заповнити поле `OwnerId` своїм Telegram User ID. Щоб дізнатися свій ID, скористайтеся ботом [@userinfobot](https://t.me/userinfobot) у Telegram. Просто надішліть йому будь-яке повідомлення та скопіюйте числовий **ID**.


### 📝 Приклад конфігурації

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

## 🤖 Команди бота

У боті доступні наступні команди.

- `/billing` — Статистика використання. **(Адміни та Власник)**
- `/model` — Вибір моделі ШІ для відповідей.
- `/provider` — Вибір стратегії провайдера (Авторотація/OpenAI/Gemini).
- `/draw <опис>` — Генерація зображення на основі текстового опису. Також можна просто надіслати текст, і зображення буде згенеровано. **<Обов'язково>**
- `/lang [language]` — Зміна мови інтерфейсу. **[Опціонально]**
- `/help` — Показати список доступних команд.
- `/restart` — Перезапуск сервісу бота. **(Адміни та Власник)**

> [!TIP]
> **Розмови у групах**: Щоб бот зберігав нить спілкування (контекст) у групах, необхідно **відповідати** (цитувати) повідомлення бота. Інакше бот вважатиме кожне повідомлення початком нової розмови.

> [!TIP]
> Якщо команда потребує параметрів і ви їх не надали (наприклад, `/draw`), бот або покаже помилку, або виведе довідку, залежно від логіки команди.

## 🔑 Реєстрація та API ключі

### 1. Telegram Бот
- Відкрийте [Telegram](https://t.me/BotFather) і знайдіть `@BotFather`.
- Надішліть `/newbot` і дотримуйтесь інструкцій, щоб отримати **Bot Token**.

### 2. Owner ID
Щоб обмежити доступ до адміністративних команд (таких як `/billing` та `/restart`) та ідентифікувати себе як власника, вам обов'язково потрібно заповнити поле `OwnerId`:
- Відкрийте [Telegram](https://t.me/userinfobot) і знайдіть `@userinfobot`.
- Надішліть йому будь-яке повідомлення або просто запустіть його, щоб отримати свій **ID**.
- Скопіюйте отриманий числовий ID та вставте його у поле `TelegramBotConfiguration.OwnerId` у файлі `appsettings.json` (замість `YOUR_OWNER_ID`).

### 3. OpenAI API Key
- Перейдіть до [OpenAI Platform](https://platform.openai.com/api-keys).
- Створіть новий секретний ключ.

### 4. Google Gemini API Key
- Перейдіть до [Google AI Studio](https://aistudio.google.com/app/apikey).
- Створіть API ключ у новому проєкті.

### 5. xAI Grok API Key
- Перейдіть до [xAI Console](https://console.x.ai/).
- Створіть новий API ключ.
- У файлі `appsettings.json` встановіть `ProviderType` на `"OpenAI"` та `BaseUrl` на `https://api.x.ai/v1`.

### 6. DeepSeek API Key
- Перейдіть до [DeepSeek Platform](https://platform.deepseek.com/api_keys).
- Створіть новий API ключ.
