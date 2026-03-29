using DataBaseLayer;
using DataBaseLayer.Contexts;
using DataBaseLayer.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceLayer.Services;
using ServiceLayer.Services.AudioTranscriptor;
using ServiceLayer.Services.GptChat;
using ServiceLayer.Services.MessageProcessor;
using ServiceLayer.Services.Telegram;
using ServiceLayer.Services.Localization;
using ServiceLayer.Utils;
using Telegram.Bot;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((Action<HostBuilderContext, IServiceCollection>)((context, services) =>
    {
        ConfigureServices(context, services);
        InitDb(context, services);
    }))
    .Build();

await host.RunAsync();

static void InitDb(HostBuilderContext context, IServiceCollection services)
{
    services.AddDbContext<SqlLiteContext>();

    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    var serviceProvider = services.BuildServiceProvider();
    MigrationConfigurator.ApplyMigrations(serviceProvider);
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

    services.AddSingleton<IChatServiceFactory, ChatServiceFactory>();
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