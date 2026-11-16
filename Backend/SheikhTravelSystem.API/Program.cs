using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using SheikhTravelSystem.API.Middleware;
using SheikhTravelSystem.Application;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Configuration;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Infrastructure;
using SheikhTravelSystem.Infrastructure.Authentication;
using SheikhTravelSystem.Infrastructure.Health;
using SheikhTravelSystem.Infrastructure.Persistence.Migrations;
using SheikhTravelSystem.Infrastructure.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Nexus commercial FX (USD → PKR) for Pakistan market display / invoices.
var usdToPkr = builder.Configuration.GetValue("SubscriptionBilling:UsdToPkr", SubscriptionPlanPricing.DefaultUsdToPkr);
if (usdToPkr > 0)
    SubscriptionPlanPricing.UsdToPkrRate = usdToPkr;

// Fail closed in non-Development when required secrets are missing/placeholders.
ProductionSecretsValidator.EnsureValidOrThrow(builder.Configuration, builder.Environment);

if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<HostOptions>(o =>
        o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
}

// Serilog
builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration));

// Application & Infrastructure DI
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Persist Data Protection keys so TrackingTokenProtector (and cookies) survive API restarts.
// Without this, Unprotect fails after recycle → admin cannot reveal /t/{token} for existing links.
var dpKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dpKeysPath))
    dpKeysPath = Path.Combine(builder.Environment.ContentRootPath, "dp-keys");
Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("SheikhGo")
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

builder.Services.AddHealthChecks()
    .AddCheck<SqlHealthCheck>("sql")
    .AddCheck<RedisHealthCheck>("redis")
    .AddCheck<TraccarHealthCheck>("traccar")
    .AddCheck<SignalRHealthCheck>("signalr");

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!)),
        // Small skew avoids noisy 401s when the access token just crossed expiry and the client is refreshing.
        ClockSkew = TimeSpan.FromMinutes(2)
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var sid = context.Principal?.FindFirst(AuthSessionLimits.ClaimSessionId)?.Value;
            if (!Guid.TryParse(sid, out var sessionId))
                return; // Legacy tokens without sid remain valid until expiry.

            var sessions = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionService>();
            var status = await sessions.GetStatusAsync(sessionId, context.HttpContext.RequestAborted);
            if (status is AuthSessionStatus.Active)
            {
                await sessions.TouchAsync(sessionId, context.HttpContext.RequestAborted);
                return;
            }

            if (status is AuthSessionStatus.Superseded)
            {
                context.HttpContext.Items[AuthSessionLimits.SessionEndedHeader] =
                    AuthSessionLimits.SessionEndedSuperseded;
                context.Fail(AuthSessionLimits.SessionSupersededMessage);
                return;
            }

            context.Fail("Session is no longer valid.");
        },
        OnChallenge = context =>
        {
            if (context.HttpContext.Items.TryGetValue(AuthSessionLimits.SessionEndedHeader, out var ended)
                && ended is string reason)
            {
                context.Response.Headers[AuthSessionLimits.SessionEndedHeader] = reason;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    SheikhTravelSystem.Infrastructure.Authentication.PermissionPolicyRegistration.AddPermissionPolicies(options);
});

// Response compression — reduces outbound bandwidth from API
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/json", "application/problem+json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// Trust X-Forwarded-For / Proto only from local reverse proxies (Caddy / cloudflared).
// Required so auth rate limits partition by real client IP instead of 127.0.0.1.
// Direct public :5082 callers keep their connection IP (spoofed XFF ignored).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// Rate Limiting (built-in). Development uses NoLimiter so local login/retry
// loops are not blocked. Production partitions by client IP — AddFixedWindowLimiter
// alone uses a single global bucket (policy name), so one tester's probes 429 everyone.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
        }

        await ValueTask.CompletedTask;
    };

    static string ClientIpKey(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("general", _ => RateLimitPartition.GetNoLimiter("dev-general"));
        options.AddPolicy("auth", _ => RateLimitPartition.GetNoLimiter("dev-auth"));
        options.AddPolicy("portal", _ => RateLimitPartition.GetNoLimiter("dev-portal"));
        options.AddPolicy("public", _ => RateLimitPartition.GetNoLimiter("dev-public"));
    }
    else
    {
        options.AddPolicy("general", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(ClientIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
        // Auth: per-IP, slightly raised for legitimate retries / password-reset flows
        // without removing protection against credential stuffing.
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(ClientIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
        options.AddPolicy("portal", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(ClientIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 40,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
        options.AddPolicy("public", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(ClientIpKey(httpContext), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    }
});

// CORS — production frontends at https://sheikhgo.com (+ subdomains); Vercel previews + local dev below.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendClients", policy =>
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin))
                {
                    return false;
                }

                if (origin is "https://sheikhgo.com"
                    or "https://www.sheikhgo.com"
                    or "https://sheikh-customer-portal.vercel.app"
                    or "https://sheikh-travel-control-center.vercel.app"
                    or "https://sheikhgo-erp.vercel.app"
                    or "https://sheikh-travel-customer-hub.vercel.app"
                    or "http://localhost:4200"
                    or "http://127.0.0.1:4200"
                    or "http://localhost:4300"
                    or "http://127.0.0.1:4300")
                {
                    return true;
                }

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                // Production: apex + any HTTPS subdomain of sheikhgo.com (e.g. portal.sheikhgo.com).
                if (uri.Scheme == Uri.UriSchemeHttps
                    && (uri.Host.Equals("sheikhgo.com", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.EndsWith(".sheikhgo.com", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                // Flutter web / local dev (driver app, etc.)
                if (uri.Scheme is "http" or "https"
                    && uri.Host is "localhost" or "127.0.0.1")
                {
                    return true;
                }

                // Vercel preview deployments for SheikhGo frontends.
                return uri.Scheme == Uri.UriSchemeHttps
                    && uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
                    && (uri.Host.Contains("sheikhgo", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Contains("sheikh-travel-system", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Contains("sheikh-customer-portal", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Contains("sheikh-travel-control-center", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Contains("sheikhgo-erp", StringComparison.OrdinalIgnoreCase)
                        || uri.Host.Contains("sheikh-travel-customer-hub", StringComparison.OrdinalIgnoreCase));
            })
            .AllowAnyMethod()
            // Explicit headers: browsers reject Access-Control-Allow-Headers: * for
            // credentialed requests when Authorization / SignalR negotiate headers are sent.
            .WithHeaders(
                "Authorization",
                "Content-Type",
                "Accept",
                "Accept-Language",
                "X-Requested-With",
                "X-SignalR-User-Agent",
                "x-signalr-user-agent",
                "X-Tenant-Slug",
                "X-Tenant-Id",
                "X-Correlation-Id",
                "Idempotency-Key",
                "X-Client-Op-Id")
            .WithExposedHeaders(AuthSessionLimits.SessionEndedHeader)
            .AllowCredentials());
});

// Controllers & Swagger
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(null, allowIntegerValues: true)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Sheikh Travel System API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Run pending database migrations before seeding (gated by config).
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var runMigrationsOnStartup = app.Configuration.GetValue("Database:RunMigrationsOnStartup", true);

    if (runMigrationsOnStartup)
    {
        try
        {
            var runner = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationRunner>();
            var result = await runner.ApplyPendingAsync(appliedBy: "Startup");
            if (!string.IsNullOrEmpty(result.FailedMigration))
            {
                logger.LogError(
                    "Database migration failed at startup on {Migration}: {Error}",
                    result.FailedMigration,
                    result.ErrorMessage);
            }
            else if (result.AppliedCount > 0)
            {
                logger.LogInformation(
                    "Startup migrations complete: applied {AppliedCount}, already applied {SkippedCount}.",
                    result.AppliedCount,
                    result.SkippedCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed at startup.");
        }

        // Development: surface missing Website CMS tables early (admin Features/Pages 500s).
        if (app.Environment.IsDevelopment())
        {
            try
            {
                var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
                using var connection = dbFactory.CreateConnection();
                var featuresTable = await Dapper.SqlMapper.ExecuteScalarAsync<int>(
                    connection,
                    "SELECT CASE WHEN OBJECT_ID(N'dbo.WebsiteFeatures', N'U') IS NULL THEN 0 ELSE 1 END");
                if (featuresTable == 0)
                {
                    logger.LogError(
                        "Website CMS tables are missing (WebsiteFeatures). Apply WebsiteCmsMigration / WebsiteCmsEnsureMigration.");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not verify Website CMS tables at startup.");
            }
        }

        // GPS position retention is data maintenance, not schema — run with startup migrations only.
        try
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            var gpsSettings = scope.ServiceProvider.GetRequiredService<IOptions<GpsSettings>>().Value;
            await GpsSchemaMigration.ApplyRetentionAsync(dbFactory, gpsSettings.PositionRetentionDays, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GPS position retention failed at startup.");
        }
    }
    else
    {
        logger.LogInformation("Startup DB migrations disabled (Database:RunMigrationsOnStartup=false).");
    }
}

// Seed baseline data on startup (idempotent — only fills empty tables).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync();

        var portalRepository = scope.ServiceProvider.GetRequiredService<ICustomerPortalRepository>();
        await PortalCustomerWriter.NormalizeCustomerPhonesAsync(portalRepository);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database seeding failed at startup.");
    }
}

// Middleware pipeline
app.UseForwardedHeaders();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// TLS is terminated at Caddy/Cloudflare in production; local dev uses HTTP on :5082 via the Angular proxy.

app.UseCors("AllowFrontendClients");
app.UseResponseCompression();
app.UseRateLimiter();

var fileStorageRoot = Path.Combine(app.Environment.ContentRootPath,
    app.Configuration.GetValue<string>("FileStorage:RootPath") ?? "uploads");
Directory.CreateDirectory(fileStorageRoot);
var publicUploadPath = app.Configuration.GetValue<string>("FileStorage:PublicBasePath") ?? "/uploads";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(fileStorageRoot),
    RequestPath = publicUploadPath
});

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<TenantSubscriptionAccessMiddleware>();
app.UseAuthorization();
app.UseMiddleware<DriverMobileIdempotencyMiddleware>();
app.MapControllers();
app.MapHub<TrackingHub>("/hubs/tracking");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<WhatsAppHub>("/hubs/whatsapp");
// /health/live — process up only; plain text for probes (body: Healthy).
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckPlainTextResponseWriter.WriteAsync
});

// /health — dependency checks; plain text "Healthy" for ADO/Caddy/curl probes
// (HTTP 200 even when Degraded so simple up checks stay green).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckPlainTextResponseWriter.WriteAsync
});

// /health/detail — same checks with JSON entries (ops / dashboards).
app.MapHealthChecks("/health/detail", new HealthCheckOptions
{
    ResponseWriter = HealthCheckJsonResponseWriter.WriteAsync
});

// /health/ready — JSON; Degraded (e.g. Traccar unreachable) → 503.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = HealthCheckJsonResponseWriter.WriteAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

{
    var fileStorage = app.Configuration.GetSection("FileStorage");
    var provider = fileStorage.GetValue<string>("Provider") ?? "Azure";
    var azureConnection = fileStorage.GetValue<string>("AzureConnectionString")
        ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("FileStorage__AzureConnectionString");
    var usesAzure = string.Equals(provider, "Azure", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(azureConnection)
        && azureConnection != "__SET_IN_USER_SECRETS_OR_ENV__";
    Log.Information(
        "File storage: {Mode} (container: {Container}; playback: {PlaybackContainer}, sasMinutes: {PlaybackSasMinutes})",
        usesAzure ? "Azure Blob" : "Local disk",
        fileStorage.GetValue<string>("AzureContainerName") ?? "vehicle-files",
        fileStorage.GetValue<string>("PlaybackContainerName") ?? "gps-playbacks",
        fileStorage.GetValue<int?>("PlaybackSasExpiryMinutes") ?? 60);
}

app.Run();
