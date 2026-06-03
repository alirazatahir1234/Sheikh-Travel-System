using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Infrastructure.Authentication;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Infrastructure.Persistence.Migrations;
using SheikhTravelSystem.Infrastructure.Services;
using SheikhTravelSystem.Infrastructure.Services.Ocr;

namespace SheikhTravelSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.Configure<GpsSettings>(configuration.GetSection(GpsSettings.SectionName));
        services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));
        services.Configure<TranslatorOptions>(configuration.GetSection(TranslatorOptions.SectionName));
        services.AddHttpClient("PaddleOcr");
        services.AddHttpClient("AzureTranslator");
        services.AddSingleton<IUrduToEnglishTranslator, AzureUrduToEnglishTranslator>();
        services.AddScoped<PaddleOcrProvider>();
        services.AddScoped<AzureDocumentIntelligenceProvider>();
        services.AddScoped<IIdentityOcrService, HybridIdentityOcrService>();
        services.AddScoped<ILocationBroadcastService, LocationBroadcastService>();
        services.AddHttpContextAccessor();
        services.AddSignalR();

        return services;
    }
}
