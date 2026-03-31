using DataBaseLayer;
using Prometheus;
using Scalar.AspNetCore;
using ServiceLayer.Services.Telegram;
using TelegramBotWebApp.Endpoints;
using TelegramBotWebApp.Extensions;
using TelegramBotWebApp.Services;

// ── Builder ──────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// Load configuration: shared web appsettings → environment variables.
// In development: Configs/ is one level above the project directory.
// In Docker: Configs/ is copied next to the published binary.
var configsFromSource = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Configs", "appsettings.web.json"));
var configsFromPublish = Path.Combine(AppContext.BaseDirectory, "Configs", "appsettings.web.json");

var webConfigPath = File.Exists(configsFromSource) ? configsFromSource : configsFromPublish;

builder.Configuration
    .AddJsonFile(webConfigPath, optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(); // env vars always override the file (used in Docker)

// ── Bot Services ─────────────────────────────────────────────────────────────
// Resolve TelegramBotConfiguration early so we can decide which mode to register
builder.AddBotServices();

// Read the config to decide the bot mode before Build()
var tempConfig = builder.Configuration
    .GetSection(ServiceLayer.Services.AppSettings.Configuration)
    .Get<ServiceLayer.Services.AppSettings>();

var isWebhookMode = !string.IsNullOrWhiteSpace(
    tempConfig?.TelegramBotConfiguration?.BaseApiUrl);

if (isWebhookMode)
{
    // Webhook: register the setup service that calls SetWebhook on startup
    builder.Services.AddHostedService<WebhookSetupService>();
}
else
{
    // Polling: register PollingService as a background IHostedService
    builder.Services.AddHostedService<PollingService>();
}

// ── ASP.NET Core services ────────────────────────────────────────────────────

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()));

// ── App ──────────────────────────────────────────────────────────────────────

var app = builder.Build();

// Apply DB migrations on startup
using (var scope = app.Services.CreateScope())
{
    MigrationConfigurator.ApplyMigrations(scope.ServiceProvider);
}

var telegramConfig = app.Services
    .GetRequiredService<ServiceLayer.Services.Telegram.Configuretions.TelegramBotConfiguration>();

app.Logger.LogInformation(
    "Bot mode: {Mode}",
    telegramConfig.IsWebhookMode()
        ? $"WEBHOOK → {telegramConfig.GetWebhookUrl()}"
        : "POLLING");

// ── Middleware ────────────────────────────────────────────────────────────────

app.UseCors();

// Prometheus: track HTTP request metrics automatically
app.UseHttpMetrics();

// ── Endpoints ────────────────────────────────────────────────────────────────

app.MapHealthEndpoints();   // GET /health
app.MapInfoEndpoints();     // GET /api/info

if (telegramConfig.IsWebhookMode())
{
    app.MapWebhookEndpoints(); // POST /aibot
}

// OpenAPI JSON: GET /openapi/v1.json
app.MapOpenApi();

// Scalar Swagger UI: GET /scalar/v1
app.MapScalarApiReference(options =>
{
    options.Title = "GPTChatBot API";
    options.Theme = ScalarTheme.DeepSpace;
});

// Prometheus metrics: GET /metrics
app.MapMetrics();

// ── Run ───────────────────────────────────────────────────────────────────────

await app.RunAsync();
