using DataBaseLayer.Contexts;
using DataBaseLayer.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataBaseLayer;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StoreContext>
{
    public StoreContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StoreContext>();
        
        // Default to Sqlite if no provider specified in args
        var provider = DatabaseProvider.Sqlite;
        var connectionString = "Data Source=/home/nudyk/Projects/DotNet/GptChatTelegramBot/TelegramBotApp/StoreContext.db";

        if (args.Length > 0 && Enum.TryParse<DatabaseProvider>(args[0], true, out var p))
        {
            provider = p;
        }

        if (args.Length > 1)
        {
            connectionString = args[1];
        }
        else
        {
            // Use dummy connection strings for design time if not provided
            connectionString = provider switch
            {
                DatabaseProvider.Sqlite => "Data Source=/home/nudyk/Projects/DotNet/GptChatTelegramBot/TelegramBotApp/StoreContext.db",
                DatabaseProvider.SqlServer => "Server=(localdb)\\mssqllocaldb;Database=design;Trusted_Connection=True;",
                DatabaseProvider.PostgreSql => "Host=localhost;Database=design;Username=postgres;Password=password",
                DatabaseProvider.MySql => "Server=localhost;Database=design;Uid=root;Pwd=password",
                _ => connectionString
            };
        }

        MigrationConfigurator.Configure(optionsBuilder, provider, connectionString);

        return new StoreContext(optionsBuilder.Options);
    }
}
