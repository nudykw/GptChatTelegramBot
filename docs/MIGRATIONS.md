# Database Migrations Guide

This project supports **four** database providers: SQLite, PostgreSQL, MySql, and SQL Server.
To keep them synchronized, migrations must be generated for each provider separately.

## Summary: Multi-Provider Setup
- **Custom Migrations Assembly**: `Internal/ProviderSpecificMigrationsAssembly.cs` handles loading the correct migrations and snapshots based on the active provider.
- **Dedicated Folders**: Each provider has its own folder in `Migrations/`.
- **Naming Rule**: Migrations **must** have a suffix (e.g., `_Sqlite`, `_Postgres`) to avoid class naming conflicts in the common assembly.

---

## 🚀 Adding a New Migration

The easiest way to add a migration is using the provided helper scripts:

- **Bash (Linux/macOS)**: `./migr.sh add Initial`
- **PowerShell (Windows/Cross-platform)**: `./migr.ps1 add Initial`

This will automatically:
1. Run `dotnet ef migrations add` for all 4 providers.
2. Append the correct suffixes (`_Sqlite`, `_Postgres`, etc.).
3. Place them in the correct directories and namespaces.

### Manual Approach
If you want to add a migration for just one specific provider (not recommended for production):
```bash
dotnet ef migrations add MyChange_Sqlite \
    --project DataBaseLayer/ \
    --startup-project TelegramBotApp/ \
    --output-dir Migrations/Sqlite \
    --namespace DataBaseLayer.Migrations.Sqlite \
    -- Sqlite
```

---

## 🛠️ Typical Tasks

### Applying Migrations
- **Local SQLite**: `dotnet ef database update --project DataBaseLayer/ --startup-project TelegramBotApp/`
- **Other Providers**: Set the corresponding provider and connection string in `appsettings.json` and run the same command.
- **Using Helper Scripts**: 
  - Bash: `./migr.sh update`
  - PowerShell: `./migr.ps1 update`

### Removing the Last Migration
```bash
./migr.sh remove
# OR
./migr.ps1 remove
```

### Listing Migrations
```bash
./migr.sh list
# OR
./migr.ps1 list
```

---

## ⚠️ Important Rules
1. **Always Update All Providers**: If you change the model, you *must* add migrations for all providers to keep them in sync.
2. **Naming**: Use camel-case names. The script will handle suffixes automatically.
3. **Snapshot Isolation**: Never manually move or rename the `*StoreContextModelSnapshot.cs` files unless you are also updating the names in `ProviderSpecificMigrationsAssembly.cs`.
