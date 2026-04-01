using Microsoft.AspNetCore.Http.Extensions;
using ServiceLayer.Services.Telegram.Configuretions;
using TelegramBotWebApp.Extensions;

namespace TelegramBotWebApp.Endpoints;

public static class DashboardEndpoints
{
    /// <summary>Maps GET / — returns an HTML dashboard with links to all active services.</summary>
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/", (HttpContext ctx, IConfiguration cfg, TelegramBotConfiguration telegramCfg) =>
        {
            var request        = ctx.Request;
            var scheme         = request.Scheme;
            var host           = request.Host.Host;        // just hostname, no port
            var fullHost       = request.Host.ToUriComponent(); // host:port or just host

            var baseUrl        = $"{scheme}://{fullHost}"; // same origin — always auto

            // Profiles & feature flags
            var profiles       = cfg["COMPOSE_PROFILES"] ?? "";
            var swaggerRaw     = cfg["SWAGGER_ENABLED"];
            var swaggerEnabled = string.IsNullOrWhiteSpace(swaggerRaw)
                ? app.Environment.IsDevelopment()
                : swaggerRaw.Equals("true", StringComparison.OrdinalIgnoreCase);

            var hasCloudBeaver = profiles.Contains("cloudbeaver", StringComparison.OrdinalIgnoreCase);
            var hasAspire      = profiles.Contains("aspire",       StringComparison.OrdinalIgnoreCase);
            var hasNginx       = profiles.Contains("nginx",        StringComparison.OrdinalIgnoreCase);
            var isWebhook      = telegramCfg.IsWebhookMode();

            // External service URLs
            // CloudBeaver:
            //   1. CLOUDBEAVER_PUBLIC_URL set → custom subdomain (Cloudflare etc.)
            //   2. nginx profile active       → same origin /cloudbeaver/ path
            //   3. otherwise                  → direct host:CLOUDBEAVER_PORT (LAN)
            string? cloudBeaverUrl = null;
            if (hasCloudBeaver)
            {
                var cbPublic = cfg["CLOUDBEAVER_PUBLIC_URL"];
                if (!string.IsNullOrWhiteSpace(cbPublic))
                {
                    cloudBeaverUrl = cbPublic.TrimEnd('/');
                }
                else if (hasNginx)
                {
                    cloudBeaverUrl = $"{baseUrl}/cloudbeaver/";
                }
                else
                {
                    var cbPort = cfg["CLOUDBEAVER_PORT"] ?? "8978";
                    cloudBeaverUrl = $"{scheme}://{host}:{cbPort}/";
                }
            }

            // Aspire: use explicit public URL if set, otherwise host:port (LAN only)
            string? aspireUrl = null;
            if (hasAspire)
            {
                var aspirePublic = cfg["ASPIRE_PUBLIC_URL"];
                if (!string.IsNullOrWhiteSpace(aspirePublic))
                {
                    aspireUrl = aspirePublic.TrimEnd('/');
                }
                else
                {
                    var aspirePort = cfg["ASPIRE_PORT"] ?? "18888";
                    aspireUrl = $"{scheme}://{host}:{aspirePort}";
                }
            }

            // Build service cards
            var cards = new List<ServiceCard>
            {
                new("❤️", "Health Check",     $"{baseUrl}/health",   "System",         "Always available — returns 200 OK when the bot is running.",            "#22c55e"),
                new("ℹ️", "API Info",         $"{baseUrl}/api/info", "System",         "Bot mode, version, masked token, and current timestamp.",               "#3b82f6"),
                new("📊", "Metrics",          $"{baseUrl}/metrics",  "Observability",  "Prometheus-compatible metrics endpoint for scraping.",                   "#a855f7"),
            };

            if (swaggerEnabled)
                cards.Add(new("📖", "Swagger UI",   $"{baseUrl}/scalar/v1", "Developer", "Interactive OpenAPI documentation (Scalar theme).",                  "#f59e0b"));

            if (isWebhook)
                cards.Add(new("🔗", "Webhook",      telegramCfg.GetWebhookUrl()!, "Telegram", "Active Telegram webhook endpoint (POST).",                      "#06b6d4"));

            if (cloudBeaverUrl is not null)
                cards.Add(new("🗄️", "CloudBeaver",  cloudBeaverUrl, "Database",       "Web-based database manager — browse tables, run queries.",              "#f97316"));

            if (aspireUrl is not null)
                cards.Add(new("🔭", "Aspire Dashboard", aspireUrl,  "Observability",  "Metrics, distributed traces and structured logs from all services.",     "#8b5cf6"));

            var dbProvider = cfg["AppSettings:Database:Provider"] ?? cfg["DB_PROVIDER"] ?? "SQLite";
            var botMode    = isWebhook ? "Webhook" : "Polling";

            var html = BuildHtml(cards, dbProvider, botMode, profiles);

            return Results.Content(html, "text/html; charset=utf-8");
        })
        .WithName("Dashboard")
        .WithSummary("Service dashboard")
        .WithDescription("HTML page listing all active service endpoints.")
        .WithTags("System")
        .ExcludeFromDescription() // exclude from OpenAPI so it doesn't clutter the schema
        .AllowAnonymous();
    }

    // ── HTML builder ──────────────────────────────────────────────────────────

    private static string BuildHtml(List<ServiceCard> cards, string dbProvider, string botMode, string profiles)
    {
        var cardHtml = string.Join("\n", cards.Select(c => $"""
            <a href="{c.Url}" target="_blank" class="card" style="--accent:{c.Color}">
              <div class="card-icon">{c.Icon}</div>
              <div class="card-body">
                <div class="card-tag">{c.Tag}</div>
                <div class="card-title">{c.Title}</div>
                <div class="card-desc">{c.Description}</div>
                <div class="card-url">{c.Url}</div>
              </div>
              <div class="card-arrow">→</div>
            </a>
        """));

        var profileBadges = string.IsNullOrWhiteSpace(profiles)
            ? "<span class='badge'>SQLite</span>"
            : string.Join("", profiles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => $"<span class='badge'>{p.Trim()}</span>"));

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
  <title>GptChatBot — Dashboard</title>
  <meta name="description" content="Service dashboard for GptChatTelegramBot — links to all active services."/>
  <!-- Favicon -->
  <link rel="icon" type="image/x-icon" href="/favicon.ico"/>
  <link rel="apple-touch-icon" href="/logo.png"/>
  <!-- Open Graph / social preview -->
  <meta property="og:type"        content="website"/>
  <meta property="og:title"       content="GptChatBot — Dashboard"/>
  <meta property="og:description" content="All active services at a glance."/>
  <meta property="og:image"       content="/logo.png"/>
  <meta name="twitter:card"       content="summary_large_image"/>
  <meta name="twitter:image"      content="/logo.png"/>
  <link rel="preconnect" href="https://fonts.googleapis.com"/>
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin/>
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet"/>
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

    :root {
      --bg:        #0a0a0f;
      --surface:   #13131a;
      --border:    rgba(255,255,255,.07);
      --text:      #e2e2f0;
      --muted:     #6b6b85;
      --radius:    16px;
    }

    body {
      font-family: 'Inter', system-ui, sans-serif;
      background: var(--bg);
      color: var(--text);
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 2.5rem 1.25rem 4rem;
    }

    /* ── Ambient glow ── */
    body::before {
      content: '';
      position: fixed;
      top: -20%;
      left: 50%;
      transform: translateX(-50%);
      width: 700px;
      height: 400px;
      background: radial-gradient(ellipse, rgba(99,102,241,.18) 0%, transparent 70%);
      pointer-events: none;
      z-index: 0;
    }

    .page { position: relative; z-index: 1; width: 100%; max-width: 860px; }

    /* ── Header ── */
    header {
      text-align: center;
      margin-bottom: 2.5rem;
    }

    .logo {
      display: block;
      width: 50%;
      height: auto;
      border-radius: 18px;
      margin: 0 auto 1.75rem;
      object-fit: cover;
      box-shadow: 0 12px 48px rgba(99,102,241,.35);
      border: 1px solid rgba(255,255,255,.08);
    }

    @keyframes float {
      0%, 100% { transform: translateY(0); }
      50%       { transform: translateY(-6px); }
    }

    h1 {
      font-size: 1.75rem;
      font-weight: 700;
      letter-spacing: -.02em;
      background: linear-gradient(135deg, #fff 40%, #a5b4fc);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }

    .subtitle {
      color: var(--muted);
      font-size: .9rem;
      margin-top: .4rem;
    }

    /* ── Meta strip ── */
    .meta {
      display: flex;
      flex-wrap: wrap;
      gap: .5rem;
      justify-content: center;
      margin-bottom: 2rem;
    }

    .meta-item {
      display: flex;
      align-items: center;
      gap: .4rem;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 999px;
      padding: .3rem .85rem;
      font-size: .8rem;
      color: var(--muted);
    }

    .meta-item strong { color: var(--text); }

    .badge {
      display: inline-block;
      background: rgba(99,102,241,.15);
      border: 1px solid rgba(99,102,241,.3);
      color: #a5b4fc;
      border-radius: 999px;
      padding: .15rem .65rem;
      font-size: .75rem;
      font-weight: 500;
      text-transform: capitalize;
    }

    /* ── Grid ── */
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
      gap: 1rem;
    }

    /* ── Card ── */
    .card {
      display: flex;
      align-items: flex-start;
      gap: 1rem;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      padding: 1.25rem 1.4rem;
      text-decoration: none;
      color: var(--text);
      position: relative;
      overflow: hidden;
      transition: transform .2s ease, border-color .2s ease, box-shadow .2s ease;
    }

    .card::before {
      content: '';
      position: absolute;
      inset: 0;
      background: linear-gradient(135deg, var(--accent, #6366f1) 0%, transparent 60%);
      opacity: 0;
      transition: opacity .25s ease;
    }

    .card:hover {
      transform: translateY(-3px);
      border-color: color-mix(in srgb, var(--accent, #6366f1) 60%, transparent);
      box-shadow: 0 8px 32px color-mix(in srgb, var(--accent, #6366f1) 20%, transparent);
    }

    .card:hover::before { opacity: .06; }

    .card-icon {
      font-size: 1.6rem;
      flex-shrink: 0;
      margin-top: .1rem;
      position: relative;
      z-index: 1;
    }

    .card-body {
      flex: 1;
      min-width: 0;
      position: relative;
      z-index: 1;
    }

    .card-tag {
      font-size: .7rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: .08em;
      color: var(--accent, #6366f1);
      margin-bottom: .25rem;
    }

    .card-title {
      font-size: 1rem;
      font-weight: 600;
      margin-bottom: .25rem;
    }

    .card-desc {
      font-size: .8rem;
      color: var(--muted);
      line-height: 1.5;
      margin-bottom: .5rem;
    }

    .card-url {
      font-family: 'SF Mono', 'Fira Code', ui-monospace, monospace;
      font-size: .72rem;
      color: color-mix(in srgb, var(--accent, #6366f1) 80%, #fff);
      word-break: break-all;
      opacity: .8;
    }

    .card-arrow {
      position: relative;
      z-index: 1;
      font-size: 1.1rem;
      color: var(--muted);
      flex-shrink: 0;
      margin-top: .15rem;
      transition: transform .2s ease, color .2s ease;
    }

    .card:hover .card-arrow {
      transform: translateX(4px);
      color: var(--accent, #6366f1);
    }

    /* ── Footer ── */
    footer {
      margin-top: 3rem;
      text-align: center;
      font-size: .78rem;
      color: var(--muted);
    }

    footer a { color: inherit; text-decoration: underline; opacity: .7; }

    @media (max-width: 480px) {
      .grid { grid-template-columns: 1fr; }
      h1 { font-size: 1.4rem; }
    }
  </style>
</head>
<body>
  <div class="page">
    <header>
      <img src="/logo.png" alt="GptChatBot logo" class="logo"/>
      <h1>GptChatBot — Dashboard</h1>
      <p class="subtitle">All active services at a glance</p>
    </header>

    <div class="meta">
      <div class="meta-item">🗄️ DB &nbsp;<strong>{{dbProvider}}</strong></div>
      <div class="meta-item">⚙️ Mode &nbsp;<strong>{{botMode}}</strong></div>
      <div class="meta-item">🧩 Profiles &nbsp;{{profileBadges}}</div>
    </div>

    <div class="grid">
      {{cardHtml}}
    </div>

    <footer>
      <p>Generated at {{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}} UTC &nbsp;·&nbsp;
         <a href="https://github.com/nudykw/GptChatTelegramBot" target="_blank">GitHub</a></p>
    </footer>
  </div>
</body>
</html>
""";
    }

    // ── Model ─────────────────────────────────────────────────────────────────

    private sealed record ServiceCard(
        string Icon,
        string Title,
        string Url,
        string Tag,
        string Description,
        string Color);
}
