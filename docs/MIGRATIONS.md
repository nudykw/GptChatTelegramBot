# 🗄️ Database Migrations Guide

🇺🇸 English | 🇺🇦 [Українська](MIGRATIONS.uk.md)

This project supports **four** database providers: **SQLite**, **PostgreSQL**, **MySQL**, and **SQL Server**.
Because each provider requires its own SQL dialect, migrations are generated separately for each one and stored in dedicated folders under `DataBaseLayer/Migrations/`.

---

## Two Application Variants

The solution contains **two runnable applications** that both use `DataBaseLayer`:

| Project | Type | Purpose |
|---|---|---|
| `TelegramBotApp/` | Console app | Local development, simple deployment |
| `TelegramBotWebApp/` | ASP.NET Core Web API | Docker deployment, Webhook mode, Prometheus metrics, Swagger |

Both projects share the **same `DataBaseLayer`** and the **same migrations**. The migration scripts use `TelegramBotApp/` as the startup project by default (it has `appsettings.json` with all required keys). You can also point the CLI at `TelegramBotWebApp/` — the result is identical since `DataBaseLayer` is what matters.

> [!NOTE]
> Migrations are applied **automatically on startup** in both applications via `MigrateAsync()` / `MigrationConfigurator.ApplyMigrations()`, so you never need to run `database update` manually in production.

---

## Prerequisites

Before working with migrations, make sure the EF Core CLI tool is installed:

```bash
dotnet tool restore
```

> [!NOTE]
> The tool is declared in `dotnet-tools.json` so `dotnet tool restore` is all you need. No global install required.

---

## How It Works

### Automatic Migrations on Startup

The application calls `MigrateAsync()` automatically at startup, so you **do not** need to run `dotnet ef database update` manually in production or in Docker. The correct provider is selected based on `DB_PROVIDER` (in `.env` / `appsettings.json`), and only the matching set of migrations is applied.

### Multi-Provider Architecture

| Concept | Detail |
|---|---|
| **Migrations folder** | `DataBaseLayer/Migrations/<Provider>/` |
| **Namespace** | `DataBaseLayer.Migrations.<Provider>` |
| **Suffix rule** | Every migration class name **must** end with `_Sqlite`, `_Postgres`, `_MySql`, or `_SqlServer` |
| **Snapshot isolation** | Each provider keeps its own `StoreContextModelSnapshot` saved as `.snapshot_saved` |
| **Assembly router** | `DataBaseLayer/Internal/ProviderSpecificMigrationsAssembly.cs` loads the correct migrations at runtime |

---

## 🚀 First Migration (Initial Setup)

Run this once when you set up the project for the first time or after cloning the repository. It creates the initial database schema for **all four providers** simultaneously.

### Using the helper script *(recommended)*

**Bash (Linux / macOS):**
```bash
./migr.sh add Initial
```

**PowerShell (Windows / cross-platform):**
```powershell
./migr.ps1 add Initial
```

The script will:
1. Iterate over all 4 providers in order: `Sqlite → PostgreSql → MySql → SqlServer`.
2. For each provider, run `dotnet ef migrations add Initial_<Suffix>` with the correct `--output-dir` and `--namespace`.
3. Save the generated `ModelSnapshot` per-provider so subsequent migrations can build on top of it.

After this command finishes you should see four new files (one per provider) in `DataBaseLayer/Migrations/`:

```
DataBaseLayer/Migrations/
├── Sqlite/
│   └── 20240101000000_Initial_Sqlite.cs
├── Postgres/
│   └── 20240101000000_Initial_Postgres.cs
├── MySql/
│   └── 20240101000000_Initial_MySql.cs
└── SqlServer/
    └── 20240101000000_Initial_SqlServer.cs
```

> [!IMPORTANT]
> Migrations are already included in the repository. You only need to run `add Initial` if you are starting completely from scratch (e.g., you deleted the `Migrations/` folder).

---

## ➕ Subsequent Migrations (Schema Changes)

Every time you modify an EF Core entity (add a property, rename a column, add a new table, etc.), you must create a new migration for **all providers**.

```bash
# Bash
./migr.sh add DescriptiveName

# PowerShell
./migr.ps1 add DescriptiveName
```

Replace `DescriptiveName` with a short, camel-case description of the change, for example:

```bash
./migr.sh add AddUserLanguageField
./migr.sh add RenameBalanceColumn
./migr.sh add AddMessageIndexes
```

The script appends the provider suffix automatically:
- `AddUserLanguageField_Sqlite`
- `AddUserLanguageField_Postgres`
- `AddUserLanguageField_MySql`
- `AddUserLanguageField_SqlServer`

> [!WARNING]
> **Never** add a migration for only one provider — all four must stay in sync. If you need to test quickly, still generate for all providers and only apply the one you need locally.

---

## 🛠️ Applying Migrations Manually

Migrations are applied automatically at startup, but you can also apply them manually:

### Apply to the currently configured provider
```bash
# Bash
./migr.sh update

# PowerShell
./migr.ps1 update
```

### Apply with the EF CLI directly (SQLite example)
```bash
dotnet ef database update \
    --project DataBaseLayer/ \
    --startup-project TelegramBotApp/
```

### Apply for a specific provider
Set `DB_PROVIDER` and `DB_CONNECTION_STRING` in `.env` / `appsettings.json` first, then run:
```bash
dotnet ef database update \
    --project DataBaseLayer/ \
    --startup-project TelegramBotApp/ \
    -- PostgreSql
```

> [!TIP]
> If you prefer to use the **web app** as the startup project, replace `TelegramBotApp/` with `TelegramBotWebApp/` in any of the commands above. Both produce identical migration results since the database schema is defined entirely in `DataBaseLayer`.

---

## 🗑️ Removing the Last Migration

Use this only if the migration has **not** been applied to any real database yet (e.g., you made a mistake right after generating it):

```bash
# Bash
./migr.sh remove

# PowerShell
./migr.ps1 remove
```

This removes the last migration for all four providers and restores their snapshots to the previous state.

---

## 📋 Listing Migrations

```bash
# Bash
./migr.sh list

# PowerShell
./migr.ps1 list
```

---

## ✏️ Manual Migration (Single Provider)

If you need to add a migration for just one provider (not recommended unless debugging):

```bash
dotnet ef migrations add MyChange_Sqlite \
    --project DataBaseLayer/ \
    --startup-project TelegramBotApp/ \
    --output-dir Migrations/Sqlite \
    --namespace DataBaseLayer.Migrations.Sqlite \
    -- Sqlite
```

Replace `Sqlite` / `_Sqlite` with the target provider (`PostgreSql` / `_Postgres`, `MySql` / `_MySql`, `SqlServer` / `_SqlServer`).

---

## ⚠️ Important Rules

1. **Always update all providers.** Any model change must produce migrations for all 4 providers. Use the helper scripts to ensure this.
2. **Use descriptive camel-case names.** The script adds suffixes automatically — do not include them in the name you pass.
3. **Never manually rename snapshot files.** If you must reorganize snapshots, also update `ProviderSpecificMigrationsAssembly.cs`.
4. **Commit migrations to the repository.** Migrations are part of the source code and must be version-controlled together with the model changes that triggered them.
5. **Do not mix schema changes in one migration.** One logical change = one migration. This keeps history clean and rollbacks safe.

---

## 🔍 Troubleshooting

| Problem | Solution |
|---|---|
| `No migrations found` at startup | Run `./migr.sh add Initial` to generate the initial migration |
| `Snapshot mismatch` error | Delete stale `.snapshot_saved` files and re-run `add` from a clean state |
| Migration applied to wrong provider | Check `DB_PROVIDER` in `.env` / `appsettings.json` |
| EF CLI not found | Run `dotnet tool restore` from the repository root |
| `Unable to create DbContext` during migration | Ensure `TelegramBotApp/` builds successfully and `appsettings.json` exists |
