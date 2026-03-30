#!/bin/bash

# Configuration
PROJECT="DataBaseLayer/"
STARTUP_PROJECT="TelegramBotApp/"

# Provider mapping: Folder/Namespace suffix
declare -A PROVIDERS
PROVIDERS["Sqlite"]="Sqlite"
PROVIDERS["PostgreSql"]="Postgres"
PROVIDERS["MySql"]="MySql"
PROVIDERS["SqlServer"]="SqlServer"

function usage {
    echo "Usage: ./migr.sh [add|remove|update|list] [MigrationName]"
    echo "Examples:"
    echo "  ./migr.sh add MyNewMigration"
    echo "  ./migr.sh remove"
    echo "  ./migr.sh update"
    exit 1
}

if [ "$1" == "add" ]; then
    if [ -z "$2" ]; then
        echo "Error: Migration name is required for 'add'"
        usage
    fi
    NAME=$2
    for p in "${!PROVIDERS[@]}"; do
        suffix=${PROVIDERS[$p]}
        MIGRATION_NAME="${NAME}_${suffix}"
        echo "=== Adding migration '$MIGRATION_NAME' for $p ==="
        dotnet ef migrations add "$MIGRATION_NAME" \
            --project "$PROJECT" \
            --startup-project "$STARTUP_PROJECT" \
            --output-dir "Migrations/$suffix" \
            --namespace "DataBaseLayer.Migrations.$suffix" \
            -- "$p"
        if [ $? -ne 0 ]; then
            echo "Error adding migration for $p"
            exit 1
        fi
    done
    echo "Successfully added migrations for all providers."

elif [ "$1" == "remove" ]; then
    for p in "${!PROVIDERS[@]}"; do
        echo "=== Removing last migration for $p ==="
        dotnet ef migrations remove \
            --project "$PROJECT" \
            --startup-project "$STARTUP_PROJECT" \
            -- "$p"
    done

elif [ "$1" == "update" ]; then
    # This applies migrations to the currently configured database (usually Sqlite locally)
    # Use carefully if you want to update a specific provider
    echo "=== Updating database (using default config) ==="
    dotnet ef database update --project "$PROJECT" --startup-project "$STARTUP_PROJECT"

elif [ "$1" == "list" ]; then
    for p in "${!PROVIDERS[@]}"; do
        echo "=== Migrations for $p ==="
        dotnet ef migrations list --project "$PROJECT" --startup-project "$STARTUP_PROJECT" -- "$p"
    done

else
    usage
fi
