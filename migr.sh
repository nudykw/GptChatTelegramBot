#!/bin/bash

# Configuration
PROJECT="DataBaseLayer/"
STARTUP_PROJECT="TelegramBotApp/"

# Ordered list — determines generation sequence.
PROVIDER_KEYS=("Sqlite" "PostgreSql" "MySql" "SqlServer")
declare -A PROVIDERS
PROVIDERS["Sqlite"]="Sqlite"
PROVIDERS["PostgreSql"]="Postgres"
PROVIDERS["MySql"]="MySql"
PROVIDERS["SqlServer"]="SqlServer"

MIGRATIONS_ROOT="DataBaseLayer/Migrations"
# EF sometimes writes snapshots into a double-nested path — we watch both.
NESTED_ROOT="DataBaseLayer/DataBaseLayer/Migrations"

function usage {
    echo "Usage: ./migr.sh [add|remove|update|list] [MigrationName]"
    echo "Examples:"
    echo "  ./migr.sh add MyNewMigration"
    echo "  ./migr.sh remove"
    echo "  ./migr.sh update"
    echo "  ./migr.sh list"
    exit 1
}

# Find the snapshot file EF just generated for the given suffix (searches both root paths).
function find_snapshot {
    local suffix=$1
    local found
    found=$(find "$MIGRATIONS_ROOT/$suffix" "$NESTED_ROOT/$suffix" \
        -name "*Snapshot*.cs" -type f 2>/dev/null | head -1)
    echo "$found"
}

# Remove ALL snapshot files everywhere (both root paths, all suffixes).
function remove_all_snapshots {
    find "$MIGRATIONS_ROOT" -name "*Snapshot*.cs" -delete 2>/dev/null
    find "$NESTED_ROOT"     -name "*Snapshot*.cs" -delete 2>/dev/null
}

# Restore a previously saved snapshot for the given suffix into the standard location.
function restore_snapshot {
    local suffix=$1
    local saved="$MIGRATIONS_ROOT/$suffix/.snapshot_saved"
    if [ -f "$saved" ]; then
        cp "$saved" "$MIGRATIONS_ROOT/$suffix/StoreContextModelSnapshot.cs"
        echo "--- Restored snapshot for $suffix ---"
    fi
}

# Save EF's generated snapshot for the given suffix for future migrations.
function save_snapshot {
    local suffix=$1
    local snap
    snap=$(find_snapshot "$suffix")
    if [ -n "$snap" ]; then
        cp "$snap" "$MIGRATIONS_ROOT/$suffix/.snapshot_saved"
        echo "--- Saved snapshot for $suffix ---"
    fi
    # Clean from EF's actual output so it doesn't bleed into other providers
    remove_all_snapshots
}

# ── ADD ──────────────────────────────────────────────────────────────────────
if [ "$1" == "add" ]; then
    if [ -z "$2" ]; then
        echo "Error: Migration name is required for 'add'"
        usage
    fi
    NAME=$2

    for p in "${PROVIDER_KEYS[@]}"; do
        suffix=${PROVIDERS[$p]}
        MIGRATION_NAME="${NAME}_${suffix}"

        # 1. Remove all snapshots so other providers don't bleed in.
        remove_all_snapshots

        # 2. Restore THIS provider's saved snapshot (exists only after first migration).
        restore_snapshot "$suffix"

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

        # 3. Save the updated snapshot and clean workspace.
        save_snapshot "$suffix"
    done
    echo "Successfully added migrations for all providers."

# ── REMOVE ───────────────────────────────────────────────────────────────────
elif [ "$1" == "remove" ]; then
    for p in "${PROVIDER_KEYS[@]}"; do
        suffix=${PROVIDERS[$p]}
        echo "=== Removing last migration for $p ==="
        # Restore snapshot so EF knows what to roll back to
        remove_all_snapshots
        restore_snapshot "$suffix"
        dotnet ef migrations remove \
            --project "$PROJECT" \
            --startup-project "$STARTUP_PROJECT" \
            -- "$p"
        save_snapshot "$suffix"
    done

# ── UPDATE ───────────────────────────────────────────────────────────────────
elif [ "$1" == "update" ]; then
    echo "=== Updating database (using default config) ==="
    dotnet ef database update --project "$PROJECT" --startup-project "$STARTUP_PROJECT"

# ── LIST ─────────────────────────────────────────────────────────────────────
elif [ "$1" == "list" ]; then
    for p in "${PROVIDER_KEYS[@]}"; do
        echo "=== Migrations for $p ==="
        dotnet ef migrations list \
            --project "$PROJECT" \
            --startup-project "$STARTUP_PROJECT" \
            -- "$p"
    done

else
    usage
fi
