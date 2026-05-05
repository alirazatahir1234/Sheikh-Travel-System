using System.Text;
using System.Threading.RateLimiting;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using SheikhTravelSystem.API.Middleware;
using SheikhTravelSystem.Application;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure;
using SheikhTravelSystem.Infrastructure.SignalR;

var builder = WebApplication.CreateBuilder(args);

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
});

builder.Services.AddAuthorization();

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
                    or "http://localhost:4200"
                    or "http://127.0.0.1:4200"
                    or "http://localhost:4300"
                    or "http://127.0.0.1:4300")
                {
                    return true;
                }

                if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    // Allow Vercel preview deployments for this frontend project.
                    return uri.Scheme == Uri.UriSchemeHttps
                        && uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
                        && uri.Host.Contains("sheikh-travel-system", StringComparison.OrdinalIgnoreCase);
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
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
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
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database migration failed at startup.");
    }
}

// Seed baseline data on startup (idempotent — only fills empty tables).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync();
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
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontendClients");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TrackingHub>("/hubs/tracking");

app.Run();
