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

// ── VIDEO ANALYSIS (OpenCV + MediaPipe via ai-service) ───────────────────────
builder.Services.AddSingleton<IVideoStorageService, LocalVideoStorageService>();
builder.Services.AddScoped<IVideoAnalysisService, VideoAnalysisService>();
builder.Services.AddHttpClient<IVideoAnalysisAiClient, VideoAnalysisAiClient>(client =>
{
    var baseUrl = builder.Configuration["AiService:BaseUrl"] ?? "http://localhost:8001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10); // analysis itself runs async server-side; this just needs to accept the 202
});

// ── CONTROLLERS + SWAGGER ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Multipart form default limit (128MB) is below our 200MB video cap — raise it here;
// [RequestSizeLimit] on the upload action handles the Kestrel-side request body limit.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 210L * 1024 * 1024;
});

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

// Serve keyframe thumbnails at /storage/videos/... (matches Video:PublicBaseUrl)
var videoStorageRoot = builder.Configuration["Video:StorageRoot"];
var videoStoragePath = string.IsNullOrWhiteSpace(videoStorageRoot)
    ? Path.Combine(AppContext.BaseDirectory, "storage", "videos")
    : videoStorageRoot;
Directory.CreateDirectory(videoStoragePath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(videoStoragePath),
    RequestPath  = "/storage/videos",
});

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MessageHub>("/hubs/messages");
app.MapHub<VitalsHub>("/hubs/vitals");
app.Run();
