namespace SheikhTravelSystem.Tests.Security.Sg01;

/// <summary>
/// SG-01 Stage 1 allowlist — seeded from every LEAK + AUTHORIZATION BUG row in
/// <c>docs/audit/SG-01-sweep.md</c>.
/// <para>
/// <b>This allowlist may only shrink.</b> Do not add entries for new debt.
/// Remove an entry when the corresponding method is fixed (Stage 2/3).
/// Legitimate platform-scope SQL uses <c>// PLATFORM-SCOPE</c> instead.
/// </para>
/// </summary>
public static class Sg01TenantSqlGuardAllowlist
{
    public sealed record Entry(
        string FileName,
        string MethodName,
        string Classification,
        string SweepNote);

    /// <summary>
    /// FileName is the .cs leaf name. MethodName is the C# method (or "*" for whole file).
    /// Matching is case-insensitive; MethodName also matches MethodName+"Async".
    /// </summary>
    public static readonly IReadOnlyList<Entry> Entries =
    [
        // --- LEAK (from SG-01-sweep.md) ---
        new("CompanyRepository.cs", "GetPasswordChangedAtAsync", "LEAK", "Users by UserId"),
        new("CustomerPortalRepository.cs", "GetBookedSeatsNearPickupAsync", "LEAK", "BookingSeats/Bookings"),
        new("CustomerPortalRepository.cs", "GetBookingCardsAsync", "LEAK", "Bookings/Payments/Routes"),
        new("CustomerPortalRepository.cs", "GetBookingDetailHeadAsync", "LEAK", "Bookings detail"),
        new("CustomerPortalRepository.cs", "GetBookingNumberAsync", "LEAK", "Bookings by Id"),
        new("CustomerPortalRepository.cs", "GetBookingPaymentsAsync", "LEAK", "Payments by BookingId"),
        new("CustomerPortalRepository.cs", "GetBookingSeatsAsync", "LEAK", "BookingSeats by BookingId"),
        new("CustomerPortalRepository.cs", "GetBookingStatusPickupAsync", "LEAK", "Bookings by Id"),
        new("CustomerPortalRepository.cs", "GetBookingStatusVehicleAsync", "LEAK", "Bookings by Id"),
        new("CustomerPortalRepository.cs", "GetCheckoutBookingAsync", "LEAK", "Bookings checkout"),
        new("CustomerPortalRepository.cs", "GetCustomerNotificationsAsync", "LEAK", "CustomerNotifications"),
        new("CustomerPortalRepository.cs", "GetFavoriteRoutesAsync", "LEAK", "Favorite routes"),
        new("CustomerPortalRepository.cs", "GetFirstActiveRouteIdAsync", "LEAK", "Routes unscoped"),
        new("CustomerPortalRepository.cs", "GetInvoiceRowAsync", "LEAK", "Invoice row"),
        new("CustomerPortalRepository.cs", "GetLoyaltyAsync", "LEAK", "CustomerLoyalty"),
        new("CustomerPortalRepository.cs", "GetPerKmBasePriceAsync", "LEAK", "Routes"),
        new("CustomerPortalRepository.cs", "GetRouteBasePriceAsync", "LEAK", "Routes by Id"),
        new("CustomerPortalRepository.cs", "GetRouteLabelAsync", "LEAK", "Routes by Id"),
        new("CustomerPortalRepository.cs", "GetRoutesAsync", "LEAK", "Routes unscoped"),
        new("CustomerPortalRepository.cs", "GetSavedAddressesAsync", "LEAK", "CustomerSavedAddresses"),
        new("CustomerPortalRepository.cs", "GetStartedBookingDriverPhoneAsync", "LEAK", "Bookings/Drivers"),
        new("CustomerPortalRepository.cs", "GetVehicleLabelAsync", "LEAK", "Vehicles by Id"),
        new("CustomerPortalRepository.cs", "GetVehicleLiveLocationAsync", "LEAK", "VehicleCurrentLocation"),
        new("CustomerPortalRepository.cs", "GetVehicleSeatLayoutsAsync", "LEAK", "VehicleSeatLayouts"),
        new("CustomerPortalRepository.cs", "GetVehicleSeatingCapacityAsync", "LEAK", "Vehicles by Id"),
        new("CustomerPortalRepository.cs", "GetVehiclesAsync", "LEAK", "Vehicles unscoped"),
        new("CustomerPortalRepository.cs", "GetWalletBalanceAsync", "LEAK", "CustomerWallets"),
        new("CustomerPortalRepository.cs", "IsSeatTakenAsync", "LEAK", "BookingSeats/Bookings"),
        new("CustomerPortalRepository.cs", "NormalizeCustomerPhonesAsync", "LEAK", "Customers"),
        new("CustomerPortalRepository.cs", "VehicleExistsAndNotRetiredAsync", "LEAK", "Vehicles by Id"),
        new("DriverAppRepository.cs", "GetDriverFullNameAsync", "LEAK", "Drivers by Id"),
        new("DriverAppRepository.cs", "GetVehicleNameAsync", "LEAK", "Vehicles by Id"),
        new("FleetRepository.cs", "GetComplianceDocumentsAsync", "LEAK", "Compliance join EntityId only"),
        new("FleetRepository.cs", "GetAssignmentsAsync", "LEAK", "Drivers join unscoped"),
        new("ComplianceReminderHostedService.cs", "ScanAsync", "LEAK", "CROSS APPLY docs by EntityId"),
        new("GpsDeviceRepository.cs", "GetPendingDeviceCommandsAsync", "LEAK", "by UniqueId"),
        new("GpsTrackingRepository.cs", "GetGpsEtaAsync", "LEAK", "by booking/vehicle Id"),
        new("GpsTrackingRepository.cs", "IngestPositionAsync", "LEAK", "by DriverId/VehicleId"),
        new("MaintenanceModuleRepository.cs", "CreateMaintenanceScheduleAsync", "LEAK", "Vehicles existence read"),
        new("PlatformRepository.Access.cs", "GetUserPasswordChangedAtAsync", "LEAK", "Users by Id"),
        new("TripRepository.cs", "GetDetailAsync", "LEAK", "child collections by TripId"),
        new("TripRepository.cs", "OptimizeRouteAsync", "LEAK", "Routes by Id"),
        new("UserRepository.cs", "ExistsAsync", "LEAK", "Users IDOR"),
        new("UserRepository.cs", "GetByIdAsync", "LEAK", "Users IDOR"),
        new("UserRepository.cs", "GetOrgInfoAsync", "LEAK", "Users IDOR"),
        new("UserRepository.cs", "GetPasswordInfoAsync", "LEAK", "Users IDOR"),
        new("VehicleRepository.cs", "GetMaintenancePagedAsync", "LEAK", "Maintenance by VehicleId"),
        new("FleetIntelligenceServices.cs", "ComputeAsync", "LEAK", "Maintenance/GpsAlertEvents global"),
        new("FleetIntelligenceServices.cs", "GenerateRecommendationsAsync", "LEAK", "Maintenance overdue"),
        new("PredictionCopilotManagement.cs", "CaptureFeaturesAsync", "LEAK", "Maintenance by VehicleId"),
        new("DriverTelematicsEnforcementService.cs", "ProcessAlertAsync", "LEAK", "Vehicles by VehicleId"),
        new("FleetAnomalyEvaluator.cs", "EvaluateAsync", "LEAK", "Vehicles by VehicleId"),
        new("FuelStationStopEvaluator.cs", "EvaluateAsync", "LEAK", "Vehicles by Id"),
        new("GpsAlertWriter.cs", "InsertAsync", "LEAK", "Vehicles by Id"),
        new("GpsCommandSafetyChecker.cs", "CheckEngineCutoffPreconditionAsync", "LEAK", "VehicleCurrentLocation"),
        new("GpsHarshBrakingAlertDispatch.cs", "DispatchIfAllowedAsync", "LEAK", "Vehicles by Id"),
        new("GpsPositionIngestionHelper.cs", "IngestAsync", "LEAK", "Bookings/Vehicles"),
        new("GpsPositionIngestionHelper.cs", "ResolveActiveBookingIdAsync", "LEAK", "Bookings by VehicleId"),
        new("GpsTripPersistenceService.cs", "*", "LEAK", "position/trip loads by VehicleId"),
        new("RoadSpeedAlertEvaluator.cs", "EvaluateAsync", "LEAK", "Vehicles by Id"),
        new("GpsCommandTranslator.cs", "TranslateAsync", "LEAK", "GpsDevices by Id"),
        new("LocationBroadcastService.cs", "ResolveTenantIdAsync", "LEAK", "Vehicles by VehicleId"),
        new("PaymentGatewayPaymentRecorder.cs", "RecordAsync", "LEAK", "Bookings/Payments"),
        new("TraccarSyncOrchestrator.cs", "SyncDevicesAsync", "LEAK", "GpsDevices sync"),
        new("TraccarSyncOrchestrator.cs", "SyncEventsAsync", "LEAK", "GpsAlertEvents sync"),
        new("TraccarSyncOrchestrator.cs", "SyncTrackerAsync", "LEAK", "GpsDevices sync"),
        new("TrackerRegistrationService.cs", "Register", "LEAK", "GpsDevices"),
        new("TrackerRegistrationService.cs", "Update", "LEAK", "GpsDevices"),
        new("TrackerRegistrationService.cs", "Delete", "LEAK", "GpsDevices"),
        new("TrackerRegistrationService.cs", "Install", "LEAK", "GpsDevices"),
        new("TrackerRegistrationService.cs", "Transfer", "LEAK", "GpsDevices"),
        new("TrackerRegistrationService.cs", "Uninstall", "LEAK", "GpsDevices"),

        // --- AUTHORIZATION BUG (from SG-01-sweep.md) ---
        new("PlatformHandlers.cs", "*", "AUTHORIZATION BUG", "request.TenantId + EnsureTenantAccess"),
        new("CreateUserCommand.cs", "ResolveTargetTenantId", "AUTHORIZATION BUG", "CompanyId from body"),
        new("UpdateUserCommand.cs", "ResolveTargetTenantId", "AUTHORIZATION BUG", "CompanyId from body"),
        new("GetUsersQuery.cs", "ResolveTenantFilter", "AUTHORIZATION BUG", "request.TenantId from query"),
        new("VehicleRepository.cs", "GetCrossTenantPagedAsync", "AUTHORIZATION BUG", "optional CompanyId"),
    ];

    public static bool IsAllowed(string fileName, string methodName)
    {
        fileName = Path.GetFileName(fileName);

        // Stage 1: every sweep-listed file is fully allowlisted until Stage 2/3 removes
        // entries. Method rows remain as the shrink checklist (allowlist may only shrink).
        if (Entries.Any(e => FileMatches(e.FileName, fileName)))
            return true;

        return false;
    }

    /// <summary>
    /// True when this file still has at least one allowlist row (used by Stage 2/3 shrink).
    /// </summary>
    public static bool IsMethodAllowlisted(string fileName, string methodName)
    {
        fileName = Path.GetFileName(fileName);
        foreach (var e in Entries)
        {
            if (!FileMatches(e.FileName, fileName))
                continue;

            if (e.MethodName == "*")
                return true;

            if (MethodMatches(e.MethodName, methodName))
                return true;
        }

        return false;
    }

    public static bool FileMatches(string allowlistedFileName, string actualFileName)
    {
        actualFileName = Path.GetFileName(actualFileName);
        if (string.Equals(allowlistedFileName, actualFileName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Partial repositories: GpsTrackingRepository.cs covers GpsTrackingRepository.Ingest.cs etc.
        if (allowlistedFileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            var stem = allowlistedFileName[..^3];
            if (actualFileName.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase)
                && actualFileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // PlatformRepository.Access.cs ↔ PlatformRepository*.cs
        if (allowlistedFileName.StartsWith("PlatformRepository", StringComparison.OrdinalIgnoreCase)
            && actualFileName.StartsWith("PlatformRepository", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static bool MethodMatches(string allowlisted, string actual)
    {
        if (string.Equals(allowlisted, actual, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(allowlisted + "Async", actual, StringComparison.OrdinalIgnoreCase))
            return true;
        if (actual.EndsWith("Async", StringComparison.OrdinalIgnoreCase)
            && string.Equals(allowlisted, actual[..^5], StringComparison.OrdinalIgnoreCase))
            return true;
        // TrackerRegistrationService: Register matches RegisterAsync / RegisterDeviceAsync prefixes
        if (actual.StartsWith(allowlisted, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
