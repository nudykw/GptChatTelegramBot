# Configuration
$Project = "DataBaseLayer/"
$StartupProject = "TelegramBotApp/"

# Provider mapping: Folder/Namespace suffix
$Providers = @{
    "Sqlite"     = "Sqlite"
    "PostgreSql" = "Postgres"
    "MySql"      = "MySql"
    "SqlServer"  = "SqlServer"
}

function Show-Usage {
    Write-Host "Usage: ./migr.ps1 [add|remove|update|list] [MigrationName]"
    Write-Host "Examples:"
    Write-Host "  ./migr.ps1 add MyNewMigration"
    Write-Host "  ./migr.ps1 remove"
    Write-Host "  ./migr.ps1 update"
}

if ($args.Count -lt 1) {
    Show-Usage
    exit 1
}

$Action = $args[0]

if ($Action -eq "add") {
    if ($args.Count -lt 2) {
        Write-Host "Error: Migration name is required for 'add'" -ForegroundColor Red
        Show-Usage
        exit 1
    }
    $Name = $args[1]
    foreach ($p in $Providers.Keys) {
        $suffix = $Providers[$p]
        $MigrationName = "${Name}_$suffix"
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
    }
    Write-Host "Successfully added migrations for all providers." -ForegroundColor Green

} elseif ($Action -eq "remove") {
    foreach ($p in $Providers.Keys) {
        Write-Host "=== Removing last migration for $p ===" -ForegroundColor Cyan
        dotnet ef migrations remove `
            --project "$Project" `
            --startup-project "$StartupProject" `
            -- "$p"
    }

} elseif ($Action -eq "update") {
    Write-Host "=== Updating database (using default config) ===" -ForegroundColor Cyan
    dotnet ef database update --project "$Project" --startup-project "$StartupProject"

} elseif ($Action -eq "list") {
    foreach ($p in $Providers.Keys) {
        Write-Host "=== Migrations for $p ===" -ForegroundColor Cyan
        dotnet ef migrations list --project "$Project" --startup-project "$StartupProject" -- "$p"
    }

} else {
    Show-Usage
    exit 1
}
