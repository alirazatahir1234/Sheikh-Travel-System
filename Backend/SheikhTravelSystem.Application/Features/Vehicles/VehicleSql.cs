namespace SheikhTravelSystem.Application.Features.Vehicles;

internal static class VehicleSql
{
    internal const string ListSelect = """
        v.Id, v.Name, v.RegistrationNumber, v.VehicleCode, v.VIN, v.Make, v.Model, v.Year, v.VehicleType,
        v.SeatingCapacity, v.FuelAverage, v.FuelType, v.CurrentMileage, v.InsuranceExpiryDate,
        v.Status, v.BranchId, v.CreatedAt,
        d.FullName AS DriverName,
        activeAssign.DriverId AS DriverId,
        gd.UniqueId AS GpsImei,
        gd.SimNumber AS GpsSim,
        gd.LastIgnition AS EngineIgnition,
        gd.LastSeenAt AS GpsLastSeenAt,
        CASE
            WHEN (gd.LastSeenAt IS NOT NULL AND gd.LastSeenAt > DATEADD(minute, -30, GETUTCDATE()))
              OR (vcl.LastUpdate IS NOT NULL AND vcl.LastUpdate > DATEADD(minute, -30, GETUTCDATE()))
            THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT)
        END AS GpsOnline,
        CASE WHEN v.GpsDeviceId IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS HasGpsDevice,
        vcl.Latitude AS LocationLatitude,
        vcl.Longitude AS LocationLongitude,
        vcl.LastUpdate AS LocationLastUpdate,
        svc.NextDueDate AS NextServiceDue,
        svc.NextDueMileage AS NextDueMileage,
        svc.ServiceAlert AS ServiceAlert,
        vimg.FileUrl AS ImageUrl
        """;

    internal const string ListFrom = """
        FROM Vehicles v
        OUTER APPLY (
            SELECT TOP 1 ah.DriverId
            FROM AssignmentHistory ah
            WHERE ah.VehicleId = v.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
            ORDER BY ah.StartAt DESC
        ) activeAssign
        LEFT JOIN Drivers d ON d.Id = activeAssign.DriverId AND d.IsDeleted = 0
        LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
        LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
        OUTER APPLY (
            SELECT TOP 1 m.NextDueDate, m.NextDueMileage,
                CASE
                    WHEN m.NextDueDate IS NOT NULL AND m.NextDueDate < GETUTCDATE() THEN N'Service Due'
                    WHEN m.Status IN (1, 2) THEN N'Service Due'
                    ELSE NULL
                END AS ServiceAlert
            FROM Maintenance m
            WHERE m.VehicleId = v.Id AND m.IsDeleted = 0
            ORDER BY
                CASE WHEN m.NextDueDate IS NOT NULL AND m.NextDueDate < GETUTCDATE() THEN 0 ELSE 1 END,
                m.NextDueDate ASC
        ) svc
        OUTER APPLY (
            SELECT TOP 1 vd.FileUrl
            FROM VehicleDocuments vd
            WHERE vd.VehicleId = v.Id
              AND vd.DocumentType = N'VehicleImage'
              AND vd.IsDeleted = 0
              AND vd.FileUrl IS NOT NULL
              AND LTRIM(RTRIM(vd.FileUrl)) <> N''
            ORDER BY
                CASE
                    WHEN vd.Notes LIKE N'%|primary%' OR LOWER(LTRIM(RTRIM(vd.Notes))) = N'primary' THEN 0
                    ELSE 1
                END,
                vd.CreatedAt DESC
        ) vimg
        """;

    internal const string DetailColumns = """
        v.Id, v.Name, v.RegistrationNumber, v.VehicleCode, v.VIN, v.Make, v.Model, v.Year, v.Color, v.VehicleType,
        v.SeatingCapacity, v.FuelAverage, v.FuelType, v.EngineNo, v.ChassisNo,
        v.CurrentMileage, v.InsuranceExpiryDate, v.GpsDeviceId, v.PurchaseDate, v.PurchasePrice, v.PurchaseCurrencyCode,
        v.BranchId, v.DepartmentId, v.Status,
        CASE WHEN v.Status <> 4 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsActive,
        v.CreatedAt, v.UpdatedAt,
        d.FullName AS DriverName,
        activeAssign.DriverId AS DriverId,
        d.Phone AS DriverPhone,
        d.LicenseNumber AS DriverLicenseNumber,
        gd.UniqueId AS GpsImei,
        gd.SimNumber AS GpsSim,
        gd.LastIgnition AS EngineIgnition,
        gd.LastSeenAt AS GpsLastSeenAt,
        CASE
            WHEN (gd.LastSeenAt IS NOT NULL AND gd.LastSeenAt > DATEADD(minute, -30, GETUTCDATE()))
              OR (vcl.LastUpdate IS NOT NULL AND vcl.LastUpdate > DATEADD(minute, -30, GETUTCDATE()))
            THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT)
        END AS GpsOnline,
        CASE WHEN v.GpsDeviceId IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS HasGpsDevice,
        vcl.Latitude AS LocationLatitude,
        vcl.Longitude AS LocationLongitude,
        vcl.LastUpdate AS LocationLastUpdate,
        vcl.Speed AS LocationSpeed,
        vcl.TotalDistanceKm AS TotalDistanceKm,
        vcl.BatteryLevel AS BatteryLevel,
        vcl.GsmSignal AS GsmSignal,
        vcl.Address AS Address,
        vimg.FileUrl AS ImageUrl,
        gd.Name AS TrackerName,
        COALESCE(tm.Name, gd.Model) AS TrackerModel,
        tb.Name AS TrackerBrand,
        gd.InstallationDate AS TrackerInstallationDate,
        svc.NextDueDate AS NextServiceDue,
        svc.NextDueMileage AS NextDueMileage,
        svc.ServiceAlert AS ServiceAlert
        """;

    internal const string DetailFrom = """
        FROM Vehicles v
        OUTER APPLY (
            SELECT TOP 1 ah.DriverId
            FROM AssignmentHistory ah
            WHERE ah.VehicleId = v.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
            ORDER BY ah.StartAt DESC
        ) activeAssign
        LEFT JOIN Drivers d ON d.Id = activeAssign.DriverId AND d.IsDeleted = 0
        LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
        LEFT JOIN TrackerModels tm ON tm.Id = gd.TrackerModelId
        LEFT JOIN TrackerBrands tb ON tb.Id = tm.TrackerBrandId
        LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
        OUTER APPLY (
            SELECT TOP 1 m.NextDueDate, m.NextDueMileage,
                CASE
                    WHEN m.NextDueDate IS NOT NULL AND m.NextDueDate < GETUTCDATE() THEN N'Service Due'
                    WHEN m.Status IN (1, 2) THEN N'Service Due'
                    ELSE NULL
                END AS ServiceAlert
            FROM Maintenance m
            WHERE m.VehicleId = v.Id AND m.IsDeleted = 0
            ORDER BY
                CASE WHEN m.NextDueDate IS NOT NULL AND m.NextDueDate < GETUTCDATE() THEN 0 ELSE 1 END,
                m.NextDueDate ASC
        ) svc
        OUTER APPLY (
            SELECT TOP 1 vd.FileUrl
            FROM VehicleDocuments vd
            WHERE vd.VehicleId = v.Id
              AND vd.DocumentType = N'VehicleImage'
              AND vd.IsDeleted = 0
              AND vd.FileUrl IS NOT NULL
              AND LTRIM(RTRIM(vd.FileUrl)) <> N''
            ORDER BY
                CASE
                    WHEN vd.Notes LIKE N'%|primary%' OR LOWER(LTRIM(RTRIM(vd.Notes))) = N'primary' THEN 0
                    ELSE 1
                END,
                vd.CreatedAt DESC
        ) vimg
        """;
}
