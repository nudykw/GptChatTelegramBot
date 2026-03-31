using DataBaseLayer.Contexts;
using DataBaseLayer.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DataBaseLayer;

public static class MigrationConfigurator
{
    public static void Configure(DbContextOptionsBuilder optionsBuilder, DatabaseProvider provider, string connectionString)
    {
        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                optionsBuilder.UseSqlite(connectionString, x => x.MigrationsAssembly("DataBaseLayer"))
                              .ReplaceService<IMigrationsAssembly, DataBaseLayer.Internal.ProviderSpecificMigrationsAssembly>();
                break;
            case DatabaseProvider.SqlServer:
                optionsBuilder.UseSqlServer(connectionString, x => x.MigrationsAssembly("DataBaseLayer"))
                              .ReplaceService<IMigrationsAssembly, DataBaseLayer.Internal.ProviderSpecificMigrationsAssembly>();
                break;
            case DatabaseProvider.PostgreSql:
                optionsBuilder.UseNpgsql(connectionString, x => x.MigrationsAssembly("DataBaseLayer"))
                              .ReplaceService<IMigrationsAssembly, DataBaseLayer.Internal.ProviderSpecificMigrationsAssembly>();
                break;
            case DatabaseProvider.MySql:
                try 
                {
                    optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), x => x.MigrationsAssembly("DataBaseLayer"))
                                  .ReplaceService<IMigrationsAssembly, DataBaseLayer.Internal.ProviderSpecificMigrationsAssembly>();
                }
                catch
                {
                    // Default version for design-time if connection fails
                    optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)), x => x.MigrationsAssembly("DataBaseLayer"))
                                  .ReplaceService<IMigrationsAssembly, DataBaseLayer.Internal.ProviderSpecificMigrationsAssembly>();
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
        }
    }

    public static void ApplyMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StoreContext>();
        dbContext.Database.Migrate();

        if (dbContext.Database.IsSqlite())
        {
            dbContext.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        }
    }
}