using System.Text;
using System.Threading.RateLimiting;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using SheikhTravelSystem.API.Middleware;
using SheikhTravelSystem.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Infrastructure;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
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
});

// CORS
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

                if (origin is "https://sheikh-travel-system.vercel.app"
                    or "https://sheikh-customer-portal.vercel.app"
                    or "https://sheikh-travel-control-center.vercel.app"
                    or "https://sheikh-travel-customer-hub.vercel.app"
                    or "http://localhost:4200"
                    or "http://127.0.0.1:4200"
                    or "http://localhost:4300"
                    or "http://127.0.0.1:4300")
                {
                    return true;
                }

                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    // Flutter web / local dev (driver app, etc.)
                    if (uri.Scheme is "http" or "https"
                        && (uri.Host is "localhost" or "127.0.0.1"))
                    {
                        return true;
                    }

                    // Allow Vercel preview deployments for admin and customer hub projects.
                    return uri.Scheme == Uri.UriSchemeHttps
                        && uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
                        && (uri.Host.Contains("sheikh-travel-system", StringComparison.OrdinalIgnoreCase)
                            || uri.Host.Contains("sheikh-customer-portal", StringComparison.OrdinalIgnoreCase)
                            || uri.Host.Contains("sheikh-travel-control-center", StringComparison.OrdinalIgnoreCase)
                            || uri.Host.Contains("sheikh-travel-customer-hub", StringComparison.OrdinalIgnoreCase));
                }

                return false;
            })
            .AllowAnyMethod()
            .AllowAnyHeader());
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

// Run database migrations before seeding
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        using var connection = dbFactory.CreateConnection();
        
        // Add BookingNumber column if missing
        var columnExists = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'BookingNumber'");
        
        if (columnExists == 0)
        {
            logger.LogInformation("Adding BookingNumber column to Bookings table...");
            await connection.ExecuteAsync("ALTER TABLE Bookings ADD BookingNumber NVARCHAR(20) NOT NULL DEFAULT ''");
            
            // Backfill existing rows
            var rows = await connection.QueryAsync<int>("SELECT Id FROM Bookings WHERE BookingNumber = '' OR BookingNumber IS NULL");
            foreach (var id in rows)
            {
                var year = DateTime.UtcNow.Year;
                await connection.ExecuteAsync(
                    "UPDATE Bookings SET BookingNumber = @BN WHERE Id = @Id",
                    new { BN = $"BK-{year}-{id:D4}", Id = id });
            }
            logger.LogInformation("BookingNumber column added and backfilled for {Count} rows.", rows.Count());
        }

        await GpsSchemaMigration.ApplyAsync(dbFactory, logger);

        var gpsSettings = scope.ServiceProvider.GetRequiredService<IOptions<GpsSettings>>().Value;
        await GpsSchemaMigration.ApplyRetentionAsync(dbFactory, gpsSettings.PositionRetentionDays, logger);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database migration failed at startup.");
    }

    try
    {
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        await PortalSchemaMigration.ApplyAsync(dbFactory, logger);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Portal schema migration failed at startup.");
    }

    try
    {
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        await TenantSchemaMigration.ApplyAsync(dbFactory, logger);
        await PlatformSchemaMigration.ApplyAsync(dbFactory, logger);
        await TenantNormalizationMigration.ApplyAsync(dbFactory, logger);
        await PlatformSettingsMigration.ApplyAsync(dbFactory, logger);
        await OrganizationDesignerMigration.ApplyAsync(dbFactory, logger);
        await SubscriptionBillingMigration.ApplyAsync(dbFactory, logger);
        await FleetSchemaMigration.ApplyAsync(dbFactory, logger);
        await FleetComplianceMigration.ApplyAsync(dbFactory, logger);
        await DriverPerformanceMigration.ApplyAsync(dbFactory, logger);
        await DriverVerificationMigration.ApplyAsync(dbFactory, logger);
        await AssignmentSchemaMigration.ApplyAsync(dbFactory, logger);
        await MaintenanceModuleMigration.ApplyAsync(dbFactory, logger);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Tenant schema migration failed at startup.");
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
