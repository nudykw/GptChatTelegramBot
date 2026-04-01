using DataBaseLayer;
using DataBaseLayer.Contexts;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceLayer.Services;
using ServiceLayer.Services.AudioTranscriptor;
using ServiceLayer.Services.OpenAI;
using ServiceLayer.Services.MessageProcessor;
using ServiceLayer.Services.Telegram;
using ServiceLayer.Services.Localization;
using ServiceLayer.Utils;
using Telegram.Bot;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        // Load shared base config (AI keys, BotToken, SQLite default):
        //   Dev:  Configs/ is at the repo root, 4 levels up from bin/Debug/net*/
        //   Prod: Configs/ is copied next to the binary
        var devBase = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Configs"));
        var pubBase = Path.Combine(AppContext.BaseDirectory, "Configs");
        var configsDir = Directory.Exists(devBase) && File.Exists(Path.Combine(devBase, "appsettings.json"))
            ? devBase : pubBase;

        cfg.AddJsonFile(Path.Combine(configsDir, "appsettings.json"), optional: true, reloadOnChange: true);
        // appsettings.json in the project dir overrides (debug logging, local DB path)
        // already added by CreateDefaultBuilder — no need to add again
    })
    .ConfigureServices((context, services) =>
    {
        ConfigureServices(context, services);
        InitDb(context, services);
    })
    .Build();

using (var scope = host.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    MigrationConfigurator.ApplyMigrations(serviceProvider);
}

await host.RunAsync();

static void InitDb(HostBuilderContext context, IServiceCollection services)
{
    var appSettings = context.Configuration.GetSection(AppSettings.Configuration).Get<AppSettings>() 
        ?? throw new Exception($"Failed to bind configuration section '{AppSettings.Configuration}' to {nameof(AppSettings)}.");
    
    services.AddDbContext<StoreContext>(options => 
        MigrationConfigurator.Configure(options, appSettings.Database.Provider, appSettings.Database.ConnectionString));

    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
}

static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
{
    InitConfigs(context, services);

    // Register named HttpClient to benefits from IHttpClientFactory
    // and consume it with ITelegramBotClient typed client.
    // More read:
    //  https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0#typed-clients
    //  https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
    services.AddHttpClient();
    services.AddHttpClient("telegram_bot_client")
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                AppSettings appConfig = sp.GetConfiguration<AppSettings>();
                TelegramBotClientOptions options = new(appConfig.TelegramBotConfiguration.BotToken);
                return new TelegramBotClient(options, httpClient);
            });

    services.AddScoped<UpdateHandler>();
    services.AddScoped<ReceiverService>();
    services.AddHostedService<PollingService>();

    services.AddScoped<IChatServiceFactory, ChatServiceFactory>();
    services.AddScoped<IChatService, ResilientChatService>();

    services.AddScoped<MessageProcessor>();
    services.AddScoped<AudioTranscriptorService>();

    services.AddLocalization();
    services.AddScoped<IUserContext, UserContext>();
    services.AddScoped<IDynamicLocalizer, DynamicLocalizer>();
}
static void InitConfigs(HostBuilderContext context, IServiceCollection services)
{
    // Register Bot configuration
    var appSection = context.Configuration.GetSection(AppSettings.Configuration);
    if (!appSection.Exists())
    {
        throw new Exception($"Configuration section '{AppSettings.Configuration}' is missing from the setup. Please ensure appsettings.json is available in the run directory and contains this section.");
    }
    
    services.Configure<AppSettings>(appSection);
    var appSettings = appSection.Get<AppSettings>() 
        ?? throw new Exception($"Failed to bind configuration section '{AppSettings.Configuration}' to {nameof(AppSettings)}. Check for type mismatches.");
    
    services.AddSingleton(appSettings);
}