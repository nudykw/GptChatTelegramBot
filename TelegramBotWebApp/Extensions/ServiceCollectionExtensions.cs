using DataBaseLayer;
using DataBaseLayer.Contexts;
using DataBaseLayer.Repositories;
using ServiceLayer.Services;
using ServiceLayer.Services.AudioTranscriptor;
using ServiceLayer.Services.Localization;
using ServiceLayer.Services.MessageProcessor;
using ServiceLayer.Services.OpenAI;
using ServiceLayer.Services.Telegram;
using ServiceLayer.Utils;
using Telegram.Bot;

namespace TelegramBotWebApp.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application services: configuration, Telegram bot client,
    /// database, domain services, and localization.
    /// </summary>
    public static WebApplicationBuilder AddBotServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config  = builder.Configuration;

        // ── App configuration ────────────────────────────────────────────────
        // Register AppSettings as IOptions<T> — the actual values are resolved lazily
        // after the full configuration pipeline (including WebApplicationFactory overrides) runs.
        var appSection = config.GetSection(AppSettings.Configuration);
        services.Configure<AppSettings>(appSection);

        // Provide AppSettings as a directly-resolvable singleton via IOptions
        services.AddSingleton<AppSettings>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppSettings>>().Value);

        // Provide TelegramBotConfiguration as a singleton derived from AppSettings
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<AppSettings>();
            return settings.TelegramBotConfiguration
                ?? new ServiceLayer.Services.Telegram.Configuretions.TelegramBotConfiguration();
        });

        // ── Telegram Bot client (named HttpClient → typed client) ────────────
        services.AddHttpClient();
        services.AddHttpClient("telegram_bot_client")
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                var cfg = sp.GetRequiredService<AppSettings>();
                var token = cfg.TelegramBotConfiguration?.BotToken ?? string.Empty;
                var options = new TelegramBotClientOptions(string.IsNullOrEmpty(token) ? "0:test" : token);
                return new TelegramBotClient(options, httpClient);
            });

        // ── Bot handler services ─────────────────────────────────────────────
        services.AddScoped<UpdateHandler>();
        services.AddScoped<ReceiverService>();

        // ── Chat / AI services ───────────────────────────────────────────────
        services.AddScoped<IChatServiceFactory, ChatServiceFactory>();
        services.AddScoped<IChatService, ResilientChatService>();
        services.AddScoped<MessageProcessor>();
        services.AddScoped<AudioTranscriptorService>();

        // ── Localization ─────────────────────────────────────────────────────
        services.AddLocalization();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<IDynamicLocalizer, DynamicLocalizer>();

        // ── Database ─────────────────────────────────────────────────────────
        // DB config is also resolved lazily so the factory's in-memory DB is used in tests
        services.AddDbContext<StoreContext>((sp, options) =>
        {
            var settings = sp.GetRequiredService<AppSettings>();
            MigrationConfigurator.Configure(options, settings.Database.Provider, settings.Database.ConnectionString);
        });

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return builder;
    }
}
