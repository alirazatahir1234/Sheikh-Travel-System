# SG-01 Pass 1 — tenant isolation sweep

**Branch:** `fix/SG-01-compliance-tenant-isolation`  
**Scope:** investigation only — no repository query fixes in this pass.  
**Prompt:** `prompts/day-4-SG-01-pass1-sweep.md`

## Counts (LEAD)

| Classification | Count |
| --- | ---: |
| **LEAK** | **82** |
| AUTHORIZATION BUG | 5 |
| OK | 255 |
| **Total read paths classified** | **342** |

Pass 2 size: **large (~3–5 days)** — ~28 files / ~82 LEAK methods + 5 AUTH BUG surfaces. Do not start Pass 2 in this commit.

## Notes

- Data access is Dapper SQL; no EF Core `DbSet` / `FromSql*` tenant reads found.
- There is **no `DriverDocuments` table** — driver docs live in `ComplianceDocuments` (`EntityType = N'Driver'`).
- Confirmed compliance hotspot: `FleetRepository.GetComplianceDocumentsAsync` — outer `c.TenantId = @TenantId` is present, but `LEFT JOIN Drivers` / `Vehicles` match on `EntityId` only (no `d.TenantId` / `v.TenantId`). Cross-tenant names can surface when document `EntityId` points at another tenant's driver/vehicle (or when doc rows were written with wrong `TenantId`).
- Hosted jobs that intentionally loop tenants / scan `TenantId IS NOT NULL` are **OK (PLATFORM-JOB)**.
- Auth email/token user lookups are **OK (PLATFORM-AUTH)**.

## Top LEAKs (priority for Pass 2)

| Priority | File | Method | Tables |
| --- | --- | --- | --- |
| 1 | `FleetRepository.cs` | **GetComplianceDocumentsAsync** | ComplianceDocuments, Drivers, Vehicles |
| 1b | `ComplianceReminderHostedService.cs` | ScanAsync CROSS APPLY ComplianceDocuments | ComplianceDocuments (missing `c.TenantId = v.TenantId`) |
| 2 | `FleetRepository.cs` | GetAssignmentsAsync | AssignmentHistory, Drivers, Vehicles |
| 3 | `CustomerPortalRepository.cs` | GetVehiclesAsync / GetRoutesAsync (+ 27 siblings) | Vehicles, Routes, Bookings, Payments, … |
| 4 | `FleetIntelligenceServices.cs` | ComputeAsync / GenerateRecommendationsAsync | Maintenance, GpsAlertEvents, ComplianceDocuments |
| 5 | `UserRepository.cs` | GetByIdAsync / GetOrgInfoAsync / GetPasswordInfoAsync / ExistsAsync | Users (IDOR by id) |
| 6 | `GpsTrackingRepository.cs` | GetGpsEtaAsync / IngestPositionAsync | Bookings, Vehicles, … |
| 7 | `VehicleRepository.cs` | GetMaintenancePagedAsync | Maintenance (by VehicleId only) |
| 8 | `TripRepository.cs` | GetDetailAsync child collections | TripStops / Expenses / Documents / … |
| 9 | `TrackerRegistrationService.cs` + `TraccarSyncOrchestrator.cs` | device CRUD/sync | GpsDevices, GpsAlertEvents |
| 10 | PaymentGateway + GPS ingest helpers | RecordAsync / Ingest* | Bookings, Payments, Vehicles |

## LEAK + AUTHORIZATION BUG table

| File | Method | Table(s) | Classification | WHERE clause |
| --- | --- | --- | --- | --- |
| `Infrastructure/.../CompanyRepository.cs` | GetPasswordChangedAtAsync | Users | LEAK | `WHERE Id = @UserId` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetBookedSeatsNearPickupAsync | BookingSeats, Bookings | LEAK | by VehicleId/time — **no TenantId** |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetBookingCardsAsync | Bookings, Payments, Routes | LEAK | by customer — **no TenantId** |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetBookingDetailHeadAsync | Bookings, Drivers, Payments, Routes, Vehicles | LEAK | by booking Id — **no TenantId** |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetBookingNumberAsync | Bookings | LEAK | `WHERE Id = @Id AND IsDeleted = 0` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetBookingPaymentsAsync | Payments | LEAK | `WHERE BookingId = @BookingId` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetBookingSeatsAsync | BookingSeats | LEAK | `WHERE BookingId = @BookingId` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetBookingStatusPickupAsync | Bookings | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetBookingStatusVehicleAsync | Bookings | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetCheckoutBookingAsync | Bookings, Customers, Payments | LEAK | `WHERE b.Id = @BookingId` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetCustomerNotificationsAsync | CustomerNotifications | LEAK | `WHERE CustomerId = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetFavoriteRoutesAsync | CustomerFavoriteRoutes, Routes | LEAK | `WHERE f.CustomerId = @CustomerId` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetFirstActiveRouteIdAsync | Routes | LEAK | all active routes |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetInvoiceRowAsync | Bookings, Customers, Payments, Routes | LEAK | by booking — **no TenantId** |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetLoyaltyAsync | CustomerLoyalty | LEAK | `WHERE CustomerId = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetPerKmBasePriceAsync | Routes | LEAK | all active routes with Distance |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetRouteBasePriceAsync | Routes | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetRouteLabelAsync | Routes | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetRoutesAsync | Routes | LEAK | all active — **no TenantId** |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetSavedAddressesAsync | CustomerSavedAddresses | LEAK | `WHERE CustomerId = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetStartedBookingDriverPhoneAsync | Bookings, Drivers | LEAK | `WHERE b.Id = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetVehicleLabelAsync | Vehicles | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetVehicleLiveLocationAsync | VehicleCurrentLocation | LEAK | `WHERE VehicleId = @VehicleId` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetVehicleSeatLayoutsAsync | VehicleSeatLayouts | LEAK | `WHERE VehicleId = @VehicleId` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetVehicleSeatingCapacityAsync | Vehicles | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetVehiclesAsync | Vehicles | LEAK | all non-retired — **no TenantId** |
| `Infrastructure/.../CustomerPortalRepository.cs` | GetWalletBalanceAsync | CustomerWallets | LEAK | `WHERE CustomerId = @Id` |
| `Infrastructure/.../CustomerPortalRepository.cs` | IsSeatTakenAsync | BookingSeats, Bookings | LEAK | by VehicleId/seat — **no TenantId** |
| `Infrastructure/.../CustomerPortalRepository.cs` | NormalizeCustomerPhonesAsync | Customers | LEAK | all customers with Phone |
| `Infrastructure/.../CustomerPortalRepository.cs` | VehicleExistsAndNotRetiredAsync | Vehicles | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../DriverAppRepository.cs` | GetDriverFullNameAsync | Drivers | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../DriverAppRepository.cs` | GetVehicleNameAsync | Vehicles | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../FleetRepository.cs` | **GetComplianceDocumentsAsync** | ComplianceDocuments, Vehicles, Drivers | **LEAK** | `c.TenantId=@TenantId` but **JOIN Drivers/Vehicles on EntityId only** |
| `Infrastructure/.../Services/ComplianceReminderHostedService.cs` | ScanAsync driverVehicleDocs | ComplianceDocuments, Vehicles, Drivers | LEAK | CROSS APPLY docs by EntityId only — missing `c.TenantId = v.TenantId` |
| `Infrastructure/.../FleetRepository.cs` | GetAssignmentsAsync | AssignmentHistory, Vehicles, Drivers | LEAK | `v.TenantId=@TenantId`; Drivers join unscoped |
| `Infrastructure/.../GpsDeviceRepository.cs` | GetPendingDeviceCommandsAsync | GpsDeviceCommands, GpsDevices | LEAK | by UniqueId — no TenantId |
| `Infrastructure/.../GpsTrackingRepository.cs` | GetGpsEtaAsync | Bookings, Routes, Trips, VehicleCurrentLocation, Vehicles | LEAK | by booking/vehicle Id — no TenantId |
| `Infrastructure/.../GpsTrackingRepository.cs` | IngestPositionAsync | Bookings, VehicleCurrentLocation | LEAK | by DriverId/VehicleId — no TenantId |
| `Infrastructure/.../MaintenanceModuleRepository.cs` | CreateMaintenanceScheduleAsync (existence read) | Vehicles | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../PlatformRepository.Access.cs` | GetUserPasswordChangedAtAsync | Users | LEAK | `WHERE Id = @Id` |
| `Infrastructure/.../TripRepository.cs` | GetDetailAsync (child collections) | TripStops, TripStatusHistory, TripExpenses, TripDocuments, TripPassengers | LEAK | child rows by `TripId=@Id` only |
| `Infrastructure/.../TripRepository.cs` | OptimizeRouteAsync | Routes | LEAK | `WHERE Id = @Id` — no TenantId |
| `Infrastructure/.../UserRepository.cs` | ExistsAsync / GetByIdAsync / GetOrgInfoAsync / GetPasswordInfoAsync | Users | LEAK | by UserId — **no TenantId** (IDOR) |
| `Infrastructure/.../VehicleRepository.cs` | GetMaintenancePagedAsync | Maintenance | LEAK | by VehicleId only |
| `Infrastructure/.../Services/Ai/FleetIntelligenceServices.cs` | ComputeAsync | Maintenance, GpsAlertEvents, ComplianceDocuments, Vehicles | LEAK | Maintenance + GpsAlertEvents **global** |
| `Infrastructure/.../Services/Ai/FleetIntelligenceServices.cs` | GenerateRecommendationsAsync | Maintenance, ComplianceDocuments | LEAK | Maintenance overdue **no TenantId** |
| `Infrastructure/.../Services/Ai/PredictionCopilotManagement.cs` | CaptureFeaturesAsync | Maintenance | LEAK | by VehicleId |
| `Infrastructure/.../Services/Drivers/DriverTelematicsEnforcementService.cs` | ProcessAlertAsync | Vehicles | LEAK | by VehicleId |
| `Infrastructure/.../Services/Gps/FleetAnomalyEvaluator.cs` | EvaluateAsync | Vehicles | LEAK | by VehicleId |
| `Infrastructure/.../Services/Gps/FuelStationStopEvaluator.cs` | EvaluateAsync | Vehicles | LEAK | by Id |
| `Infrastructure/.../Services/Gps/GpsAlertWriter.cs` | InsertAsync (vehicle lookup) | Vehicles | LEAK | by Id |
| `Infrastructure/.../Services/Gps/GpsCommandSafetyChecker.cs` | CheckEngineCutoffPreconditionAsync | VehicleCurrentLocation | LEAK | by VehicleId |
| `Infrastructure/.../Services/Gps/GpsHarshBrakingAlertDispatch.cs` | DispatchIfAllowedAsync | Vehicles | LEAK | by Id |
| `Infrastructure/.../Services/Gps/GpsPositionIngestionHelper.cs` | IngestAsync / ResolveActiveBookingIdAsync | Bookings, Vehicles | LEAK | by Id / VehicleId |
| `Infrastructure/.../Services/Gps/GpsTripPersistenceService.cs` | position/trip loads | GpsPositions, GpsTrips, Vehicles | LEAK | by VehicleId |
| `Infrastructure/.../Services/Gps/RoadSpeedAlertEvaluator.cs` | EvaluateAsync | Vehicles | LEAK | by Id |
| `Infrastructure/.../Services/GpsControl/GpsCommandTranslator.cs` | TranslateAsync | GpsDevices | LEAK | by Id |
| `Infrastructure/.../Services/LocationBroadcastService.cs` | ResolveTenantIdAsync path | Vehicles | LEAK | by VehicleId |
| `Infrastructure/.../Services/Payments/PaymentGatewayPaymentRecorder.cs` | RecordAsync | Bookings, Payments | LEAK | by BookingId / TransactionReference |
| `Infrastructure/.../Traccar/TraccarSyncOrchestrator.cs` | SyncDevicesAsync / SyncEventsAsync / SyncTrackerAsync | GpsDevices, GpsAlertEvents | LEAK | by UniqueId / ExternalEventId / Id |
| `Infrastructure/.../Traccar/TrackerRegistrationService.cs` | Register/Update/Delete/Install/Transfer/Uninstall | GpsDevices | LEAK | by Id / UniqueId |
| `Application/.../Platform/PlatformHandlers.cs` | org/role/module/subscription handlers | Branches, Departments, Users, Roles, TenantModules | AUTHORIZATION BUG | `request.TenantId` + `EnsureTenantAccess` |
| `Application/.../Users/Commands/CreateUserCommand.cs` | ResolveTargetTenantId | Users | AUTHORIZATION BUG | `CompanyId` from body |
| `Application/.../Users/Commands/UpdateUserCommand.cs` | ResolveTargetTenantId | Users | AUTHORIZATION BUG | `CompanyId` from body |
| `Application/.../Users/Queries/GetUsersQuery.cs` | ResolveTenantFilter | Users | AUTHORIZATION BUG | `request.TenantId` from query |
| `Infrastructure/.../VehicleRepository.cs` | GetCrossTenantPagedAsync | Vehicles | AUTHORIZATION BUG | optional CompanyId; can list all tenants |

## OK summary (255 methods / ~47 files)

TenantId from `ITenantContext` (or intentional platform-auth / platform-job). Largest OK concentrations:

| File | OK method count (approx) |
| --- | ---: |
| DriverAppRepository.cs | 41 |
| DriverRepository.cs | 25 |
| MaintenanceModuleRepository.cs | 17 |
| VehicleRepository.cs | 14 |
| GpsTrackingRepository.cs | 12 |
| TripRepository.cs | 11 |
| BookingRepository.cs | 10 |
| AuthRepository.cs | 8 |
| PaymentRepository.cs | 8 |
| AssignmentRepository.cs | 7 |
| CustomerRepository.cs | 7 |
| DriverAllowanceRepository.cs | 7 |
| ReportRepository.cs | 7 |
| RouteRepository.cs | 6 |
| UserRepository.cs (paged list SQL) | 6 |
| (+ ~32 more files, 1–4 OK each) | … |

Compliance-related OK examples: `DriverRepository.GetDocumentsAsync`, `DriverAppRepository.GetDriverComplianceDocsAsync` / `GetVehicleComplianceDocsAsync`, `FleetRepository.GetDashboardAsync` / `GetInspectionsAsync`, SmartOps/MaintenanceAlert/DriverVerification scanners (per-tenant loops).

## Pass 2 size estimate

| Metric | Estimate |
| --- | --- |
| Files to touch | **~27** LEAK files + **~5** AUTH BUG surfaces (+ isolation tests) |
| Methods to fix | **~82 LEAK** + **5 AUTH BUG** |
| Effort | **Large — ~3–5 days** |
| Suggested order | (1) FleetRepository compliance/assignment joins, (2) CustomerPortal tenant scoping, (3) FleetIntelligence Maintenance/GpsAlertEvents, (4) User IDOR reads, (5) GPS/Traccar/ingest Id-only reads, (6) tighten Platform/Users request-TenantId paths |

**Do not start Pass 2 until this sweep is reviewed.**
