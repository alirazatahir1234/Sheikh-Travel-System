namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class MaintenanceSql
{
    public const string VehicleJoin = """
        INNER JOIN Vehicles v ON v.Id = wo.VehicleId AND v.IsDeleted = 0
        """;

    public const string WorkOrderListSelect = """
        wo.Id, wo.WorkOrderNumber, wo.VehicleId,
        v.Name AS VehicleName, v.RegistrationNumber AS VehicleRegistration,
        wo.ServiceTypeName, wo.Status, wo.Priority, wo.MaintenanceType,
        wo.LaborCost, wo.PartsCost, wo.TotalCost,
        wo.EstimatedLaborCost, wo.EstimatedPartsCost,
        wo.WorkshopId, w.Name AS WorkshopName,
        wo.TechnicianId, t.FullName AS TechnicianName,
        wo.StartDate, wo.EstimatedCompletionDate, wo.CompletedAt, wo.CreatedAt
        """;

    public const string WorkOrderListFrom = """
        FROM WorkOrders wo
        INNER JOIN Vehicles v ON v.Id = wo.VehicleId AND v.IsDeleted = 0
        LEFT JOIN Workshops w ON w.Id = wo.WorkshopId AND w.IsDeleted = 0
        LEFT JOIN Technicians t ON t.Id = wo.TechnicianId AND t.IsDeleted = 0
        """;

    public const string RequestListSelect = """
        r.Id, r.RequestNumber, r.VehicleId,
        v.Name AS VehicleName, v.RegistrationNumber AS VehicleRegistration,
        r.DriverId, d.FullName AS DriverName,
        r.RequestDate, r.RequestType, r.Priority, r.IssueCategory, r.Description,
        r.BreakdownLocation, r.DriverRemarks, r.Status, r.WorkOrderId, r.CreatedAt,
        r.PhotosJson, r.DocumentsJson,
        b.Name AS BranchName, dep.Name AS DepartmentName,
        r.RejectionReason,
        r.ApprovedBy, r.ApprovedAt, r.RejectedBy, r.RejectedAt
        """;

    public const string ScheduleListSelect = """
        s.Id, s.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehicleRegistration,
        v.CurrentMileage, s.NextDueMileage AS NextServiceMileage, s.NextDueDate AS DueDate,
        s.ServiceTypeName, s.IntervalType, s.IntervalValue, s.Priority, s.IsActive,
        v.CurrentEngineHours, s.NextDueEngineHours,
        s.LastServiceMileage, s.LastServiceDate, s.LastServiceEngineHours
        """;

    public const string ScheduleListFrom = """
        FROM VehicleMaintenanceSchedules s
        INNER JOIN Vehicles v ON v.Id = s.VehicleId AND v.IsDeleted = 0
        """;

    public const string RequestListFrom = """
        FROM MaintenanceRequests r
        INNER JOIN Vehicles v ON v.Id = r.VehicleId AND v.IsDeleted = 0
        LEFT JOIN Drivers d ON d.Id = r.DriverId AND d.IsDeleted = 0
        LEFT JOIN Branches b ON b.Id = COALESCE(r.BranchId, v.BranchId)
        LEFT JOIN Departments dep ON dep.Id = COALESCE(r.DepartmentId, v.DepartmentId)
        """;

    public const string ServiceHistorySelect = """
        Id, Source, VehicleId, VehicleName, VehicleRegistration, ServiceType, ServiceDate,
        WorkshopName, TechnicianName, TotalCost, LaborCost, PartsCost, DocumentsJson, Notes, Status
        """;

    public const string ServiceHistoryUnion = """
        SELECT m.Id, N'Maintenance' AS Source, m.VehicleId, v.Name AS VehicleName,
               v.RegistrationNumber AS VehicleRegistration,
               COALESCE(NULLIF(m.MaintenanceType, N''), NULLIF(m.Category, N''), m.Description) AS ServiceType,
               m.MaintenanceDate AS ServiceDate,
               w.Name AS WorkshopName, t.FullName AS TechnicianName,
               ISNULL(m.Cost, 0) + ISNULL(m.LaborCost, 0) + ISNULL(m.PartsCost, 0) AS TotalCost,
               ISNULL(m.LaborCost, 0) AS LaborCost, ISNULL(m.PartsCost, 0) AS PartsCost,
               COALESCE(req.DocumentsJson, reqWo.DocumentsJson) AS DocumentsJson,
               COALESCE(NULLIF(m.TechnicianNotes, N''), m.Description) AS Notes,
               CAST(m.Status AS NVARCHAR(20)) AS Status
        FROM Maintenance m
        INNER JOIN Vehicles v ON v.Id = m.VehicleId AND v.IsDeleted = 0
        LEFT JOIN WorkOrders wo ON wo.Id = m.WorkOrderId AND wo.IsDeleted = 0
        LEFT JOIN Workshops w ON w.Id = wo.WorkshopId AND w.IsDeleted = 0
        LEFT JOIN Technicians t ON t.Id = wo.TechnicianId AND t.IsDeleted = 0
        LEFT JOIN MaintenanceRequests req ON req.Id = m.RequestId AND req.IsDeleted = 0
        LEFT JOIN MaintenanceRequests reqWo ON reqWo.Id = wo.RequestId AND reqWo.IsDeleted = 0
        WHERE m.IsDeleted = 0 AND v.TenantId = @TenantId AND m.Status = 3
        UNION ALL
        SELECT wo.Id, N'WorkOrder' AS Source, wo.VehicleId, v.Name AS VehicleName,
               v.RegistrationNumber AS VehicleRegistration,
               COALESCE(NULLIF(wo.ServiceTypeName, N''), N'Service') AS ServiceType,
               COALESCE(wo.CompletedAt, wo.StartDate, wo.CreatedAt) AS ServiceDate,
               w.Name AS WorkshopName, t.FullName AS TechnicianName,
               ISNULL(wo.TotalCost, ISNULL(wo.LaborCost, 0) + ISNULL(wo.PartsCost, 0)) AS TotalCost,
               ISNULL(wo.LaborCost, 0) AS LaborCost, ISNULL(wo.PartsCost, 0) AS PartsCost,
               req.DocumentsJson,
               COALESCE(NULLIF(wo.TechnicianNotes, N''), wo.Notes) AS Notes,
               wo.Status
        FROM WorkOrders wo
        INNER JOIN Vehicles v ON v.Id = wo.VehicleId AND v.IsDeleted = 0
        LEFT JOIN Workshops w ON w.Id = wo.WorkshopId AND w.IsDeleted = 0
        LEFT JOIN Technicians t ON t.Id = wo.TechnicianId AND t.IsDeleted = 0
        LEFT JOIN MaintenanceRequests req ON req.Id = wo.RequestId AND req.IsDeleted = 0
        WHERE wo.IsDeleted = 0 AND wo.TenantId = @TenantId AND wo.Status = N'Completed'
          AND NOT EXISTS (
              SELECT 1 FROM Maintenance m2
              WHERE m2.WorkOrderId = wo.Id AND m2.IsDeleted = 0)
        """;

    public const string WorkshopSelect = """
        w.Id, w.Name, w.WorkshopType, w.Location, w.ContactPerson, w.ContactPhone, w.ContactEmail,
        w.Capacity, w.VendorType, w.ContractDetails, w.SLA, w.Rating, w.IsActive,
        (SELECT COUNT(*) FROM Technicians t WHERE t.WorkshopId = w.Id AND t.IsDeleted = 0 AND t.IsActive = 1) AS ActiveTechnicians
        """;

    public const string VendorSelect = """
        v.Id, v.Name, v.Category, v.ContactPerson, v.ContactPhone, v.ContactEmail,
        v.ProductsJson, v.Rating, v.IsPreferred, v.IsActive
        """;
}
