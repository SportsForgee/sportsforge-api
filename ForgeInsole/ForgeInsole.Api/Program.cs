using ForgeInsole.Api.Auth;
using ForgeInsole.Api.Devices;
using ForgeInsole.Api.Services;
using ForgeInsole.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ── CORS (browser-based SSE consumers) ───────────────────────────────────────
// CORS origin policy (three-way):
//   1. ForgeInsole:AllowedOrigins set  -> locked to exactly those origins
//   2. empty + Development             -> AllowAnyOrigin (dev convenience; API key is a
//                                         header not a cookie, so no credential-leak risk)
//   3. empty + non-Development         -> deny-by-design (no cross-origin browser access);
//                                         a LogError fires at startup so it's loud, not silent
var allowedOrigins = builder.Configuration.GetSection("ForgeInsole:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Partners", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Dev convenience only — no credentials travel via CORS (the API key is a
            // header, not a cookie), so AllowAnyOrigin carries no credential-leak risk here.
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            // Deliberately empty origin list: outside Development with nothing configured,
            // browser cross-origin access is denied by design. This is not a missing config —
            // it's the secure default. Set ForgeInsole:AllowedOrigins to opt specific origins in.
            policy.WithOrigins(Array.Empty<string>()).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

// ── DATABASE (separate DB, separate migrations history — no shared tables with SportsForgeDb) ──
builder.Services.AddDbContext<ForgeInsoleDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ForgeInsoleDb")));

// ── AUTH (partner API key — independent of SportsForge's JWT scheme) ─────────
builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.SchemeName, null);
builder.Services.AddAuthorization();

// ── TELEMETRY GENERATION ──────────────────────────────────────────────────────
builder.Services.AddSingleton<TelemetryBroadcaster>();
builder.Services.AddHostedService<InsoleTelemetryHostedService>();

// ── REAL HARDWARE INGEST ──────────────────────────────────────────────────────
// Connects out to each configured ESP32's WebSocket server (firmware port 81) and files
// its frames as ordinary TelemetryReadings. Inert unless ForgeInsole:DeviceIngest:Devices
// is populated, so a default checkout still behaves as the pure simulator it was.
builder.Services.Configure<DeviceIngestOptions>(builder.Configuration.GetSection(DeviceIngestOptions.SectionName));
builder.Services.AddSingleton<DeviceRegistry>();
builder.Services.AddSingleton<InsoleScanner>();
builder.Services.AddSingleton<InsoleDeviceManager>();
builder.Services.AddHostedService<DeviceIngestHostedService>();

// ── HEALTH ────────────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── CONTROLLERS + SWAGGER ──────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Forge Insole Telemetry API", Version = "v1" });

    c.AddSecurityDefinition(ApiKeyAuthenticationOptions.HeaderName, new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationOptions.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Partner API key issued to Khoi Tech.",
    });
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference(ApiKeyAuthenticationOptions.HeaderName),
            new List<string>()
        },
    });
});

// No hardcoded UseUrls here on purpose — hosting platforms (Azure App Service, containers,
// etc.) inject their own binding via ASPNETCORE_URLS/PORT, and a hardcoded call here would
// silently override that. Local dev gets its port from Properties/launchSettings.json
// (which sets ASPNETCORE_URLS via its applicationUrl) and/or Kestrel config in
// appsettings.Development.json — see docs/forge-insole/ARCHITECTURE.md §7.
var app = builder.Build();

if (allowedOrigins.Length == 0 && !app.Environment.IsDevelopment())
{
    app.Logger.LogError(
        "ForgeInsole:AllowedOrigins is empty outside Development — CORS will block all " +
        "cross-origin browser requests to this API. Set ForgeInsole:AllowedOrigins to Khoi " +
        "Tech's actual origin(s) if browser-based access (e.g. their web SSE client) is required.");
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ForgeInsoleDbContext>();
    db.Database.Migrate();
}

// Always on, not just in Development — Khoi Tech needs to explore the deployed API itself,
// not a local copy. Swagger only exposes route/schema metadata; calling any real endpoint
// still requires a valid X-Api-Key, so this carries no meaningful additional exposure.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Partners");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
