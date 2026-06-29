using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Infrastructure.Authentication;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Infrastructure.Persistence.Migrations;
using SheikhTravelSystem.Infrastructure.Services;
using SheikhTravelSystem.Infrastructure.Services.Payments;
using SheikhTravelSystem.Infrastructure.Services.Ocr;
using SheikhTravelSystem.Infrastructure.Traccar;
using Azure.Storage.Blobs;
using SheikhTravelSystem.Infrastructure.Services.Storage;

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
        services.AddHostedService<MaintenanceAlertHostedService>();
        services.Configure<GpsSettings>(configuration.GetSection(GpsSettings.SectionName));
        services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<TranslatorOptions>(configuration.GetSection(TranslatorOptions.SectionName));
        services.AddHttpClient("PaddleOcr");
        services.AddHttpClient("AzureTranslator");
        services.AddSingleton<IUrduToEnglishTranslator, AzureUrduToEnglishTranslator>();
        services.AddScoped<PaddleOcrProvider>();
        services.AddScoped<AzureDocumentIntelligenceProvider>();
        services.AddScoped<IIdentityOcrService, HybridIdentityOcrService>();
        RegisterFileStorage(services, configuration);
        services.AddScoped<ILocationBroadcastService, LocationBroadcastService>();
        services.AddHttpContextAccessor();
        services.AddSignalR();

        // Traccar GPS integration
        services.Configure<TraccarOptions>(configuration.GetSection(TraccarOptions.SectionName));
        services.AddHttpClient<ITraccarClient, TraccarClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<TraccarOptions>>().Value;
            if (opts.TryGetBaseUri(out var baseUri))
                client.BaseAddress = baseUri;
            else if (opts.Enabled)
            {
                var logger = sp.GetRequiredService<ILogger<TraccarClient>>();
                logger.LogWarning(
                    "Traccar:Enabled is true but Traccar:BaseUrl is missing or invalid. " +
                    "Set Traccar:BaseUrl (e.g. http://20.174.1.230:8082) in user secrets or environment.");
            }

            if (!string.IsNullOrWhiteSpace(opts.Username))
            {
                var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{opts.Username}:{opts.Password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
            }
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHostedService<TraccarSyncService>();
        services.AddSingleton<ITraccarSyncState, TraccarSyncState>();
        services.AddScoped<ITraccarSyncOrchestrator, TraccarSyncOrchestrator>();

        return services;
    }

    private static void RegisterFileStorage(IServiceCollection services, IConfiguration configuration)
    {
        var section = FileStorageOptions.SectionName;
        var provider = configuration.GetValue<string>($"{section}:Provider") ?? "Azure";
        var azureConnection = configuration.GetValue<string>($"{section}:AzureConnectionString")
            ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("FileStorage__AzureConnectionString");

        if (string.Equals(provider, "Azure", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(azureConnection)
            && azureConnection != "__SET_IN_USER_SECRETS_OR_ENV__")
        {
            services.AddSingleton(_ => new BlobServiceClient(azureConnection));
            services.AddScoped<IFileStorageService, AzureBlobStorageService>();
            return;
        }

        services.AddScoped<IFileStorageService, LocalFileStorageService>();
    }
}
