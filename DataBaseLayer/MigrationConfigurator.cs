using DataBaseLayer.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DataBaseLayer;

public static class MigrationConfigurator
{
    public static void Configure(DbContextOptionsBuilder<SqlLiteContext> optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=your-database.db");
    }

    public static void ApplyMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SqlLiteContext>();

        dbContext.Database.Migrate();

        dbContext.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }
}