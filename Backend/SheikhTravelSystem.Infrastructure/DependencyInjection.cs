using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Infrastructure.Authentication;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Infrastructure.Persistence.Migrations;
using SheikhTravelSystem.Infrastructure.Services;
using SheikhTravelSystem.Infrastructure.Services.Payments;
using SheikhTravelSystem.Infrastructure.Services.Ocr;

namespace SheikhTravelSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.Configure<PortalAuthSettings>(configuration.GetSection(PortalAuthSettings.SectionName));
        services.Configure<PortalPaymentGatewaySettings>(configuration.GetSection(PortalPaymentGatewaySettings.SectionName));
        services.AddSingleton<IPortalOtpService, PortalOtpStore>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IPlatformScope, PlatformScope>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<ITenantModuleService, TenantModuleService>();
        services.AddScoped<ITenantRoleSeedService, TenantRoleSeedService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISmsOtpService, ConsoleSmsOtpService>();
        services.AddScoped<PaymentGatewayPaymentRecorder>();
        services.AddScoped<IPaymentGatewayProvider, StripePaymentGatewayService>();
        services.AddScoped<IPaymentGatewayProvider, JazzCashPaymentGatewayService>();
        services.AddScoped<IPaymentGatewayProvider, EasyPaisaPaymentGatewayService>();
        services.AddScoped<IPaymentGatewayService, ConfiguredPaymentGatewayService>();
        services.AddHostedService<ComplianceReminderHostedService>();
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
