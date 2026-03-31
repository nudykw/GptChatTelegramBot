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
        var appSection = config.GetSection(AppSettings.Configuration);
        if (!appSection.Exists())
            throw new InvalidOperationException(
                $"Configuration section '{AppSettings.Configuration}' is missing. " +
                "Ensure appsettings.web.json is present and contains this section.");

        services.Configure<AppSettings>(appSection);
        var appSettings = appSection.Get<AppSettings>()
            ?? throw new InvalidOperationException(
                $"Failed to bind '{AppSettings.Configuration}' to {nameof(AppSettings)}.");

        services.AddSingleton(appSettings);
        services.AddSingleton(appSettings.TelegramBotConfiguration);

        // ── Telegram Bot client (named HttpClient → typed client) ────────────
        services.AddHttpClient();
        services.AddHttpClient("telegram_bot_client")
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                var cfg = sp.GetRequiredService<AppSettings>();
                var options = new TelegramBotClientOptions(cfg.TelegramBotConfiguration.BotToken);
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
        services.AddDbContext<StoreContext>(options =>
            MigrationConfigurator.Configure(
                options,
                appSettings.Database.Provider,
                appSettings.Database.ConnectionString));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        return builder;
    }
}
