using System.IO.Compression;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using SheikhTravelSystem.API.Middleware;
using SheikhTravelSystem.Application;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Infrastructure;
using SheikhTravelSystem.Infrastructure.Authentication;
using SheikhTravelSystem.Infrastructure.Health;
using SheikhTravelSystem.Infrastructure.Persistence.Migrations;
using SheikhTravelSystem.Infrastructure.SignalR;

var builder = WebApplication.CreateBuilder(args);

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

// Rate Limiting (built-in)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("general", opt =>
    {
        opt.PermitLimit = 60;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("portal", opt =>
    {
        opt.PermitLimit = 40;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("public", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
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
            .AllowAnyHeader()
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

        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        await PortalCustomerWriter.NormalizeCustomerPhonesAsync(dbFactory);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database seeding failed at startup.");
    }
}

// Middleware pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// TLS is terminated at Railway/Vercel in production; local dev uses HTTP on :5082 via the Angular proxy.

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
app.UseAuthorization();
app.MapControllers();
app.MapHub<TrackingHub>("/hubs/tracking");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

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
        "File storage: {Mode} (container: {Container})",
        usesAzure ? "Azure Blob" : "Local disk",
        fileStorage.GetValue<string>("AzureContainerName") ?? "vehicle-files");
}

app.Run();
