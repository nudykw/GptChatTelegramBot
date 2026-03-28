using DataBaseLayer.Contexts;
using DataBaseLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceLayer.Services;
using ServiceLayer.Services.GptChat;

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
        services.AddDbContext<SqlLiteContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
            
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Add actual service we are testing
        services.AddSingleton<ChatGptService>();

        ServiceProvider = services.BuildServiceProvider();

        // Ensure purely fresh DB scheme created in memory
        var dbContext = ServiceProvider.GetRequiredService<SqlLiteContext>();
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
