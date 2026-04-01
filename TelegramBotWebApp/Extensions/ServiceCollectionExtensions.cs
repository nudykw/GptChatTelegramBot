using DataBaseLayer;
using DataBaseLayer.Contexts;
using DataBaseLayer.Repositories;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ServiceLayer.Services;
using ServiceLayer.Services.AudioTranscriptor;
using ServiceLayer.Services.Localization;
using ServiceLayer.Services.MessageProcessor;
using ServiceLayer.Services.OpenAI;
using ServiceLayer.Services.Telegram;
using ServiceLayer.Utils;
using Telegram.Bot;
using Telegram.Bot.Polling;

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
        // IUpdateHandler: used by the webhook endpoint (easily mockable in tests)
        // UpdateHandler:  used by ReceiverService for Polling mode
        services.AddScoped<UpdateHandler>();
        services.AddScoped<IUpdateHandler>(sp => sp.GetRequiredService<UpdateHandler>());
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

        // ── OpenTelemetry → Aspire Dashboard ───────────────────────────────────
        // Reads OTEL_EXPORTER_OTLP_ENDPOINT directly from the process environment
        // (env vars are available from startup, before the full config pipeline runs).
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        var otelBuilder = services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName:        "GptChatTelegramBot",
                serviceVersion:     typeof(ServiceCollectionExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(otlpEndpoint);
                        o.Protocol = OtlpExportProtocol.Grpc;
                    });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    metrics.AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(otlpEndpoint);
                        o.Protocol = OtlpExportProtocol.Grpc;
                    });
            });

        // Forward structured logs to Aspire Dashboard
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                logging.AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OtlpExportProtocol.Grpc;
                });
            });
        }

        return builder;
    }
}
