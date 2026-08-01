using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.Infrastructure.Authentication;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Infrastructure.Persistence.Migrations;
using SheikhTravelSystem.Infrastructure.Services;
using SheikhTravelSystem.Infrastructure.Services.Payments;
using SheikhTravelSystem.Infrastructure.Services.Ocr;
using SheikhTravelSystem.Infrastructure.Traccar;
using Azure.Storage.Blobs;
using SheikhTravelSystem.Infrastructure.Services.Notifications;
using SheikhTravelSystem.Infrastructure.Services.Ai.Tools;
using SheikhTravelSystem.Infrastructure.Services.GpsControl;
using SheikhTravelSystem.Infrastructure.Services.Storage;
using SheikhTravelSystem.Infrastructure.Caching;
using SheikhTravelSystem.Infrastructure.SignalR;

namespace SheikhTravelSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        var cacheOptions = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();
        services.AddMemoryCache();
        if (!string.IsNullOrWhiteSpace(cacheOptions.RedisConnectionString))
        {
            // AbortOnConnectFail=false keeps the app usable when Redis is down;
            // AppCacheService also fail-opens to the factory on Redis errors.
            services.AddStackExchangeRedisCache(o =>
            {
                var redisOpts = StackExchange.Redis.ConfigurationOptions.Parse(
                    cacheOptions.RedisConnectionString);
                redisOpts.AbortOnConnectFail = false;
                redisOpts.ConnectTimeout = 2000;
                redisOpts.SyncTimeout = 2000;
                redisOpts.AsyncTimeout = 2000;
                o.ConfigurationOptions = redisOpts;
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }
        services.AddSingleton<IAppCache, AppCacheService>();

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
        services.AddScoped<IPermissionEngine, PermissionEngine>();
        services.AddScoped<IDataScopeEngine, DataScopeEngine>();
        services.AddScoped<ISecurityEngine, SecurityEngine>();
        services.AddScoped<ITenantModuleService, TenantModuleService>();
        services.AddScoped<ITenantRoleSeedService, TenantRoleSeedService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IDatabaseMigrationRunner, DatabaseMigrationRunner>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
        services.AddScoped<IDatabaseResetService, DatabaseResetService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditEngine, AuditEngine>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<NotificationRecipientResolver>();
        services.AddScoped<INotificationRetentionService, NotificationRetentionService>();
        services.AddScoped<INotificationRealtimePublisher, NotificationRealtimePublisher>();
        services.AddScoped<INotificationChannelSender, EmailNotificationSender>();
        services.AddScoped<INotificationChannelSender, SmsNotificationSender>();
        services.AddScoped<INotificationChannelSender, PushNotificationSender>();
        services.AddScoped<INotificationChannelSender, BrowserNotificationSender>();
        services.AddScoped<INotificationChannelSender, WhatsAppNotificationSender>();
        services.AddSingleton<FcmHttpV1Client>();
        services.AddHttpClient("FcmHttpV1");
        services.AddHostedService<NotificationDispatchHostedService>();
        services.AddHostedService<NotificationRetentionHostedService>();

        // SheikhGo AI Platform
        services.AddScoped<INotificationDecisionEngine, SheikhTravelSystem.Infrastructure.Services.Ai.NotificationDecisionEngine>();
        services.AddScoped<IUserPresenceService, SheikhTravelSystem.Infrastructure.Services.Ai.UserPresenceService>();
        services.AddScoped<IDeviceTokenService, SheikhTravelSystem.Infrastructure.Services.Ai.DeviceTokenService>();
        services.AddScoped<IAlertNotificationAudit, SheikhTravelSystem.Infrastructure.Services.Ai.AlertNotificationAudit>();
        services.AddScoped<IFleetHealthService, SheikhTravelSystem.Infrastructure.Services.Ai.FleetHealthService>();
        services.AddScoped<IAiDigestService, SheikhTravelSystem.Infrastructure.Services.Ai.AiDigestService>();
        services.AddScoped<IAiRecommendationService, SheikhTravelSystem.Infrastructure.Services.Ai.AiRecommendationService>();
        services.AddScoped<IAiPredictionService, SheikhTravelSystem.Infrastructure.Services.Ai.AiPredictionService>();
        services.AddScoped<IAiManagementService, SheikhTravelSystem.Infrastructure.Services.Ai.AiManagementService>();
        services.AddScoped<IAiCopilotService, SheikhTravelSystem.Infrastructure.Services.Ai.AiCopilotService>();

        // Phase 2 AI Tool Engine
        services.AddScoped<AiEntityResolver>();
        services.AddScoped<IAiTool, GetFleetHealthTool>();
        services.AddScoped<IAiTool, GetOfflineVehiclesTool>();
        services.AddScoped<IAiTool, GetCriticalAlertsTool>();
        services.AddScoped<IAiTool, GetMaintenancePrioritiesTool>();
        services.AddScoped<IAiTool, GetDriverRiskTool>();
        services.AddScoped<IAiTool, GetVehicleStatusTool>();
        services.AddScoped<IAiTool, AssignDriverTool>();
        services.AddScoped<IAiTool, SendNotificationTool>();
        services.AddScoped<IAiToolEngine, AiToolEngine>();

        services.AddScoped<SheikhTravelSystem.Infrastructure.Services.Ai.Providers.OllamaAiProvider>();
        services.AddScoped<SheikhTravelSystem.Infrastructure.Services.Ai.Providers.IAiProviderResolver,
            SheikhTravelSystem.Infrastructure.Services.Ai.Providers.AiProviderResolver>();
        services.AddScoped<IAiChatGateway, SheikhTravelSystem.Infrastructure.Services.Ai.AiChatGateway>();
        services.AddHttpClient(SheikhTravelSystem.Infrastructure.Services.Ai.Providers.OllamaAiProvider.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddScoped<IEscalationService, SheikhTravelSystem.Infrastructure.Services.Ai.EscalationService>();
        services.AddHostedService<SheikhTravelSystem.Infrastructure.Services.Ai.EscalationHostedService>();
        services.AddHostedService<SheikhTravelSystem.Infrastructure.Services.Ai.AiJobsHostedService>();

        services.AddScoped<ISmsOtpService, ConsoleSmsOtpService>();
        services.AddScoped<PaymentGatewayPaymentRecorder>();
        services.AddScoped<IPaymentGatewayProvider, StripePaymentGatewayService>();
        services.AddScoped<IPaymentGatewayProvider, JazzCashPaymentGatewayService>();
        services.AddScoped<IPaymentGatewayProvider, EasyPaisaPaymentGatewayService>();
        services.AddScoped<IPaymentGatewayService, ConfiguredPaymentGatewayService>();
        services.AddHostedService<ComplianceReminderHostedService>();
        services.AddHostedService<MaintenanceAlertHostedService>();
        services.AddHostedService<GpsFleetStatusSnapshotHostedService>();
        services.AddHostedService<GpsDailyRollupHostedService>();
        services.AddHostedService<GpsOfflineDetectionHostedService>();
        services.AddHostedService<GpsCommandRetryHostedService>();
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
        services.AddScoped<IReverseGeocodingService, NominatimReverseGeocodingService>();
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
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(90);
        });
        services.AddHostedService<TraccarSyncService>();
        services.AddSingleton<ITraccarSyncState, TraccarSyncState>();
        services.AddScoped<ITraccarSyncOrchestrator, TraccarSyncOrchestrator>();
        services.AddScoped<ITrackerRegistrationService, TrackerRegistrationService>();

        // Stage 16 — GPS Device Control Center
        services.AddScoped<IGpsControlCenterService, GpsControlCenterService>();
        services.AddScoped<IGpsCommandTranslator, GpsCommandTranslator>();
        services.AddScoped<IGpsTransportRouter, GpsTransportRouter>();
        services.AddScoped<IGpsTransportProvider, TraccarGpsTransportProvider>();
        services.AddScoped<IGpsTransportProvider, SmsGpsTransportProvider>();
        services.AddScoped<IGpsTransportProvider, SimulatorGpsTransportProvider>();
        services.AddScoped<IGpsTransportProvider>(_ => new StubGpsTransportProvider("Tcp"));
        services.AddScoped<IGpsTransportProvider>(_ => new StubGpsTransportProvider("Mqtt"));
        services.AddScoped<IGpsTransportProvider>(_ => new StubGpsTransportProvider("Http"));
        services.AddScoped<IGpsTransportProvider>(_ => new StubGpsTransportProvider("Bluetooth"));
        services.AddScoped<IGpsTransportProvider>(_ => new StubGpsTransportProvider("Serial"));
        services.AddScoped<IGpsCommandResultParser, StatusGpsCommandResultParser>();
        services.AddScoped<IGpsCommandResultParser, VersionGpsCommandResultParser>();
        services.AddScoped<IGpsCommandResultParser, IccidGpsCommandResultParser>();
        services.AddScoped<IGpsCommandResultParser, ImsiGpsCommandResultParser>();
        services.AddScoped<IGpsCommandResultParser, ParamGpsCommandResultParser>();
        services.AddScoped<IGpsCommandResultParser, SignalGpsCommandResultParser>();
        services.AddScoped<IGpsCommandResultParserRegistry, GpsCommandResultParserRegistry>();

        // Reverse-geocoding backfill for positions Traccar's own geocoder didn't resolve.
        services.Configure<GeocodingOptions>(configuration.GetSection(GeocodingOptions.SectionName));
        services.AddHttpClient("Nominatim", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<GeocodingOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                string.IsNullOrWhiteSpace(opts.UserAgent) ? "SheikhGoERP/1.0" : opts.UserAgent);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient("GoogleMaps", client =>
        {
            client.BaseAddress = new Uri("https://maps.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(12);
        });
        services.AddSingleton<GpsAddressBackfillHostedService>();
        services.AddSingleton<IGpsAddressBackfillQueue>(sp => sp.GetRequiredService<GpsAddressBackfillHostedService>());
        services.AddHostedService(sp => sp.GetRequiredService<GpsAddressBackfillHostedService>());

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
