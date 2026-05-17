using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nest;
using System.Text;
using TarteelClone.Api.Hubs;
using TarteelClone.QuranEngine.Data;
using TarteelClone.QuranEngine.Services;
using TarteelClone.UserService.Data;
using TarteelClone.UserService.Services;
using TarteelClone.SearchService.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Databases ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<QuranDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("QuranDb")));

builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("UserDb")));

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        // Allow JWT in query string for WebSocket / SignalR hubs
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Elasticsearch ─────────────────────────────────────────────────────────────
var elasticUri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
var elasticSettings = new ConnectionSettings(new Uri(elasticUri))
    .DefaultIndex("quran_verses");
builder.Services.AddSingleton<IElasticClient>(new ElasticClient(elasticSettings));

// ── HttpClient (for ASR service calls from SignalR hub) ───────────────────────
builder.Services.AddHttpClient();

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IVerseMatchingService, VerseMatchingService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IQuranSearchService, QuranSearchService>();

// ── SignalR (real-time recitation streaming) ──────────────────────────────────
builder.Services.AddSignalR();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<RecitationHub>("/hubs/recitation");

app.Run();
