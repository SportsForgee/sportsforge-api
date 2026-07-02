using System.Text;
using Api.Data;
using Api.Hubs;
using Api.Models;
using Api.Services;
using KhoiIntegration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── CORS ────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod(); // required for SignalR WebSocket handshake
    });
});

// ── DATABASE ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── IDENTITY ─────────────────────────────────────────────────────────────────
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequireDigit           = true;
    options.Password.RequiredLength         = 8;
    options.Password.RequireUppercase       = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail         = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── JWT AUTH ──────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero,
        };

        // SignalR passes the JWT via query string for WebSocket connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path        = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── SIGNALR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── SERVICES ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IClubDataService, InMemoryClubDataService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ICoachDashboardService, CoachDashboardService>();
builder.Services.AddScoped<IDoctorDashboardService, DoctorDashboardService>();

// ── HARDWARE TELEMETRY (Forge Insole + Khoi wearable) ────────────────────────
builder.Services.AddScoped<IHardwareTelemetryService, HardwareTelemetryService>();
builder.Services.AddHostedService<KhoiSyncService>();
builder.Services.AddHttpClient<IKhoiClient, KhoiClient>(client =>
{
    var baseUrl = builder.Configuration["Khoi:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl)) client.BaseAddress = new Uri(baseUrl);

    var apiKey = builder.Configuration["Khoi:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
});

// ── CONTROLLERS + SWAGGER ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── BUILD ─────────────────────────────────────────────────────────────────────
builder.WebHost.UseUrls("http://0.0.0.0:5186");
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MessageHub>("/hubs/messages");
app.MapHub<VitalsHub>("/hubs/vitals");
app.Run();
