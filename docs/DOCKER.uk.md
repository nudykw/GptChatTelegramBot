# 🐳 Розгортання у Docker

🇺🇸 [English](DOCKER.md) | 🇺🇦 Українська

> [!NOTE]
> Цей посібник охоплює розгортання **веб-версії** бота (`TelegramBotWebApp`) через Docker Compose.  
> Щоб переглянути **всі** способи запуску (локально, консоль, systemd тощо), див. [Посібник з хостингу](./HOSTING.uk.md).

---

## Вимоги

- [Docker](https://docs.docker.com/get-docker/) ≥ 24
- [Docker Compose](https://docs.docker.com/compose/install/) ≥ 2.20 (плагін, не standalone)
- Публічна HTTPS-адреса для режиму Webhook *(опціонально — Polling працює без неї)*

---

## Швидкий старт

### 1. Клонуйте репозиторій

```bash
git clone https://github.com/nudykw/GptChatTelegramBot.git
cd GptChatTelegramBot
```

### 2. Створіть файл оточення

```bash
cp .env.example .env
```

Відкрийте `.env` та заповніть значення. Обов'язкові поля:

| Змінна | Опис |
|---|---|
| `TELEGRAM_BOT_TOKEN` | Токен від [@BotFather](https://t.me/BotFather) |
| `TELEGRAM_OWNER_ID` | Ваш Telegram User ID (отримайте у [@userinfobot](https://t.me/userinfobot)) |
| `POSTGRES_PASSWORD` | Надійний пароль для бази даних |
| `CLOUDBEAVER_ADMIN_PASSWORD` | Надійний пароль для CloudBeaver UI (≥ 8 символів) |

### 3. Оберіть базу даних та сервіси

Відредагуйте змінну `COMPOSE_PROFILES` у `.env`:

| Значення | Запущені сервіси |
|---|---|
| `postgres,cloudbeaver` | PostgreSQL + CloudBeaver + Бот *(за замовчуванням)* |
| `postgres` | PostgreSQL + Бот |
| `mysql,cloudbeaver` | MySQL + CloudBeaver + Бот |
| `mysql` | MySQL + Бот |
| `mssql,cloudbeaver` | SQL Server + CloudBeaver + Бот |
| `mssql` | SQL Server + Бот |
| *(порожньо)* | Тільки Бот *(для SQLite — контейнер не потрібен)* |

Також оновіть `DB_PROVIDER` та `DB_CONNECTION_STRING` відповідно до обраної БД.

### 4. Запустіть стек

```bash
docker compose up -d
```

### 5. Перевірте роботу

```bash
docker compose ps
curl http://localhost:8080/health
```

---

## Режими роботи бота: Polling та Webhook

Бот автоматично визначає режим на основі змінної `TELEGRAM_BASE_API_URL` у `.env`.

### Polling *(публічна URL-адреса не потрібна)*

Залиште `TELEGRAM_BASE_API_URL` порожнім:

```dotenv
TELEGRAM_BASE_API_URL=
```

Бот підключиться до Telegram і сам опитуватиме оновлення кожні декілька секунд.

### Webhook *(потрібна публічна HTTPS URL-адреса)*

Вкажіть свій домен:

```dotenv
TELEGRAM_BASE_API_URL=https://yourdomain.com
```

Слеш наприкінці необов'язковий — `https://example.com` та `https://example.com/` обидва працюють.

При старті бот автоматично зареєструє вебхук:
```
POST https://yourdomain.com/aibot
```

> [!TIP]
> Для локального тестування Webhook можна використати [ngrok](https://ngrok.com/)  
> або [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/):
> ```bash
> cloudflared tunnel --url http://localhost:8080
> ```
> Після цього встановіть `TELEGRAM_BASE_API_URL` на згенерований HTTPS URL.

---

## Доступні ендпоінти

Після запуску бота доступні наступні HTTP-ендпоінти:

| Метод | URL | Опис |
|---|---|---|
| `GET` | `/health` | Перевірка стану — повертає `200 OK`, якщо сервіс працює |
| `GET` | `/api/info` | Інформація про бота (назва, режим, версія) |
| `POST` | `/aibot` | Приймач Telegram Webhook *(лише у Webhook режимі)* |
| `GET` | `/metrics` | Метрики Prometheus |
| `GET` | `/scalar` | Swagger / OpenAPI інтерактивна документація |
| `GET` | `/openapi/v1.json` | OpenAPI специфікація у форматі JSON |

---

## Провайдери баз даних

### PostgreSQL *(профіль: `postgres`)*

```dotenv
COMPOSE_PROFILES=postgres
DB_PROVIDER=PostgreSQL
DB_CONNECTION_STRING=Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
```

### MySQL *(профіль: `mysql`)*

```dotenv
COMPOSE_PROFILES=mysql
DB_PROVIDER=MySQL
DB_CONNECTION_STRING=Server=mysql;Port=3306;Database=${MYSQL_DB};User=${MYSQL_USER};Password=${MYSQL_PASSWORD}
```

### Microsoft SQL Server *(профіль: `mssql`)*

```dotenv
COMPOSE_PROFILES=mssql
DB_PROVIDER=SqlServer
DB_CONNECTION_STRING=Server=mssql,1433;Database=${MSSQL_DB};User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=true
```

> [!WARNING]
> Пароль SA для MSSQL повинен відповідати вимогам SQL Server:  
> ≥ 8 символів, з великими та малими літерами, цифрою **і** спеціальним символом.

### SQLite *(без контейнера)*

```dotenv
COMPOSE_PROFILES=          # залиште порожнім
DB_PROVIDER=Sqlite
DB_CONNECTION_STRING=Data Source=/data/bot.db
```

---

## CloudBeaver — веб-менеджер бази даних

CloudBeaver надає браузерний інтерфейс для перегляду та управління базою даних.

**Адреса**: `http://localhost:${CLOUDBEAVER_PORT}` (за замовчуванням: `http://localhost:8978`)

**Облікові дані** налаштовуються у `.env`:

```dotenv
CLOUDBEAVER_PORT=8978
CLOUDBEAVER_ADMIN_NAME=cbadmin          # логін адміністратора
CLOUDBEAVER_ADMIN_PASSWORD=yourpassword  # пароль (≥ 8 символів)
```

> [!NOTE]
> CloudBeaver є **опціональним**. Щоб запустити без нього, видаліть `cloudbeaver` з `COMPOSE_PROFILES`.

### Підключення CloudBeaver до бази даних

1. Відкрийте CloudBeaver у браузері
2. Увійдіть з `CLOUDBEAVER_ADMIN_NAME` / `CLOUDBEAVER_ADMIN_PASSWORD`
3. Натисніть **New Connection** та оберіть тип бази даних
4. Використайте **ім'я контейнера** як хост (наприклад, `postgres`, `mysql` або `mssql`)
5. Заповніть назву БД та облікові дані з вашого `.env`

---

## Корисні команди

```bash
# Запустити всі сервіси у фоновому режимі
docker compose up -d

# Переглянути логи (всіх сервісів)
docker compose logs -f

# Переглянути логи лише бота
docker compose logs -f bot

# Перезапустити бота
docker compose restart bot

# Зупинити все
docker compose down

# Зупинити та видалити томи (⚠️ видаляє дані бази даних)
docker compose down -v

# Перезбирати образ бота після змін у коді
docker compose up -d --build bot
```

---

## Довідник змінних оточення

Повний перелік усіх змінних з описами — у файлі [`.env.example`](../.env.example).
