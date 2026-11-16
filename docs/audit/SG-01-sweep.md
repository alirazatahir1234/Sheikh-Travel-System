# SG-01 Pass 1 — Tenant isolation sweep

**Branch:** `fix/SG-01-compliance-tenant-isolation`  
**Scope:** investigation only — no repository/query code changes in this pass.  
**Classifications:**
- **OK** — filters on `TenantId` from `ITenantContext` (or intentional platform-scope / background all-tenant scanner with per-row tenant routing)
- **AUTHORIZATION BUG** — filters on a `TenantId` that came from the request (body/query/route), even if gated by `EnsureTenantAccess`
- **LEAK** — no `TenantId` filter on a tenant-owned read

## Summary counts

| Classification | Count |
|---|---:|
| LEAK | 41 |
| AUTHORIZATION BUG | 8 |
| OK | 152 |
| **Total paths reviewed** | **201** |

## Smoking gun — compliance (Hannan 1 driver / 39 docs)

`FleetRepository.GetComplianceDocumentsAsync` **does** filter `c.TenantId = @TenantId` and the handler uses `ITenantContext.GetRequiredTenantId()` only.

Likely blast radius is **not** a missing `WHERE` on ComplianceDocuments itself, but:
1. **Tenant source mismatch:** `GetDriversQuery` accepts `request.TenantId` (UI company filter / AUTHORIZATION BUG pattern) while `/fleet/compliance` uses JWT/`ITenantContext` only — a platform operator can see company A drivers and company B (JWT home) documents at once.
2. **Join LEAK:** `LEFT JOIN Drivers/Vehicles` on `EntityId` without `d.TenantId` / `v.TenantId`, so contaminated `EntityId` values disclose other tenants' names.
3. **Data contamination risk:** docs written under the wrong `TenantId` (upload path must be checked in Pass 2).

Pass 2 must print full SQL for compliance list + driver list tenant resolution + document upload INSERT.

## Pass 2 size estimate

- **~3 days** to close all **41 LEAK** + **8 AUTHORIZATION BUG** paths cleanly (Trip child-table reads, portal, AI health, user-by-id, WhatsApp).
- **~1 day** if Pass 2 is scoped to compliance + Drivers/Vehicles tenant-source unification + top portal/AI LEAKs only.
- Recommendation: start Pass 2 with compliance (1 day critical path), then continue remaining LEAKs on the same branch.

## Full table

| file | method | table(s) | classification | WHERE clause |
|---|---|---|---|---|
| ComplianceReminderHostedService.cs | ScanAsync driverVehicleDocs CROSS APPLY ComplianceDocuments | ComplianceDocuments | LEAK | `c.EntityType/EntityId/IsDeleted/Expiry` only — missing `c.TenantId = v.TenantId` (background scanner; wrong-doc risk) |
| FleetRepository.cs | GetComplianceDocumentsAsync (JOIN Vehicles/Drivers) | Vehicles, Drivers | LEAK | LEFT JOIN Vehicles/Drivers on EntityId only — no `v.TenantId`/`d.TenantId`; EntityName can disclose other tenants' names if EntityId is contaminated |
| CustomerPortalRepository.cs | GetBookedSeatsNearPickupAsync | Bookings | LEAK | vehicle/time window only — no TenantId |
| CustomerPortalRepository.cs | GetVehiclesAsync | Vehicles | LEAK | `WHERE IsDeleted = 0 AND Status <> @Retired` — NONE on TenantId (lists all companies' vehicles) |
| CustomerPortalRepository.cs | IsSeatTakenAsync | Bookings | LEAK | vehicle/seat/time — no TenantId |
| DriverAppRepository.cs | GetDashboardStatsAsync (earnings subquery) | Payments, Bookings | LEAK | bookings/earnings path omits `TenantId` on Payments join |
| DriverAppRepository.cs | GetDriverFullNameAsync | Drivers | LEAK | `Id = @Id` — NONE on TenantId |
| DriverAppRepository.cs | GetInspectionHistoryAsync | Inspections, Vehicles | LEAK | `i.DriverId = @DriverId AND i.IsDeleted = 0 AND (i.TenantId IS NULL OR i.TenantId = @TenantId)` |
| DriverAppRepository.cs | GetVehicleNameAsync | Vehicles | LEAK | `Id = @Id` — NONE on TenantId |
| FleetIntelligenceServices.cs (AiDigestService) | GenerateMorningDigestAsync — maintenanceDue / overspeed / critical | Maintenance, GpsAlertEvents | LEAK | Maintenance/GpsAlertEvents: NONE on TenantId |
| FleetIntelligenceServices.cs (AiRecommendationService) | RefreshAsync — overdue Maintenance | Maintenance | LEAK | `IsDeleted = 0 AND NextDueDate IS NOT NULL AND NextDueDate < GETUTCDATE()` — NONE on TenantId |
| FleetIntelligenceServices.cs (FleetHealthService) | ComputeAsync — critical / overspeed | GpsAlertEvents | LEAK | `IsDeleted = 0` … — NONE on TenantId |
| FleetIntelligenceServices.cs (FleetHealthService) | ComputeAsync — maintenance score | Maintenance | LEAK | `FROM Maintenance WHERE IsDeleted = 0` — NONE on TenantId |
| CompanyRepository.cs | GetAssignedRolesAsync | UserRoles, Roles | LEAK | `ur.UserId = @UserId` (NONE on TenantId) |
| CompanyRepository.cs | GetPasswordChangedAtAsync | Users | LEAK | `Id = @UserId` (NONE on TenantId) |
| NotificationRepository.cs | GetDeliveryLogsAsync | NotificationDeliveryLogs | LEAK | `NotificationId = @NotificationId` (NONE on TenantId); gated earlier by UserCanAccessNotificationAsync |
| NotificationRepository.cs | GetPreferencesAsync | NotificationPreferences | LEAK | `UserId = @UserId` (NONE on TenantId) |
| NotificationRepository.cs | GetRetentionEstimateAsync | NotificationRecipients, Notifications | LEAK | archive/delete/critical counts with **NONE** TenantId (cross-tenant aggregate) |
| TripRepository.cs | AddPassengerAsync / SoftDeletePassengerAsync (COUNT) | TripPassengers | LEAK | `TripId = @TripId AND IsDeleted = 0` (no TenantId) |
| TripRepository.cs | AssignDriverAsync (TripNumber) | Trips | LEAK | `Id = @Id` |
| TripRepository.cs | ClearDriverAssignmentAsync (TripNumber) | Trips | LEAK | `Id = @Id` |
| TripRepository.cs | CreateAsync (Customers EXISTS) | Customers | LEAK | `Id = @Id AND IsDeleted = 0` |
| TripRepository.cs | CreateAsync (Routes EXISTS) | Routes | LEAK | `Id = @Id AND IsDeleted = 0` |
| TripRepository.cs | GetDetailAsync (documents) | TripDocuments | LEAK | `TripId = @Id AND IsDeleted = 0` (no TenantId; after parent load) |
| TripRepository.cs | GetDetailAsync (expenses) | TripExpenses | LEAK | `TripId = @Id AND IsDeleted = 0` (no TenantId; after parent load) |
| TripRepository.cs | GetDetailAsync (openAlerts) | GpsAlertEvents | LEAK | `VehicleId = @VehicleId AND IsDeleted = 0 AND (Status IS NULL OR LOWER(Status)='active') AND Timestamp >= ...` |
| TripRepository.cs | GetDetailAsync (passengers) | TripPassengers | LEAK | `TripId = @Id AND IsDeleted = 0` (no TenantId; after parent load) |
| TripRepository.cs | GetDetailAsync (stops) | TripStops | LEAK | `TripId = @Id AND IsDeleted = 0` (no TenantId; after parent load) |
| TripRepository.cs | GetDetailAsync (timeline) | TripStatusHistory | LEAK | `TripId = @Id` (no TenantId; after parent load) |
| TripRepository.cs | GetRouteSummaryAsync (live loc) | VehicleCurrentLocation | LEAK | `VehicleId = @VehicleId` |
| TripRepository.cs | OptimizeRouteAsync (route) | Routes | LEAK | `Id = @Id AND IsDeleted = 0` |
| TripRepository.cs | UpdateStatusAsync (TripNumber) | Trips | LEAK | `Id = @Id` |
| UserRepository.cs | ExistsAsync | Users | LEAK | typically by Id only (NONE on TenantId) |
| UserRepository.cs | GetAssignedRolesAsync | UserRoles, Roles | LEAK | `ur.UserId = @UserId` (NONE on TenantId); handler gate: `EnsureTenantAccess` via prior GetTenantIdAsync |
| UserRepository.cs | GetByIdAsync | Users, Tenants, Branches, Departments, UserRoles, Roles | LEAK | `u.Id = @Id AND u.IsDeleted = 0` (NONE on TenantId); handler gate after read: `EnsureTenantAccess(user.CompanyId)` |
| UserRepository.cs | GetOrgInfoAsync | Users | LEAK | `Id = @Id AND IsDeleted = 0` (NONE on TenantId) |
| UserRepository.cs | GetPasswordInfoAsync | Users | LEAK | `Id = @UserId AND IsDeleted = 0` (NONE on TenantId) |
| UserRepository.cs | GetProfileAsync | Users, Tenants, Branches, Departments, UserRoles, Roles | LEAK | `u.Id = @Id AND u.IsDeleted = 0` (NONE on TenantId); self JWT UserId |
| UserRepository.cs | GetTenantIdAsync | Users | LEAK | `Id = @Id AND IsDeleted = 0` (NONE on TenantId) |
| VehicleRepository.cs | GetMaintenancePagedAsync | Maintenance | LEAK | `IsDeleted = 0 AND VehicleId = @VehicleId` — NONE on TenantId (vehicle existence pre-check only) |
| WhatsAppAutomationRepository.cs | ListActiveEnRouteTripVehicleIdsAsync | Trips | LEAK | `IsDeleted=0 AND VehicleId IS NOT NULL AND Status=@Started` — NONE on TenantId |
| GetDriverStatsQueryHandler | Handle | Drivers | AUTHORIZATION BUG | `request.TenantId ?? platformScope.TenantId` with EnsureTenantAccess |
| GetDriversQueryHandler | Handle / ResolveTenantFilter | Drivers | AUTHORIZATION BUG | `request.TenantId` via EnsureTenantAccess, else platformScope.TenantId — smoking-gun mismatch vs compliance JWT-only path |
| CompanyRepository.cs | GetCompanyFeaturesAsync | TenantFeatures / feature registry | AUTHORIZATION BUG | uses `request.TenantId ?? ITenantContext` (gate: `EnsureTenantAccess`) |
| UserRepository.cs | EmailExistsInTenantAsync / EmailExistsForOtherAsync | Users | AUTHORIZATION BUG | `Email... AND TenantId = @TenantId`; depends whether caller passes context vs request CompanyId (`EnsureTenantAccess` on create/update) |
| UserRepository.cs | EnsureOrgBelongsToTenantAsync | Branches, Departments | AUTHORIZATION BUG | `Id = @... AND TenantId = @TenantId`; tenantId from caller |
| UserRepository.cs | GetActiveRoleCodesAsync / ActivePlatformRoleExistsAsync | Roles | AUTHORIZATION BUG | `TenantId = @TenantId AND ...`; tenantId from caller after access checks |
| UserRepository.cs | GetCompanyUserSummaryAsync | Users, Departments | AUTHORIZATION BUG | `TenantId = @TenantId`; SQL uses request/query `tenantId` when supplied (gate: `EnsureTenantAccess`); else `platformScope.TenantId` |
| UserRepository.cs | GetPagedAsync | Users, Tenants, Branches, Departments | AUTHORIZATION BUG | `u.IsDeleted = 0 AND u.TenantId = @TenantId`; OK when `platformScope.TenantId`; AUTHORIZATION BUG when query `TenantId` used (gate: `EnsureTenantAccess`) |
| ComplianceReminderHostedService.cs | ScanAsync — docExpiring (VehicleDocuments) | VehicleDocuments, Vehicles | OK | via `v.TenantId`; notify uses `v.TenantId` |
| ComplianceReminderHostedService.cs | ScanAsync — driverVehicleDocs CROSS APPLY | ComplianceDocuments, Drivers, Vehicles, AssignmentHistory | OK | `c.EntityType = N'Vehicle' AND c.EntityId = v.Id AND c.IsDeleted = 0 AND c.ExpiryDate IS NOT NULL AND c.ExpiryDate <= @Threshold` |
| DriverAppRepository.cs | GetDriverComplianceDocsAsync | ComplianceDocuments | OK | `EntityType = N'Driver' AND EntityId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0` |
| DriverAppRepository.cs | GetDriverLoginByPhoneAsync / GetProfileAsync / GetDriverStatusAsync / GetDriverComplianceProfileAsync | Drivers | OK | `d.TenantId = @TenantId` (+ Id/phone) |
| DriverAppRepository.cs | GetVehicleComplianceDocsAsync | VehicleDocuments | OK | `VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0` |
| DriverRepository.cs | CheckAvailability / Exists / GetById / GetPaged / GetStats / GetAssignmentGuardRow / VehicleExists / GetTimeline / GetActiveDuty / GetDocuments / GetReviewNotes / GetDriversForVerificationExpiryScan / GetDriverUserId (+ other SELECTs with `@TenantId`) | Drivers, ComplianceDocuments, AssignmentHistory, FuelLogs, Bookings, Vehicles | OK | includes `TenantId = @TenantId` (or `d/c/a.TenantId = @TenantId`); docs callers use `ITenantContext` |
| DriverRepository.cs | GetDocumentsAsync | ComplianceDocuments | OK | `EntityType = N'Driver' AND EntityId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0` |
| DriverRepository.cs | GetTimelineAsync (doc branch) | ComplianceDocuments | OK | `c.EntityType = N'Driver' AND c.EntityId = @DriverId AND c.TenantId = @TenantId AND c.IsDeleted = 0` |
| DriverVerificationExpiryHostedService.cs | ScanComplianceDocExpiryAsync | ComplianceDocuments, Drivers | OK | `d.TenantId = @TenantId AND d.IsDeleted = 0 AND d.EntityType = N'Driver' AND d.ExpiryDate IS NOT NULL AND d.ExpiryDate <= @Threshold` (+ `dr.TenantId = @TenantId`) |
| FleetEvAnomalyRepository.cs | GetDriverVehicleDocumentExpiriesAsync | AssignmentHistory (+ tracker) | OK | `ah.TenantId = @TenantId` then reuses tracker TenantId filter |
| FleetEvAnomalyRepository.cs | GetVehicleDocumentTrackerAsync | ComplianceDocuments, Vehicles | OK | `c.IsDeleted = 0 AND c.TenantId = @TenantId AND c.EntityType = N'Vehicle'`; columns path `v.TenantId = @TenantId` |
| FleetIntelligenceServices.cs (AiRecommendationService) | RefreshAsync — compliance | ComplianceDocuments | OK | `IsDeleted = 0 AND TenantId = @TenantId AND ExpiryDate IS NOT NULL AND ExpiryDate <= DATEADD(DAY, 14, GETUTCDATE())` |
| FleetIntelligenceServices.cs (FleetHealthService) | ComputeAsync — compliance | ComplianceDocuments | OK | `IsDeleted = 0 AND TenantId = @TenantId` |
| FleetRepository.cs | GetComplianceDocumentsAsync | ComplianceDocuments (+ LEFT JOIN Vehicles, Drivers) | OK | `c.IsDeleted = 0 AND c.TenantId = @TenantId` |
| FleetRepository.cs | GetDashboardAsync | Vehicles, Drivers, Maintenance, FuelLogs, ComplianceDocuments | OK | `TenantId = @TenantId` (each subquery; Maint via `v.TenantId`); caller `ITenantContext` |
| GetComplianceDocumentsQueryHandler | Handle | ComplianceDocuments | OK | `tenantContext.GetRequiredTenantId()` only — no request TenantId (contrast GetDriversQuery) |
| MaintenanceAlertHostedService.cs | ScanComplianceAsync (threshold + expired) | ComplianceDocuments, Vehicles | OK | `d.TenantId = @TenantId AND d.IsDeleted = 0` (+ EntityType/expiry windows) |
| MaintenanceModuleRepository.cs | GetMaintenanceComplianceSummaryAsync | ComplianceDocuments | OK | `TenantId = @TenantId AND IsDeleted = 0 AND EntityType = N'Vehicle'` (+ expiry windows) |
| MaintenanceModuleRepository.cs | GetMaintenanceDashboardAsync / request/WO/alert list SELECTs | Vehicles, WorkOrders, MaintenanceRequests, Maintenance, FuelLogs, ComplianceDocuments | OK | consistently `TenantId = @TenantId` / `v.TenantId = @TenantId`; caller `GetRequiredTenantId()` |
| SmartOpsRuleScanner.cs | ScanDocumentExpiryAsync | ComplianceDocuments, Vehicles, Drivers | OK | `d.TenantId = @TenantId AND d.IsDeleted = 0 AND d.ExpiryDate IS NOT NULL AND d.ExpiryDate <= DATEADD(DAY, @LeadDays, @AsOf)` |
| SmartOpsRuleScanner.cs | ScanDocumentExpiryAsync (insurance / licenses) | Vehicles, Drivers, ComplianceDocuments EXISTS | OK | `v/d.TenantId = @TenantId`; EXISTS also `d.TenantId = @TenantId` |
| AssignmentRepository.cs | ListAsync / GetStatsAsync / GetCalendarAsync / GetUtilizationReportAsync / GetChangelogAsync / ValidateAsync SELECTs | AssignmentHistory, Vehicles, Drivers | OK | `v.TenantId = @TenantId` or `TenantId = @TenantId` |
| ComplianceReminderHostedService.cs | ScanAsync — expiringDrivers | Drivers | OK | `IsDeleted = 0 AND IsActive = 1 AND LicenseExpiryDate <= @Threshold AND TenantId IS NOT NULL AND TenantId > 0`; notifies `d.TenantId` |
| ComplianceReminderHostedService.cs | ScanAsync — expiringVehicles | Vehicles | OK | `InsuranceExpiryDate <= @Threshold AND TenantId IS NOT NULL AND TenantId > 0` |
| ComplianceReminderHostedService.cs | ScanAsync — maintenanceDue | Maintenance, Vehicles | OK | `m.NextDueDate <= @Threshold`; notify uses `v.TenantId` |
| DriverAppRepository.cs | GetFallbackVehiclesForInspectionAsync | Vehicles | OK | `TenantId = @TenantId AND IsDeleted = 0 AND Status IN (1, 2)` |
| DriverAppRepository.cs | GetFuelReceiptsAsync / SumFuelCostAsync | FuelLogs | OK | `f.TenantId = @TenantId` (+ DriverId) |
| DriverAppRepository.cs | GetVehiclesForInspectionAsync / GetPrimaryAssignedVehicleAsync | Vehicles, AssignmentHistory | OK | union uses `TenantId = @TenantId`; join `v.Id = x.VehicleId AND v.IsDeleted = 0` |
| DriverVerificationExpiryHostedService.cs | ScanAsync → GetDriversForVerificationExpiryScanAsync | Drivers | OK | `TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1` |
| FleetEvAnomalyRepository.cs | GetDriverEvStatusAsync | AssignmentHistory, Vehicles | OK | `ah.TenantId = @TenantId` (+ Active) |
| FleetEvAnomalyRepository.cs | GetEvDashboardAsync / ListChargingStationsAsync / ListAnomaliesAsync | Vehicles, ChargingStations, FleetAnomalyEvents | OK | `TenantId = @TenantId` |
| FleetIntelligenceServices.cs (AiDigestService) | GenerateMorningDigestAsync — offline | Vehicles | OK | `v.TenantId = @TenantId` |
| FleetIntelligenceServices.cs (AiRecommendationService) | GetActiveAsync | AiRecommendations | OK | `TenantId = @TenantId AND IsDismissed = 0` |
| FleetIntelligenceServices.cs (FleetHealthService) | ComputeAsync — gpsOnline | Vehicles | OK | `v.IsDeleted = 0 AND v.TenantId = @TenantId` |
| FleetRepository.cs | GetAssignmentsAsync | AssignmentHistory, Vehicles, Drivers | OK | `a.IsDeleted = 0 AND v.TenantId = @TenantId` |
| FleetRepository.cs | GetInspectionsAsync | Inspections, Vehicles | OK | `i.IsDeleted = 0 AND v.TenantId = @TenantId` |
| FuelLogRepository.cs | GetPagedAsync / GetByIdAsync / GetEfficiencyReportAsync / GetVehicleTelemetryAsync | FuelLogs, Vehicles, Drivers | OK | `f.TenantId = @TenantId`; joins also `v/d.TenantId = f.TenantId` |
| GetDriversQuery.cs / GetDriverStatsQuery.cs (callers) | Handle → GetPagedAsync / GetStatsAsync | Drivers | OK | repo `d.TenantId = @TenantId`; tenant from `request.TenantId` via `EnsureTenantAccess` / else `platformScope.TenantId` |
| GetVehiclesQuery.cs / VehicleTabQueries / GetVehicleByIdQuery (callers) | Handle | Vehicles | OK | `platformScope.ResolveTenantId(request.TenantId)` → `EnsureTenantAccess` |
| GpsDeviceRepository.cs | GetGpsDevicesAsync / GetTrackerByIdAsync / CountTraccarLinkedAsync | GpsDevices, Vehicles | OK | `TrackerTenantSql.DeviceScopeFilter`: `d.TenantId = @TenantId OR (d.TenantId IS NULL AND v.TenantId = @TenantId)`; tenant from `ITenantContext` |
| GpsDeviceRepository.cs | GetTrackersAsync | GpsDevices | OK | same DeviceScopeFilter; tenant via `ResolveTenantId(request.TenantId)` |
| MaintenanceRepository.cs | GetPagedAsync / GetByIdAsync | Maintenance | OK | `IsDeleted = 0 AND TenantId = @TenantId` (+ Id) |
| PlatformRepository.Tenants.cs | GetTenantManagementStatsGlobalAsync | Vehicles (+ Users) | OK | `FROM Vehicles WHERE IsDeleted = 0` (global aggregate) |
| BookingRepository.cs | CreateAsync / UpdateAsync (FK checks) | Customers, Routes, Vehicles, Drivers | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| BookingRepository.cs | GetByIdAsync | Bookings, Customers, Routes, Vehicles, Drivers | OK | `b.Id = @Id AND b.IsDeleted = 0 AND b.TenantId = @TenantId` (ITenantContext) |
| BookingRepository.cs | GetPagedAsync | Bookings, Customers, Routes, Vehicles, Drivers | OK | `b.IsDeleted = 0 AND b.TenantId = @TenantId` (+ filters) (ITenantContext) |
| BookingRepository.cs | UpdateAsync / SoftDelete* / Assign* / Unassign* / UpdateStatusAsync | Bookings (+ Drivers/Vehicles) | OK | `... TenantId = @TenantId ...` (ITenantContext) |
| CompanyRepository.cs | GetFeatureCatalogAsync | FeatureDefinitions | OK | catalog (no tenant filter) |
| CompanyRepository.cs | GetHierarchyCountsAsync | Branches, Departments, Users, Drivers, Vehicles | OK | subquery `TenantId = @TenantId`; caller ITenantContext |
| CompanyRepository.cs | GetTenantAsync | Tenants | OK | `t.Id = @TenantId`; caller ITenantContext |
| CompanyRepository.cs | GetTenantModulesAsync | TenantModules, Modules | OK | `tm.TenantId = @TenantId`; caller ITenantContext |
| CompanyRepository.cs | GetUserOrgProfileAsync / GetUserOrgFallbackAsync | Users, Branches, Departments, UserRoles, Roles | OK | `u.Id = @UserId AND u.TenantId = @TenantId AND u.IsDeleted = 0`; caller ITenantContext |
| CompanyRepository.cs | MapDataScopeAsync | Branches, Departments | OK | `TenantId = @TenantId AND Id IN @Ids`; from resolved scope |
| CustomerInvoiceRepository.cs | AllocateInvoiceNumberAsync | PlatformSettings, CustomerInvoices | OK | `TenantId = @TenantId ...`; caller ITenantContext |
| CustomerInvoiceRepository.cs | ExistsAsync | CustomerInvoices | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| CustomerInvoiceRepository.cs | GetBillingSummaryAsync | CustomerInvoices | OK | `i.IsDeleted = 0 AND i.TenantId = @TenantId AND i.Status <> N'Void'`; caller ITenantContext |
| CustomerInvoiceRepository.cs | GetBookingSnapshotAsync | Bookings, Customers, Routes | OK | `b.Id = @BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0`; caller ITenantContext |
| CustomerInvoiceRepository.cs | GetByIdAsync | CustomerInvoices | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| CustomerInvoiceRepository.cs | GetPagedAsync | CustomerInvoices, Trips, Bookings, Vehicles, Drivers | OK | `i.IsDeleted = 0 AND i.TenantId = @TenantId` (+ filters); caller ITenantContext |
| CustomerInvoiceRepository.cs | GetTotalPaidForBookingAsync | Payments | OK | `BookingId = @BookingId AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| CustomerInvoiceRepository.cs | GetTripSnapshotAsync | Trips, Customers, Bookings, Routes | OK | `t.Id = @TripId AND t.TenantId = @TenantId AND t.IsDeleted = 0`; caller ITenantContext |
| CustomerInvoiceRepository.cs | HasActiveInvoiceForBookingAsync | CustomerInvoices | OK | `BookingId = @BookingId AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| CustomerInvoiceRepository.cs | HasActiveInvoiceForTripAsync | CustomerInvoices | OK | `TripId = @TripId AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| CustomerRepository.cs | CreateAsync / UpdateAsync / SoftDeleteAsync (EXISTS/CNIC) | Customers | OK | `... TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| CustomerRepository.cs | GetByIdAsync | Customers | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| CustomerRepository.cs | GetListStatsAsync | Customers | OK | `IsDeleted = 0 AND TenantId = @TenantId` (+ filters) (ITenantContext) |
| CustomerRepository.cs | GetPagedAsync | Customers | OK | `IsDeleted = 0 AND TenantId = @TenantId` (+ search/active/recency) (ITenantContext) |
| DashboardRepository.cs | GetSummaryAsync | Vehicles, Bookings, Payments, FuelLogs, Maintenance | OK | each subquery `TenantId = @TenantId`; caller ITenantContext |
| DriverAllowanceRepository.cs | GetActiveRulesAsync | DriverAllowanceRules | OK | `IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId`; caller ITenantContext |
| DriverAllowanceRepository.cs | GetByIdAsync / ExistsAsync | DriverAllowanceRules | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| DriverAllowanceRepository.cs | GetPagedAsync | DriverAllowanceRules | OK | `IsDeleted = 0 AND TenantId = @TenantId` (+ filter); caller ITenantContext |
| DriverAllowanceRepository.cs | GetRouteContextAsync | Routes | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| DriverAllowanceRepository.cs | GetVehicleFuelTypeAsync | Vehicles | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| DriverSettlementRepository.cs | BuildLinesAsync | Payments, Bookings, FuelLogs | OK | `b.TenantId = @TenantId AND p.TenantId = @TenantId` / `DriverId... TenantId=@TenantId`; caller ITenantContext |
| DriverSettlementRepository.cs | CalculateTotalsAsync | Payments, Bookings, FuelLogs, DriverAllowanceRules | OK | all branches include `TenantId = @TenantId`; caller ITenantContext |
| DriverSettlementRepository.cs | DriverExistsAsync | Drivers | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| DriverSettlementRepository.cs | GetByIdAsync / GetHeaderAsync | DriverSettlements, DriverSettlementLines, Drivers | OK | `s.Id = @Id AND s.TenantId = @TenantId AND s.IsDeleted = 0` / lines `SettlementId=@Id AND TenantId=@TenantId`; caller ITenantContext |
| DriverSettlementRepository.cs | GetPagedAsync | DriverSettlements, Drivers | OK | `s.TenantId = @TenantId AND s.IsDeleted = 0`; caller ITenantContext |
| NotificationRepository.cs | GetPagedAsync | Notifications, NotificationRecipients | OK | mailbox predicates `AND n.TenantId = @TenantId`; tenant from `IPlatformScope.TenantId` (= ITenantContext) |
| NotificationRepository.cs | GetRetentionSettingsAsync | PlatformSettings | OK | `TenantId = @TenantId AND Category = @Category AND IsActive = 1`; via PlatformScope/ITenantContext |
| NotificationRepository.cs | GetStatsAsync | Notifications, NotificationRecipients, NotificationDeliveryLogs | OK | `... AND n.TenantId = @TenantId`; via PlatformScope/ITenantContext |
| NotificationRepository.cs | GetTemplatesAsync | NotificationTemplates | OK | `IsDeleted = 0 AND TenantId = @TenantId`; caller ITenantContext |
| NotificationRepository.cs | GetUnreadCountAsync | Notifications, NotificationRecipients | OK | `... AND n.TenantId = @TenantId`; via PlatformScope/ITenantContext |
| NotificationRepository.cs | SearchRecipientsAsync | Users | OK | `IsDeleted = 0 AND TenantId = @TenantId` (+ search); via PlatformScope/ITenantContext |
| NotificationRepository.cs | UserCanAccessNotificationAsync | Notifications, NotificationRecipients | OK | `n.Id = @NotificationId AND n.TenantId = @TenantId AND (...)`; via PlatformScope/ITenantContext |
| PaymentRepository.cs | ExistsAsync | Payments | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| PaymentRepository.cs | GetBookingForPaymentAsync | Bookings | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| PaymentRepository.cs | GetByBookingIdAsync | Payments | OK | `BookingId = @BookingId AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| PaymentRepository.cs | GetByIdAsync | Payments, Bookings, Customers | OK | `p.Id = @Id AND p.TenantId = @TenantId AND p.IsDeleted = 0`; caller ITenantContext |
| PaymentRepository.cs | GetPagedAsync | Payments, Bookings, Customers | OK | `p.IsDeleted = 0 AND p.TenantId = @TenantId` (+ filters); caller ITenantContext |
| PaymentRepository.cs | GetReportAsync | Payments | OK | `TenantId = @TenantId AND ... PaymentDate range ...`; caller ITenantContext |
| PaymentRepository.cs | GetTotalPaidForBookingAsync | Payments | OK | `BookingId = @BookingId AND TenantId = @TenantId ...`; caller ITenantContext |
| PlatformRepository.Organization.cs | EnsureBranchExistsAsync | Branches | OK | `Id = @Id AND TenantId = @TenantId`; caller ITenantContext |
| PlatformRepository.Organization.cs | EnsureDepartmentHeadExistsAsync | Users | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| PlatformRepository.Organization.cs | GetBranchByIdAsync | Branches | OK | `b.Id = @Id AND b.TenantId = @TenantId`; caller ITenantContext |
| PlatformRepository.Organization.cs | GetBranchesAsync | Branches | OK | `b.TenantId = @TenantId`; caller ITenantContext |
| PlatformRepository.Organization.cs | GetDepartmentsAsync | Departments, Users | OK | `d.TenantId = @TenantId`; caller ITenantContext |
| PlatformRepository.Organization.cs | GetOrganizationBranchesAsync | Branches | OK | `TenantId = @TenantId`; caller ITenantContext |
| PlatformRepository.Organization.cs | GetOrganizationDepartmentsAsync | Departments, Users | OK | `d.TenantId = @TenantId`; caller ITenantContext |
| PlatformRepository.Tenants.cs | tenant list/detail branch/dept counts | Tenants, Branches, Departments, Users | OK | `b.TenantId = t.Id` / platform tenant catalog (no ITenantContext single-tenant filter) |
| RentalRepository.cs | CustomerExistsAsync / VehicleExistsAsync / GetCustomerNameAsync / GetVehicleSnapshotAsync | Customers, Vehicles | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| RentalRepository.cs | GetBookingsPagedAsync / GetBookingByIdAsync | RentalBookings, Customers, Vehicles | OK | `b.TenantId = @TenantId AND b.IsDeleted = 0`; caller ITenantContext |
| RentalRepository.cs | GetPaymentsAsync / UpdateBookingPaymentTotalsAsync (paid sum) | RentalPayments, RentalBookings | OK | `TenantId = @TenantId` / `b.Id=@Id AND b.TenantId=@TenantId`; caller ITenantContext |
| RentalRepository.cs | GetRatesPagedAsync / GetRateByIdAsync / FindBestRateAsync | RentalRates, Vehicles | OK | `r.TenantId = @TenantId AND r.IsDeleted = 0` (+ filters); caller ITenantContext |
| RentalRepository.cs | HasOverlappingBookingAsync / CheckAvailabilityAsync | RentalBookings, Customers | OK | `b.TenantId = @TenantId AND b.IsDeleted = 0` (+ overlap window); caller ITenantContext |
| RentalRepository.cs | NextRentalNumberAsync | RentalBookings | OK | `TenantId = @TenantId`; caller ITenantContext |
| ReportRepository.cs | GetActiveAssignmentVehicleNamesAsync | AssignmentHistory, Vehicles | OK | `ah.TenantId = @TenantId AND ah.IsDeleted = 0 AND ah.Status = N'Active'`; caller ITenantContext |
| ReportRepository.cs | GetActiveSpeedLimitsByVehicleAsync | GpsAlertRules | OK | `TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 AND SpeedLimitKmh IS NOT NULL`; caller ITenantContext |
| ReportRepository.cs | GetAssignmentWindowsAsync | AssignmentHistory | OK | `TenantId = @TenantId AND IsDeleted = 0 AND DriverId IS NOT NULL ...`; caller ITenantContext |
| ReportRepository.cs | GetBookingReportAsync | Bookings | OK | `CreatedAt >= @From AND CreatedAt < @ToExclusive AND IsDeleted = 0 AND TenantId = @TenantId` (ITenantContext) |
| ReportRepository.cs | GetDriverNamesAsync | Drivers | OK | `TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| ReportRepository.cs | GetDriverPerformanceAsync | Drivers, Bookings | OK | `d.TenantId = @TenantId` + `b.TenantId = @TenantId` (ITenantContext) |
| ReportRepository.cs | GetDriverProfilesAsync | Drivers | OK | scoped by `@TenantId`; caller ITenantContext |
| ReportRepository.cs | GetFuelReportRowsAsync | FuelLogs, Vehicles | OK | scoped by `@TenantId`; caller ITenantContext |
| ReportRepository.cs | GetRevenueReportAsync | Payments, FuelLogs, Maintenance | OK | `TenantId = @TenantId AND ... date range ...` (ITenantContext) |
| ReportRepository.cs | GetVehicleIdsByBranchDepartmentAsync | Vehicles | OK | includes `TenantId = @TenantId`; caller ITenantContext |
| ReportRepository.cs | GetVehicleProfitAsync | Vehicles, Bookings, Payments, FuelLogs, Maintenance | OK | `v.TenantId = @TenantId` + join TenantId filters (ITenantContext) |
| ReportRepository.cs | GetVehicleReportRowsAsync | Vehicles, AssignmentHistory | OK | vehicle query scoped by `@TenantId`; caller ITenantContext |
| ReportRepository.cs | ResolveVehicleIdsForDriverAsync | AssignmentHistory | OK | `TenantId = @TenantId AND IsDeleted = 0 AND DriverId = @DriverId ...`; caller ITenantContext |
| ReportRepository.cs | ResolveVehicleToDeviceMapAsync / HasNonTraccarVehicleAsync | Vehicles, GpsDevices | OK | `v.TenantId = @TenantId AND v.IsDeleted = 0 AND v.Id IN @VehicleIds`; caller ITenantContext |
| RouteRepository.cs | GetByIdAsync | Routes | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| RouteRepository.cs | GetListStatsAsync | Routes | OK | `IsDeleted = 0 AND TenantId = @TenantId` (+ filters); caller ITenantContext |
| RouteRepository.cs | GetPagedAsync | Routes | OK | `IsDeleted = 0 AND TenantId = @TenantId` (+ filters); caller ITenantContext |
| RouteRepository.cs | UpdateAsync / DeleteAsync (EXISTS) | Routes | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0`; caller ITenantContext |
| SmartAlertRepository.cs | CountNewAsync | SmartAlerts | OK | `TenantId = @TenantId AND Status = N'New'`; via PlatformScope/ITenantContext |
| SmartAlertRepository.cs | GetActiveAsync | SmartAlerts | OK | `TenantId = @TenantId` (+ Status/RuleType/Take); via PlatformScope/ITenantContext |
| SmartAlertRepository.cs | GetByIdAsync | SmartAlerts | OK | `Id = @Id AND TenantId = @TenantId`; via PlatformScope/ITenantContext |
| TripRepository.cs | AssignDriverAsync (conflict) | Trips | OK | `DriverId = @DriverId AND IsDeleted = 0 AND TenantId = @TenantId AND Id != @TripId` (ITenantContext) |
| TripRepository.cs | AssignDriverAsync (driver) | Drivers | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1` (ITenantContext) |
| TripRepository.cs | AssignDriverAsync (trip) | Trips | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | AssignVehicleAsync (assignment/conflict) | AssignmentHistory, Trips | OK | `TenantId = @TenantId` on both (ITenantContext) |
| TripRepository.cs | AssignVehicleAsync (trip) | Trips | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | AssignVehicleAsync (vehicle) | Vehicles | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | ClearDriverAssignmentAsync (trip) | Trips | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | CreateAsync (Bookings EXISTS) | Bookings | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | EnsureTripExistsAsync | Trips | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | GetAnalyticsAsync | Trips, TripExpenses, Drivers, Vehicles | OK | `t.IsDeleted = 0 AND t.TenantId = @TenantId AND t.TripDate >= @From AND t.TripDate < @ToExclusive` (ITenantContext) |
| TripRepository.cs | GetCalendarAsync | Trips, Customers, Drivers, Vehicles | OK | `t.IsDeleted = 0 AND t.TenantId = @TenantId AND t.TripDate >= @From AND t.TripDate < @ToExclusive` (ITenantContext) |
| TripRepository.cs | GetCreateFromBookingSeedAsync | Trips, Bookings, Customers, Routes, Vehicles, Drivers | OK | `BookingId/Id + TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | GetDashboardAsync | Trips | OK | `t.IsDeleted = 0 AND t.TenantId = @TenantId` (+ fleet scope) (ITenantContext) |
| TripRepository.cs | GetDetailAsync (header) | Trips, Bookings, Customers, Routes, Drivers, Vehicles, GpsDevices | OK | `t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | GetLiveAsync | Trips | OK | `t.IsDeleted = 0 AND t.TenantId = @TenantId AND t.Status NOT IN (...)` (ITenantContext) |
| TripRepository.cs | GetPagedAsync | Trips, Customers, Bookings, Drivers, Vehicles | OK | `t.IsDeleted = 0 AND t.TenantId = @TenantId` (+ filters/fleet scope) (ITenantContext) |
| TripRepository.cs | GetRouteSummaryAsync (trip) | Trips, Routes | OK | `t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | OptimizeRouteAsync (trip) | Trips | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | SoftDeleteAsync | Trips | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | UpdateAsync | Trips | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| TripRepository.cs | UpdateStatusAsync (trip load) | Trips | OK | `Id = @Id AND TenantId = @TenantId AND IsDeleted = 0` (ITenantContext) |
| VehicleRepository.cs | GetByIdAsync / GetGpsSnapshotAsync / Exists paths | Vehicles, GpsDevices | OK | `v.TenantId = @TenantId` |
| VehicleRepository.cs | GetCrossTenantPagedAsync | Vehicles, Drivers, GpsDevices | OK | `v.IsDeleted = 0 AND v.Status <> 5` [+ optional `v.TenantId = @CompanyId`]; gated admin handler |
| VehicleRepository.cs | GetFuelSummaryAsync | FuelLogs | OK | `IsDeleted = 0 AND VehicleId = @VehicleId AND TenantId = @TenantId` |
| VehicleRepository.cs | GetPagedAsync | Vehicles | OK | `v.IsDeleted = 0 AND v.TenantId = @TenantId` |

## Notes for Pass 2

- Prefer `ITenantContext.GetRequiredTenantId()` everywhere tenant-owned ERP data is read; do not take TenantId from the client.
- Platform cross-tenant reads must stay on explicit platform endpoints with audit (SG-07).
- Child-table reads after a tenant-scoped parent load (TripStops, etc.) should still add TenantId where the column exists, or join back to the parent with TenantId.
- Ali will require **full SQL of every changed query** in Pass 2.

