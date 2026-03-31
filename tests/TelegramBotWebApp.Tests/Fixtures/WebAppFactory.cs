using DataBaseLayer.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DataBaseLayer.Contexts;
using ServiceLayer.Services.Telegram.Configuretions;

namespace TelegramBotWebApp.Tests.Fixtures;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> that spins up the full
/// <see cref="TelegramBotWebApp"/> pipeline in-process with:
/// <list type="bullet">
///   <item>In-memory SQLite database (no external dependencies)</item>
///   <item>Telegram bot client replaced by a no-op (no real API calls)</item>
///   <item>Configurable <see cref="TelegramBotConfiguration.BaseApiUrl"/> for
///         Polling / Webhook mode switching in tests</item>
/// </list>
/// </summary>
public class WebAppFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// When set, the app starts in Webhook mode.
    /// When null or empty, the app starts in Polling mode.
    /// </summary>
    public string? BaseApiUrl { get; init; }

    /// <summary>Bot token used in config (does not need to be real).</summary>
    public string BotToken { get; init; } = "1234567890:AABBCCDDEEFFaabbccddeeff-TestToken00";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Override config entirely with in-memory values
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:TelegramBotConfiguration:BotToken"]   = BotToken,
                ["AppSettings:TelegramBotConfiguration:OwnerId"]    = "0",
                ["AppSettings:TelegramBotConfiguration:BaseApiUrl"] = BaseApiUrl ?? "",
                ["AppSettings:Database:Provider"]                   = "Sqlite",
                ["AppSettings:Database:ConnectionString"]           = "Data Source=:memory:",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the real StoreContext registration added by AddBotServices()
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<StoreContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            // Register an in-memory EF Core context — no Docker / file system needed
            services.AddDbContext<StoreContext>(options =>
                options.UseSqlite("Data Source=:memory:"));

            // Remove real IHostedService registrations so the bot doesn't try
            // to connect to Telegram during tests
            var hostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var d in hostedServices)
                services.Remove(d);
        });

        // Use test environment so the app doesn't require production secrets
        builder.UseEnvironment("Testing");
    }
}
