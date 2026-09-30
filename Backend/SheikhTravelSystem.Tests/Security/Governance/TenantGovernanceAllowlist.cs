namespace SheikhTravelSystem.Tests.Security.Governance;

/// <summary>
/// Narrow, documented exceptions to Phase 3C SQL governance.
/// Every entry must explain why tenant isolation is intentional or deferred.
/// </summary>
public static class TenantGovernanceAllowlist
{
    public sealed record Entry(
        string RelativePathGlob,
        string Table,
        string Operation,
        string Reason);

    /// <summary>
    /// Path substring (relative to Infrastructure) + table + operation family.
    /// Operation: Insert | SelectUpdateDelete | ParentJoin | RepositoryContext
    /// </summary>
    public static readonly IReadOnlyList<Entry> Entries =
    [
        new("Persistence/Repositories/PublicLeadRepository.cs", "WebsiteContactRequests", "Insert",
            "PublicByDesign marketing inbox — hardcoded TenantId = 1; anonymous forms must not use GetRequiredTenantId()."),
        new("Persistence/Repositories/PublicLeadRepository.cs", "WebsiteDemoRequests", "Insert",
            "PublicByDesign marketing inbox — hardcoded TenantId = 1."),

        new("Services/Gps/GpsAlertWriter.cs", "GpsAlertEvents", "Insert",
            "Background GPS alert writer; TenantId backfill deferred beyond Phase 3C method list (ingest path now sets TenantId on GpsPositions)."),
        new("Services/Gps/GpsTripPersistenceService.cs", "GpsTrips", "Insert",
            "Trip persistence helper; TenantId write deferred with GPS analytics hardening."),
        new("Services/Payments/PaymentGatewayPaymentRecorder.cs", "Payments", "Insert",
            "Payment gateway recorder — deferred payment-path hardening; primary PaymentRepository already tenant-scoped."),
        new("Traccar/TraccarSyncOrchestrator.cs", "GpsDevices", "Insert",
            "Traccar import may create devices before vehicle link; TrackerTenantSql allows NULL TenantId + vehicle ownership."),

        new("Persistence/Repositories/GpsDeviceRepository.cs", "GpsDeviceCommands", "SelectUpdateDelete",
            "CompleteDeviceCommand / GetPendingDeviceCommands are UniqueId device-facing paths without JWT tenant."),
        new("Persistence/Repositories/GpsDeviceRepository.cs", "GpsDevices", "SelectUpdateDelete",
            "Device UniqueId poll paths intentionally unconstrained by JWT tenant."),

        new("Persistence/Repositories/PublicLeadRepository.cs", "*", "RepositoryContext",
            "Anonymous public forms — no ITenantContext by design."),
        new("Persistence/Repositories/TenantRepository.cs", "*", "RepositoryContext",
            "Platform tenant catalog — operates across tenants by design."),
        new("Persistence/Repositories/PlatformRepository", "*", "RepositoryContext",
            "Platform / SuperAdmin repository surface — cross-tenant by design."),
        new("Persistence/Repositories/AuthRepository.cs", "*", "RepositoryContext",
            "Auth resolves tenant from credentials/claims; not a tenant-filtered CRUD repo."),
        new("Persistence/Repositories/WebsiteRepository.cs", "*", "RepositoryContext",
            "Public website CMS content — platform-wide by design."),

        // Seeders and migrations are out of governance scope (not runtime request paths).
        new("Persistence/DatabaseSeeder.cs", "*", "Insert", "Seed data only — not runtime API path."),
        new("Persistence/Migrations/", "*", "Insert", "Schema/seed migrations — not runtime API path.")
    ];

    public static bool IsAllowed(string relativePath, string table, string operation)
    {
        relativePath = relativePath.Replace('\\', '/');
        foreach (var e in Entries)
        {
            var glob = e.RelativePathGlob.Replace('\\', '/');
            var pathMatch = relativePath.Contains(glob, StringComparison.OrdinalIgnoreCase)
                            || (glob.EndsWith('/') && relativePath.StartsWith(glob, StringComparison.OrdinalIgnoreCase));
            if (!pathMatch)
                continue;

            var tableMatch = e.Table == "*"
                             || string.Equals(e.Table, table, StringComparison.OrdinalIgnoreCase);
            if (!tableMatch)
                continue;

            if (string.Equals(e.Operation, operation, StringComparison.OrdinalIgnoreCase)
                || e.Operation == "*")
                return true;
        }

        return false;
    }
}
