using System.Globalization;
using System.Text.Json;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class TripEnhancementsRepository(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    ITripRepository tripRepository,
    IPricingRepository pricingRepository) : ITripEnhancementsRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<int> CloneTripAsync(int tripId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var source = await connection.QuerySingleOrDefaultAsync<CloneSourceRow>(new CommandDefinition("""
            SELECT TripName, TripType, BookingId, CustomerId, RouteId, PassengerCount, Priority,
                   PickupAddress, PickupLatitude, PickupLongitude,
                   DestinationAddress, DestinationLatitude, DestinationLongitude,
                   TripDate, PlannedStart, PlannedEnd, EstimatedDurationMinutes,
                   PlannedDistanceKm, DriverNotes, SpecialCargoFlags, EstimatedRevenue
            FROM Trips
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = tripId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (source is null)
            throw new NotFoundException("Trip", tripId);

        var stops = (await connection.QueryAsync<TripStopInputDto>(new CommandDefinition("""
            SELECT Sequence, Location, Latitude, Longitude, Eta
            FROM TripStops WHERE TripId = @Id AND IsDeleted = 0 ORDER BY Sequence
            """, new { Id = tripId }, cancellationToken: cancellationToken))).ToList();

        var dto = new CreateTripDto(
            $"{source.TripName} (copy)",
            (TripType)source.TripType,
            null,
            source.CustomerId,
            source.RouteId,
            source.PassengerCount,
            (TripPriority)source.Priority,
            source.PickupAddress,
            source.PickupLatitude,
            source.PickupLongitude,
            source.DestinationAddress,
            source.DestinationLatitude,
            source.DestinationLongitude,
            source.TripDate,
            source.PlannedStart,
            source.PlannedEnd,
            source.EstimatedDurationMinutes,
            source.PlannedDistanceKm,
            source.DriverNotes,
            null,
            null,
            null,
            stops,
            source.SpecialCargoFlags,
            source.EstimatedRevenue);

        var newId = await tripRepository.CreateAsync(dto, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Trips SET ClonedFromTripId = @SourceId WHERE Id = @Id AND TenantId = @TenantId",
            new { SourceId = tripId, Id = newId, TenantId = tenantId },
            cancellationToken: cancellationToken));
        await RecordTimelineEventAsync(newId, "Cloned", "Trip cloned", $"Cloned from trip #{tripId}", cancellationToken);
        return newId;
    }

    public async Task<BulkTripImportResult> ImportFromRowsAsync(
        IReadOnlyList<BulkTripImportRowDto> rows,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var created = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            try
            {
                if (!Enum.TryParse<TripType>(row.TripType, true, out var tripType))
                    tripType = TripType.Transfer;
                if (!Enum.TryParse<TripPriority>(row.Priority, true, out var priority))
                    priority = TripPriority.Normal;

                var dto = new CreateTripDto(
                    row.TripName,
                    tripType,
                    null,
                    row.CustomerId,
                    null,
                    1,
                    priority,
                    row.PickupAddress,
                    null,
                    null,
                    row.DestinationAddress,
                    null,
                    null,
                    row.TripDate.Date,
                    row.PlannedStart,
                    null,
                    null,
                    null,
                    null,
                    row.DriverId,
                    null,
                    row.VehicleId,
                    null,
                    row.SpecialCargoFlags,
                    null);

                await tripRepository.CreateAsync(dto, cancellationToken);
                created++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row {i + 1}: {ex.Message}");
            }
        }

        return new BulkTripImportResult(created, rows.Count - created, errors);
    }

    public async Task<IReadOnlyList<TripTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var items = await connection.QueryAsync<TripTemplateDto>(new CommandDefinition("""
            SELECT Id, Name, RecurrenceKind, RecurrenceInterval, RecurrenceDaysOfWeek,
                   NextOccurrenceDate, IsActive, CreatedAt
            FROM TripTemplates
            WHERE TenantId = @TenantId AND IsDeleted = 0
            ORDER BY Name
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return items.ToList();
    }

    public async Task<int> SaveTemplateAsync(UpsertTripTemplateDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString();
        var payload = JsonSerializer.Serialize(dto.Payload, JsonOpts);

        if (dto.Id is int id && id > 0)
        {
            var updated = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE TripTemplates SET
                    Name = @Name, PayloadJson = @PayloadJson,
                    RecurrenceKind = @RecurrenceKind, RecurrenceInterval = @RecurrenceInterval,
                    RecurrenceDaysOfWeek = @RecurrenceDaysOfWeek, NextOccurrenceDate = @NextOccurrenceDate,
                    IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
                """,
                new
                {
                    Id = id,
                    TenantId = tenantId,
                    dto.Name,
                    PayloadJson = payload,
                    dto.RecurrenceKind,
                    dto.RecurrenceInterval,
                    dto.RecurrenceDaysOfWeek,
                    dto.NextOccurrenceDate,
                    dto.IsActive
                },
                cancellationToken: cancellationToken));
            if (updated == 0)
                throw new NotFoundException("TripTemplate", id);
            return id;
        }

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TripTemplates (
                TenantId, Name, PayloadJson, RecurrenceKind, RecurrenceInterval,
                RecurrenceDaysOfWeek, NextOccurrenceDate, IsActive, CreatedBy)
            VALUES (
                @TenantId, @Name, @PayloadJson, @RecurrenceKind, @RecurrenceInterval,
                @RecurrenceDaysOfWeek, @NextOccurrenceDate, @IsActive, @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                TenantId = tenantId,
                dto.Name,
                PayloadJson = payload,
                dto.RecurrenceKind,
                dto.RecurrenceInterval,
                dto.RecurrenceDaysOfWeek,
                dto.NextOccurrenceDate,
                dto.IsActive,
                CreatedBy = actor
            },
            cancellationToken: cancellationToken));
    }

    public async Task SoftDeleteTemplateAsync(int templateId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TripTemplates SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND TenantId = @TenantId",
            new { Id = templateId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> SpawnFromTemplateAsync(int templateId, DateTime? occurrenceDate, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await connection.QuerySingleOrDefaultAsync<(string PayloadJson, string RecurrenceKind, int RecurrenceInterval, DateTime? NextOccurrenceDate)>(
            new CommandDefinition("""
                SELECT PayloadJson, RecurrenceKind, RecurrenceInterval, NextOccurrenceDate
                FROM TripTemplates WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
                """, new { Id = templateId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (row.PayloadJson is null)
            throw new NotFoundException("TripTemplate", templateId);

        var payload = JsonSerializer.Deserialize<CreateTripDto>(row.PayloadJson, JsonOpts)
                      ?? throw new InvalidOperationException("Template payload is invalid.");

        var tripDate = (occurrenceDate ?? row.NextOccurrenceDate ?? DateTime.UtcNow.Date).Date;
        var plannedStart = payload.PlannedStart;
        if (occurrenceDate.HasValue)
        {
            plannedStart = new DateTime(
                tripDate.Year, tripDate.Month, tripDate.Day,
                payload.PlannedStart.Hour, payload.PlannedStart.Minute, payload.PlannedStart.Second,
                DateTimeKind.Unspecified);
        }

        var spawn = payload with { TripDate = tripDate, PlannedStart = plannedStart, BookingId = null, DriverId = null, VehicleId = null };
        var tripId = await tripRepository.CreateAsync(spawn, cancellationToken);

        if (!string.Equals(row.RecurrenceKind, "None", StringComparison.OrdinalIgnoreCase))
        {
            var next = ComputeNextOccurrence(tripDate, row.RecurrenceKind, row.RecurrenceInterval);
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE TripTemplates SET NextOccurrenceDate = @Next WHERE Id = @Id AND TenantId = @TenantId",
                new { Next = next, Id = templateId, TenantId = tenantId },
                cancellationToken: cancellationToken));
        }

        await RecordTimelineEventAsync(tripId, "Template", "Created from template", $"Template #{templateId}", cancellationToken);
        return tripId;
    }

    public async Task<int> SpawnDueRecurringTemplatesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var due = (await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT Id FROM TripTemplates
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
              AND RecurrenceKind <> N'None'
              AND NextOccurrenceDate IS NOT NULL AND NextOccurrenceDate <= CAST(SYSUTCDATETIME() AS DATE)
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

        var count = 0;
        foreach (var templateId in due)
        {
            await SpawnFromTemplateAsync(templateId, null, cancellationToken);
            count++;
        }

        return count;
    }

    public async Task<TripInsightsDto> GetInsightsAsync(int tripId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var trip = await connection.QuerySingleOrDefaultAsync<TripInsightRow>(new CommandDefinition("""
            SELECT t.Id, t.Status, t.DriverId, t.VehicleId, t.PlannedStart,
                   t.PlannedDistanceKm, t.ActualDistanceKm, t.EstimatedRevenue,
                   (SELECT TOP 1 ci.Status FROM CustomerInvoices ci
                    WHERE ci.TenantId = t.TenantId AND ci.TripId = t.Id AND ci.IsDeleted = 0
                    ORDER BY ci.Id DESC) AS InvoiceStatus,
                   (SELECT TOP 1 ci.TotalAmount FROM CustomerInvoices ci
                    WHERE ci.TenantId = t.TenantId AND ci.TripId = t.Id AND ci.IsDeleted = 0
                    ORDER BY ci.Id DESC) AS InvoiceAmount
            FROM Trips t
            WHERE t.Id = @Id AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """, new { Id = tripId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (trip is null)
            throw new NotFoundException("Trip", tripId);

        var expenseTotal = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT COALESCE(SUM(Amount), 0) FROM TripExpenses WHERE TripId = @Id AND IsDeleted = 0
            """, new { Id = tripId }, cancellationToken: cancellationToken));

        var revenue = trip.EstimatedRevenue;
        var margin = revenue.HasValue ? revenue.Value - expenseTotal : (decimal?)null;
        var marginPct = revenue is > 0 && margin.HasValue ? Math.Round(margin.Value / revenue.Value * 100, 1) : (decimal?)null;

        var deviation = BuildDeviation(trip.PlannedDistanceKm, trip.ActualDistanceKm);

        var feedback = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM TripRatings WHERE TenantId = @TenantId AND TripId = @TripId",
            new { TenantId = tenantId, TripId = tripId }, cancellationToken: cancellationToken)) > 0
            ? "Received"
            : await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM TripFeedbackLinks WHERE TripId = @TripId AND UsedAt IS NULL",
                new { TripId = tripId }, cancellationToken: cancellationToken)) > 0
                ? "Pending"
                : trip.Status == (int)TripStatus.Completed ? "Not sent" : "—";

        var rejects = (await connection.QueryAsync<TripDriverRejectDto>(new CommandDefinition("""
            SELECT d.DriverId, dr.FullName AS DriverName, d.RejectionReason,
                   d.CreatedAtUtc AS RejectedAtUtc
            FROM TripSmartAssignmentDecisions d
            LEFT JOIN Drivers dr ON dr.Id = d.DriverId
            WHERE d.TenantId = @TenantId AND d.TripId = @TripId
              AND d.Outcome = N'Rejected'
            ORDER BY d.CreatedAtUtc DESC
            """, new { TenantId = tenantId, TripId = tripId }, cancellationToken: cancellationToken))).ToList();

        var statusEvents = (await connection.QueryAsync<TripLiveEventDto>(new CommandDefinition("""
            SELECT N'Status' AS EventType,
                   CONCAT(N'Status → ', CAST(ToStatus AS NVARCHAR(10))) AS Title,
                   Note AS Detail, ChangedAtUtc AS OccurredAtUtc, N'system' AS Source
            FROM TripStatusHistory WHERE TripId = @TripId
            """, new { TripId = tripId }, cancellationToken: cancellationToken))).ToList();

        var customEvents = (await connection.QueryAsync<TripLiveEventDto>(new CommandDefinition("""
            SELECT EventType, Title, Detail, OccurredAtUtc, N'trip' AS Source
            FROM TripTimelineEvents WHERE TenantId = @TenantId AND TripId = @TripId
            """, new { TenantId = tenantId, TripId = tripId }, cancellationToken: cancellationToken))).ToList();

        var stopEvents = (await connection.QueryAsync<TripLiveEventDto>(new CommandDefinition("""
            SELECT CASE WHEN ArrivalTime IS NOT NULL AND DepartureTime IS NULL THEN N'GeofenceArrive'
                        WHEN DepartureTime IS NOT NULL THEN N'GeofenceDepart'
                        ELSE N'Stop' END AS EventType,
                   CONCAT(N'Stop ', Sequence, N': ', Location) AS Title,
                   CASE WHEN ArrivalTime IS NOT NULL THEN CONCAT(N'Arrived ', FORMAT(ArrivalTime, N'HH:mm'))
                        ELSE NULL END AS Detail,
                   COALESCE(ArrivalTime, DepartureTime, SYSUTCDATETIME()) AS OccurredAtUtc,
                   N'stop' AS Source
            FROM TripStops
            WHERE TripId = @TripId AND IsDeleted = 0
              AND (ArrivalTime IS NOT NULL OR DepartureTime IS NOT NULL)
            """, new { TripId = tripId }, cancellationToken: cancellationToken))).ToList();

        var liveEvents = statusEvents
            .Concat(customEvents)
            .Concat(stopEvents)
            .OrderBy(e => e.OccurredAtUtc)
            .ToList();

        var conflict = await GetAssignmentConflictAsync(tripId, trip.DriverId, trip.VehicleId, cancellationToken);

        var load = await tripRepository.GetDetailAsync(tripId, cancellationToken);
        var podDocs = load.Documents
            .Where(d => string.Equals(d.DocumentType, "ProofOfDelivery", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(d.DocumentType, "DeliveryNote", StringComparison.OrdinalIgnoreCase))
            .Select(d => new TripDocumentDto(d.Id, d.DocumentType, d.FileName, d.StorageKey, d.UploadedBy, d.CreatedAt))
            .ToList();

        return new TripInsightsDto(
            trip.InvoiceStatus,
            trip.InvoiceAmount,
            feedback,
            new TripProfitabilityDto(revenue, expenseTotal, margin, marginPct),
            deviation,
            liveEvents,
            rejects,
            conflict,
            podDocs);
    }

    public async Task<TripCostEstimateDto> EstimateCostAsync(
        TripCostEstimateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        if (request.RouteId is > 0 && request.VehicleId is > 0)
        {
            var route = await pricingRepository.GetRoutePricingAsync(tenantId, request.RouteId.Value, cancellationToken);
            var fuelAvg = await pricingRepository.GetVehicleFuelAverageAsync(tenantId, request.VehicleId.Value, cancellationToken);
            if (route is not null && fuelAvg is > 0)
            {
                var mult = request.IsRoundTrip ? 2m : 1m;
                var fuel = (route.Distance / fuelAvg.Value) * request.FuelPricePerLiter * mult;
                var total = fuel + request.DriverAllowance + request.TollCharges + request.OtherCharges;
                return new TripCostEstimateDto(
                    route.Distance, Math.Round(fuel, 2), request.DriverAllowance, request.TollCharges,
                    request.OtherCharges, Math.Round(total, 2), "PricingEngine", null);
            }
        }

        var distance = request.PlannedDistanceKm ?? 0;
        if (distance <= 0)
        {
            return new TripCostEstimateDto(
                null, null, request.DriverAllowance, request.TollCharges, request.OtherCharges,
                request.DriverAllowance + request.TollCharges + request.OtherCharges,
                "Manual", "Add route + vehicle or planned distance for fuel estimate.");
        }

        const decimal defaultPerKm = 35m;
        var est = distance * defaultPerKm * (request.IsRoundTrip ? 2m : 1m)
                  + request.DriverAllowance + request.TollCharges + request.OtherCharges;
        return new TripCostEstimateDto(
            distance, Math.Round(distance * defaultPerKm, 2), request.DriverAllowance, request.TollCharges,
            request.OtherCharges, Math.Round(est, 2), "DistanceHeuristic", "Using PKR 35/km heuristic.");
    }

    public async Task RecordTimelineEventAsync(
        int tripId,
        string eventType,
        string title,
        string? detail,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var actor = currentUser.UserId?.ToString();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TripTimelineEvents (TenantId, TripId, EventType, Title, Detail, CreatedBy)
            VALUES (@TenantId, @TripId, @EventType, @Title, @Detail, @CreatedBy)
            """,
            new { TenantId = tenantId, TripId = tripId, EventType = eventType, Title = title, Detail = detail, CreatedBy = actor },
            cancellationToken: cancellationToken));
    }

    public async Task<TripAssignmentConflictDto?> GetAssignmentConflictAsync(
        int tripId,
        int? driverId,
        int? vehicleId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var plannedStart = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT PlannedStart FROM Trips WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = tripId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (plannedStart is null)
            return null;

        if (driverId is > 0)
        {
            var driverHit = await connection.QuerySingleOrDefaultAsync<(int Id, string TripNumber)>(new CommandDefinition("""
                SELECT TOP 1 Id, TripNumber FROM Trips
                WHERE TenantId = @TenantId AND IsDeleted = 0 AND Id <> @TripId AND DriverId = @DriverId
                  AND Status IN (@Scheduled, @DriverAssigned, @VehicleAssigned, @Started, @AtPickup, @Enroute, @Delayed)
                  AND PlannedStart < DATEADD(HOUR, 4, @PlannedStart)
                  AND DATEADD(HOUR, 4, PlannedStart) > @PlannedStart
                """,
                new
                {
                    TenantId = tenantId,
                    TripId = tripId,
                    DriverId = driverId,
                    PlannedStart = plannedStart,
                    Scheduled = (int)TripStatus.Scheduled,
                    DriverAssigned = (int)TripStatus.DriverAssigned,
                    VehicleAssigned = (int)TripStatus.VehicleAssigned,
                    Started = (int)TripStatus.Started,
                    AtPickup = (int)TripStatus.AtPickup,
                    Enroute = (int)TripStatus.Enroute,
                    Delayed = (int)TripStatus.Delayed
                },
                cancellationToken: cancellationToken));
            if (driverHit.Id > 0)
                return new TripAssignmentConflictDto(true, "Driver has a conflicting trip in this window.", driverHit.Id, driverHit.TripNumber);
        }

        if (vehicleId is > 0)
        {
            var vehicleHit = await connection.QuerySingleOrDefaultAsync<(int Id, string TripNumber)>(new CommandDefinition("""
                SELECT TOP 1 Id, TripNumber FROM Trips
                WHERE TenantId = @TenantId AND IsDeleted = 0 AND Id <> @TripId AND VehicleId = @VehicleId
                  AND Status IN (@Scheduled, @DriverAssigned, @VehicleAssigned, @Started, @AtPickup, @Enroute, @Delayed)
                  AND PlannedStart < DATEADD(HOUR, 4, @PlannedStart)
                  AND DATEADD(HOUR, 4, PlannedStart) > @PlannedStart
                """,
                new
                {
                    TenantId = tenantId,
                    TripId = tripId,
                    VehicleId = vehicleId,
                    PlannedStart = plannedStart,
                    Scheduled = (int)TripStatus.Scheduled,
                    DriverAssigned = (int)TripStatus.DriverAssigned,
                    VehicleAssigned = (int)TripStatus.VehicleAssigned,
                    Started = (int)TripStatus.Started,
                    AtPickup = (int)TripStatus.AtPickup,
                    Enroute = (int)TripStatus.Enroute,
                    Delayed = (int)TripStatus.Delayed
                },
                cancellationToken: cancellationToken));
            if (vehicleHit.Id > 0)
                return new TripAssignmentConflictDto(true, "Vehicle has a conflicting trip in this window.", vehicleHit.Id, vehicleHit.TripNumber);
        }

        return new TripAssignmentConflictDto(false, null, null, null);
    }

    public async Task LogDelayedEtaSentAsync(int tripId, string channel, string message, CancellationToken cancellationToken = default)
    {
        await RecordTimelineEventAsync(tripId, "DelayedEta", $"ETA sent ({channel})", message, cancellationToken);
    }

    private static TripRouteDeviationDto BuildDeviation(decimal? planned, decimal? actual)
    {
        if (planned is null or <= 0 || actual is null)
            return new TripRouteDeviationDto(planned, actual, null, null, "Insufficient distance data.");

        var devKm = actual.Value - planned.Value;
        var pct = Math.Round(devKm / planned.Value * 100, 1);
        var summary = Math.Abs(pct) < 10
            ? "Within expected route tolerance."
            : pct > 0
                ? $"Actual route was {pct}% longer than planned."
                : $"Actual route was {Math.Abs(pct)}% shorter than planned.";
        return new TripRouteDeviationDto(planned, actual, devKm, pct, summary);
    }

    private static DateTime ComputeNextOccurrence(DateTime from, string kind, int interval)
    {
        interval = Math.Max(1, interval);
        return kind.ToUpperInvariant() switch
        {
            "WEEKLY" => from.AddDays(7 * interval),
            "MONTHLY" => from.AddMonths(interval),
            "DAILY" => from.AddDays(interval),
            _ => from.AddDays(interval)
        };
    }

    private sealed class CloneSourceRow
    {
        public string TripName { get; init; } = "";
        public int TripType { get; init; }
        public int? BookingId { get; init; }
        public int CustomerId { get; init; }
        public int? RouteId { get; init; }
        public int PassengerCount { get; init; }
        public int Priority { get; init; }
        public string? PickupAddress { get; init; }
        public double? PickupLatitude { get; init; }
        public double? PickupLongitude { get; init; }
        public string? DestinationAddress { get; init; }
        public double? DestinationLatitude { get; init; }
        public double? DestinationLongitude { get; init; }
        public DateTime TripDate { get; init; }
        public DateTime PlannedStart { get; init; }
        public DateTime? PlannedEnd { get; init; }
        public int? EstimatedDurationMinutes { get; init; }
        public decimal? PlannedDistanceKm { get; init; }
        public string? DriverNotes { get; init; }
        public string? SpecialCargoFlags { get; init; }
        public decimal? EstimatedRevenue { get; init; }
    }

    private sealed class TripInsightRow
    {
        public int Id { get; init; }
        public int Status { get; init; }
        public int? DriverId { get; init; }
        public int? VehicleId { get; init; }
        public DateTime PlannedStart { get; init; }
        public decimal? PlannedDistanceKm { get; init; }
        public decimal? ActualDistanceKm { get; init; }
        public decimal? EstimatedRevenue { get; init; }
        public string? InvoiceStatus { get; init; }
        public decimal? InvoiceAmount { get; init; }
    }
}
