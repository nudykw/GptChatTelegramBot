using DataBaseLayer;
using DataBaseLayer.Contexts;
using DataBaseLayer.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ServiceLayer.IntegrationTests;

public class DatabaseSupportTests
{
    [Fact]
    public async Task Sqlite_Migrations_CanBeApplied()
    {
        // Arrange
        var services = new ServiceCollection();
        var connectionString = "Data Source=:memory:";
        
        services.AddDbContext<StoreContext>(options =>
            MigrationConfigurator.Configure(options, DatabaseProvider.Sqlite, connectionString));

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StoreContext>();
        
        // Use a persistent connection for in-memory SQLite migrations to prevent DB deletion between calls
        await context.Database.OpenConnectionAsync();
        await context.Database.MigrateAsync();

        var canConnect = await context.Database.CanConnectAsync();
        Assert.True(canConnect);
    }

    [Fact]
    public async Task Postgres_Migrations_CanBeApplied()
    {
        // Arrange
        var container = new PostgreSqlBuilder().Build();
        await container.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContext<StoreContext>(options =>
            MigrationConfigurator.Configure(options, DatabaseProvider.PostgreSql, container.GetConnectionString()));

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StoreContext>();
            await context.Database.MigrateAsync();
            
            var canConnect = await context.Database.CanConnectAsync();
            Assert.True(canConnect);
        }
        finally
        {
            await container.StopAsync();
        }
    }

    [Fact]
    public async Task SqlServer_Migrations_CanBeApplied()
    {
        // Arrange
        var container = new MsSqlBuilder().Build();
        await container.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContext<StoreContext>(options =>
            MigrationConfigurator.Configure(options, DatabaseProvider.SqlServer, container.GetConnectionString()));

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StoreContext>();
            await context.Database.MigrateAsync();
            
            var canConnect = await context.Database.CanConnectAsync();
            Assert.True(canConnect);
        }
        finally
        {
            await container.StopAsync();
        }
    }

    [Fact]
    public async Task MySql_Migrations_CanBeApplied()
    {
        // Arrange
        var container = new MySqlBuilder().Build();
        await container.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContext<StoreContext>(options =>
            MigrationConfigurator.Configure(options, DatabaseProvider.MySql, container.GetConnectionString()));

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<StoreContext>();
            await context.Database.MigrateAsync();
            
            var canConnect = await context.Database.CanConnectAsync();
            Assert.True(canConnect);
        }
        finally
        {
            await container.StopAsync();
        }
    }
}
