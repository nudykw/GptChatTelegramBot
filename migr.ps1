# Configuration
$Project        = "DataBaseLayer/"
$StartupProject = "TelegramBotApp/"

# Ordered list — determines generation sequence.
$ProviderKeys = @("Sqlite", "PostgreSql", "MySql", "SqlServer")
$Providers = @{
    "Sqlite"     = "Sqlite"
    "PostgreSql" = "Postgres"
    "MySql"      = "MySql"
    "SqlServer"  = "SqlServer"
}

$MigrationsRoot = "DataBaseLayer/Migrations"
# EF sometimes writes snapshots into a double-nested path — we watch both.
$NestedRoot     = "DataBaseLayer/DataBaseLayer/Migrations"

function Show-Usage {
    Write-Host "Usage: ./migr.ps1 [add|remove|update|list] [MigrationName]"
    Write-Host "Examples:"
    Write-Host "  ./migr.ps1 add MyNewMigration"
    Write-Host "  ./migr.ps1 remove"
    Write-Host "  ./migr.ps1 update"
    Write-Host "  ./migr.ps1 list"
}

# Find the snapshot EF generated for a given suffix (searches both root paths).
function Find-Snapshot {
    param([string]$Suffix)
    $dirs = @(
        (Join-Path $MigrationsRoot $Suffix),
        (Join-Path $NestedRoot     $Suffix)
    )
    foreach ($d in $dirs) {
        if (Test-Path $d) {
            $f = Get-ChildItem $d -Filter "*Snapshot*.cs" -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($f) { return $f.FullName }
        }
    }
    return $null
}

# Remove ALL snapshot files from both root paths.
function Remove-AllSnapshots {
    foreach ($root in @($MigrationsRoot, $NestedRoot)) {
        if (Test-Path $root) {
            Get-ChildItem $root -Recurse -Filter "*Snapshot*.cs" -ErrorAction SilentlyContinue |
                Remove-Item -Force
        }
    }
}

# Restore any previously saved snapshot for this suffix.
function Restore-Snapshot {
    param([string]$Suffix)
    $saved = Join-Path $MigrationsRoot "$Suffix/.snapshot_saved"
    if (Test-Path $saved) {
        $dest = Join-Path $MigrationsRoot "$Suffix/StoreContextModelSnapshot.cs"
        Copy-Item $saved $dest -Force
        Write-Host "--- Restored snapshot for $Suffix ---" -ForegroundColor DarkGray
    }
}

# Save EF's generated snapshot and clean workspace so it doesn't bleed into other providers.
function Save-Snapshot {
    param([string]$Suffix)
    $snap = Find-Snapshot -Suffix $Suffix
    if ($snap) {
        $dest = Join-Path $MigrationsRoot "$Suffix/.snapshot_saved"
        Copy-Item $snap $dest -Force
        Write-Host "--- Saved snapshot for $Suffix ---" -ForegroundColor DarkGray
    }
    Remove-AllSnapshots
}

if ($args.Count -lt 1) {
    Show-Usage
    exit 1
}

$Action = $args[0]

# ── ADD ──────────────────────────────────────────────────────────────────────
if ($Action -eq "add") {
    if ($args.Count -lt 2) {
        Write-Host "Error: Migration name is required for 'add'" -ForegroundColor Red
        Show-Usage
        exit 1
    }
    $Name = $args[1]

    foreach ($p in $ProviderKeys) {
        $suffix        = $Providers[$p]
        $MigrationName = "${Name}_$suffix"

        # 1. Remove all snapshots so other providers don't bleed in.
        Remove-AllSnapshots

        # 2. Restore THIS provider's saved snapshot (exists only after first migration).
        Restore-Snapshot -Suffix $suffix

        Write-Host "=== Adding migration '$MigrationName' for $p ===" -ForegroundColor Cyan
        dotnet ef migrations add "$MigrationName" `
            --project "$Project" `
            --startup-project "$StartupProject" `
            --output-dir "Migrations/$suffix" `
            --namespace "DataBaseLayer.Migrations.$suffix" `
            -- "$p"

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error adding migration for $p" -ForegroundColor Red
            exit 1
        }

        # 3. Save EF's updated snapshot and clean workspace.
        Save-Snapshot -Suffix $suffix
    }
    Write-Host "Successfully added migrations for all providers." -ForegroundColor Green

# ── REMOVE ───────────────────────────────────────────────────────────────────
} elseif ($Action -eq "remove") {
    foreach ($p in $ProviderKeys) {
        $suffix = $Providers[$p]
        Write-Host "=== Removing last migration for $p ===" -ForegroundColor Cyan
        Remove-AllSnapshots
        Restore-Snapshot -Suffix $suffix
        dotnet ef migrations remove `
            --project "$Project" `
            --startup-project "$StartupProject" `
            -- "$p"
        Save-Snapshot -Suffix $suffix
    }

# ── UPDATE ───────────────────────────────────────────────────────────────────
} elseif ($Action -eq "update") {
    Write-Host "=== Updating database (using default config) ===" -ForegroundColor Cyan
    dotnet ef database update --project "$Project" --startup-project "$StartupProject"

# ── LIST ─────────────────────────────────────────────────────────────────────
} elseif ($Action -eq "list") {
    foreach ($p in $ProviderKeys) {
        Write-Host "=== Migrations for $p ===" -ForegroundColor Cyan
        dotnet ef migrations list `
            --project "$Project" `
            --startup-project "$StartupProject" `
            -- "$p"
    }

} else {
    Write-Host "Unknown action: $Action" -ForegroundColor Red
    Show-Usage
    exit 1
}
