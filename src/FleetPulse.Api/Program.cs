using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FleetPulse.Api.Auth;
using FleetPulse.Api.Data;
using FleetPulse.Api.Hubs;
using FleetPulse.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:4200" };

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.Configure<EnrollmentOptions>(builder.Configuration.GetSection(EnrollmentOptions.SectionName));

var demoEnabled = builder.Configuration.GetValue("Demo:Enabled", false);

// --- Database: SQLite by default, SQL Server when configured ---
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=fleetpulse.db";

// --- Services ---
builder.Services.AddDbContext<FleetPulseDbContext>(options =>
{
    switch (databaseProvider.ToLowerInvariant())
    {
        case "sqlite":
            options.UseSqlite(connectionString);
            break;
        case "sqlserver":
            options.UseSqlServer(connectionString);
            break;
        default:
            throw new InvalidOperationException(
                $"Unsupported Database:Provider '{databaseProvider}'. Use 'Sqlite' or 'SqlServer'.");
    }
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IDeviceSessionService, DeviceSessionService>();
builder.Services.AddScoped<IDeviceOfflineSweeper, DeviceOfflineSweeper>();
builder.Services.AddScoped<DeviceNotifier>();
builder.Services.AddScoped<IDeviceNotifier>(sp =>
{
    var inner = sp.GetRequiredService<DeviceNotifier>();

    return demoEnabled
        ? new DemoDeviceNotifier(
            inner,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<DemoDeviceNotifier>>())
        : inner;
});
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddHostedService<DeviceOfflineMonitor>();

if (demoEnabled)
{
    builder.Services.AddHostedService<DemoSimulatorService>();
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.SetIsOriginAllowed(origin => allowedOrigins.Any(allowed => MatchesOrigin(origin, allowed)))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Allow the SignalR hub to authenticate via the access_token query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/devices"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// --- Create and seed the database on first run (works for both providers) ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FleetPulseDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.InitializeAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
}));

app.UseCors("AngularDev");

// Exact origins are compared case-insensitively; an entry may use "*" for a subdomain,
// e.g. "https://*.netlify.app" covers Netlify deploy previews whose hostnames change per build.
static bool MatchesOrigin(string origin, string allowed)
{
    // Browsers send Origin without a trailing slash; tolerate one in configuration.
    origin = origin.TrimEnd('/');
    allowed = allowed.TrimEnd('/');

    if (string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!allowed.Contains('*'))
    {
        return false;
    }

    var pattern = "^" + Regex.Escape(allowed).Replace("\\*", ".*") + "$";
    return Regex.IsMatch(origin, pattern, RegexOptions.IgnoreCase);
}
app.UseAuthentication();
app.UseAuthorization();

// Anonymous liveness probe for hosting platforms (Render, Docker health checks).
app.MapGet("/health", () => Results.Text("ok"));

app.MapControllers();
app.MapHub<DeviceHub>("/hubs/devices");

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;

