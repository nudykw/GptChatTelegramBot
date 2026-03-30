using DataBaseLayer.Contexts;
using DataBaseLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceLayer.Services;
using ServiceLayer.Services.OpenAI;
using ServiceLayer.Services.GeminiChat.DotNet;
using ServiceLayer.Services.MessageProcessor;
using Telegram.Bot;
using Telegram.Bot.Requests;
using ServiceLayer.Constans;
using Moq;
using ServiceLayer.Services.Localization;
using ServiceLayer.Utils;

namespace ServiceLayer.IntegrationTests.Fixtures;

public class TestAppFixture : IDisposable
{
    public ServiceProvider ServiceProvider { get; private set; }
    
    public TestAppFixture()
    {
        var services = new ServiceCollection();

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Bind AppSettings
        var appSection = configuration.GetSection(AppSettings.Configuration);
        services.Configure<AppSettings>(appSection);
        var appSettings = appSection.Get<AppSettings>();
        if (appSettings == null)
            throw new Exception("Cannot bind AppSettings from linked appsettings.json.");
            
        services.AddSingleton(appSettings);

        // Add InMemory DB instead of actual SQLite
        services.AddDbContext<StoreContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
            
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add HttpClient for pricing service
        services.AddHttpClient();

        // Register default OpenAI provider config for tests
        var openAiConfig = appSettings.ChatProviders.FirstOrDefault(p => p.ProviderType == AiProvider.OpenAI)
            ?? new ChatProviderConfig
            {
                Name = "OpenAI-Default",
                ProviderType = AiProvider.OpenAI,
                ApiKey = "sk-test-key",
                ModelName = AiModel.Gpt4oMini
            };
        services.AddSingleton(openAiConfig);

        services.AddLocalization();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<IDynamicLocalizer, DynamicLocalizer>();

        // Add actual service we are testing
        services.AddSingleton<OpenAIService>();
        services.AddSingleton<ChatGeminiService>();
        services.AddSingleton<IChatServiceFactory, ChatServiceFactory>();

        ServiceProvider = services.BuildServiceProvider();

        // Ensure purely fresh DB scheme created in memory
        var dbContext = ServiceProvider.GetRequiredService<StoreContext>();
        dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        if (ServiceProvider != null)
        {
            ServiceProvider.Dispose();
        }
    }
}
