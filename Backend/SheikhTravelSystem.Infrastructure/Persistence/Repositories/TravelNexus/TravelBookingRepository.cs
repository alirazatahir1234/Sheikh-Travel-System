using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Booking files, flight and hotel services, cancellations and history. Root predicate <c>b.TenantId = @TenantId</c>
/// (or the service's own TenantId); every join repeats the tenant predicate. Writes that depend on a parent use
/// <c>INSERT … SELECT … WHERE parent.TenantId = @TenantId</c>; status-dependent writes check the expected status and
/// write history in the same transaction.
/// </summary>
public sealed class TravelBookingRepository(IDbConnectionFactory dbFactory) : ITravelBookingRepository
{
    private const string ListSelect = """
        SELECT b.Id, b.BookingNumber, b.Title, b.CustomerId, b.GroupId, COALESCE(c.FullName, g.Name) AS OwnerName, b.Status,
               d.FirstTravelDate, d.LastTravelDate,
               (SELECT COUNT(1) FROM TravelFlightBookings f WHERE f.BookingId = b.Id AND f.TenantId = b.TenantId AND f.IsDeleted = 0 AND f.Status <> N'Cancelled') AS FlightCount,
               (SELECT COUNT(1) FROM TravelHotelBookings h WHERE h.BookingId = b.Id AND h.TenantId = b.TenantId AND h.IsDeleted = 0 AND h.Status <> N'Cancelled') AS HotelCount,
               (SELECT STRING_AGG(f.Pnr, N', ') FROM TravelFlightBookings f
                 WHERE f.BookingId = b.Id AND f.TenantId = b.TenantId AND f.IsDeleted = 0 AND f.Status <> N'Cancelled' AND f.Pnr IS NOT NULL) AS Pnrs,
               (SELECT MIN(f.TicketingTimeLimitUtc) FROM TravelFlightBookings f
                 WHERE f.BookingId = b.Id AND f.TenantId = b.TenantId AND f.IsDeleted = 0 AND f.Status = N'OnHold') AS NextTicketingLimitUtc,
               u.FullName AS AssignedAgentName, b.CreatedAt
        """;

    private const string FromJoins = """
        FROM TravelBookings b
        LEFT JOIN Customers c ON c.Id = b.CustomerId AND c.TenantId = b.TenantId
        LEFT JOIN TravelGroups g ON g.Id = b.GroupId AND g.TenantId = b.TenantId
        LEFT JOIN Users u ON u.Id = b.AssignedAgentUserId AND u.TenantId = b.TenantId
        OUTER APPLY (
            SELECT MIN(x.FromDate) AS FirstTravelDate, MAX(x.ToDate) AS LastTravelDate FROM (
                SELECT CAST(s.DepartLocal AS DATE) AS FromDate, CAST(s.ArriveLocal AS DATE) AS ToDate
                FROM TravelFlightSegments s
                INNER JOIN TravelFlightBookings f ON f.Id = s.FlightBookingId AND f.TenantId = s.TenantId
                WHERE f.BookingId = b.Id AND f.TenantId = b.TenantId AND f.IsDeleted = 0 AND f.Status <> N'Cancelled'
                UNION ALL
                SELECT h.CheckIn, h.CheckOut FROM TravelHotelBookings h
                WHERE h.BookingId = b.Id AND h.TenantId = b.TenantId AND h.IsDeleted = 0 AND h.Status <> N'Cancelled'
            ) x
        ) d
        """;

    // ── Files: reads ────────────────────────────────────────────────────────

    public async Task<TravelBookingState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Id", id);
        var clauses = new List<string> { "b.TenantId = @TenantId", "b.Id = @Id", "b.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", clauses, p);
        return await connection.QuerySingleOrDefaultAsync<TravelBookingState>(new CommandDefinition(
            $"SELECT b.Id, b.BookingNumber, b.Status, b.CustomerId, b.GroupId, b.BranchId FROM TravelBookings b WHERE {string.Join(" AND ", clauses)}",
            p, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TravelBookingListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelBookingListFilter f,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "b.TenantId = @TenantId", "b.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", clauses, p);
        if (f.Search is not null)
        {
            clauses.Add("""
                (b.BookingNumber LIKE @Search OR b.Title LIKE @Search OR c.FullName LIKE @Search OR g.Name LIKE @Search
                 OR EXISTS (SELECT 1 FROM TravelFlightBookings sf WHERE sf.BookingId = b.Id AND sf.TenantId = b.TenantId AND sf.IsDeleted = 0
                            AND (sf.Pnr LIKE @Search OR sf.AirlinePnr LIKE @Search)))
                """);
            p.Add("Search", $"%{TravelSql.EscapeLike(f.Search)}%");
        }
        if (f.Status is not null) { clauses.Add("b.Status = @Status"); p.Add("Status", f.Status); }
        if (f.OwnerType == "Customer") clauses.Add("b.CustomerId IS NOT NULL");
        if (f.OwnerType == "Group") clauses.Add("b.GroupId IS NOT NULL");
        if (f.CustomerId.HasValue) { clauses.Add("b.CustomerId = @CustomerId"); p.Add("CustomerId", f.CustomerId); }
        if (f.GroupId.HasValue) { clauses.Add("b.GroupId = @GroupId"); p.Add("GroupId", f.GroupId); }
        if (f.TravelFrom.HasValue) { clauses.Add("d.FirstTravelDate >= @TravelFrom"); p.Add("TravelFrom", f.TravelFrom.Value.Date); }
        if (f.TravelTo.HasValue) { clauses.Add("d.FirstTravelDate <= @TravelTo"); p.Add("TravelTo", f.TravelTo.Value.Date); }
        if (f.TicketingDueBeforeUtc.HasValue)
        {
            clauses.Add("""
                EXISTS (SELECT 1 FROM TravelFlightBookings tf WHERE tf.BookingId = b.Id AND tf.TenantId = b.TenantId AND tf.IsDeleted = 0
                        AND tf.Status = N'OnHold' AND tf.TicketingTimeLimitUtc <= @DueBefore)
                """);
            p.Add("DueBefore", f.TicketingDueBeforeUtc.Value);
        }

        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(1) {FromJoins} WHERE {where}", p, cancellationToken: cancellationToken));
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelBookingListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {FromJoins}
            WHERE {where}
            ORDER BY CASE WHEN d.FirstTravelDate IS NULL THEN 1 ELSE 0 END, d.FirstTravelDate DESC, b.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelBookingListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "b.TenantId = @TenantId", "b.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", clauses, p);
        var rows = await connection.QueryAsync<(string Status, int Total)>(new CommandDefinition(
            $"SELECT b.Status, COUNT(1) FROM TravelBookings b WHERE {string.Join(" AND ", clauses)} GROUP BY b.Status",
            p, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Status, r => r.Total);
    }

    public async Task<int> CountTicketingDueAsync(int tenantId, TravelBranchScope scope, DateTime untilUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Until", untilUtc);
        var clauses = new List<string>
        {
            "f.TenantId = @TenantId", "f.IsDeleted = 0", "f.Status = N'OnHold'", "f.TicketingTimeLimitUtc <= @Until",
            "b.IsDeleted = 0", "b.Status = N'Open'"
        };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", clauses, p);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(1) FROM TravelFlightBookings f
            INNER JOIN TravelBookings b ON b.Id = f.BookingId AND b.TenantId = f.TenantId
            WHERE {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TravelBookingListItemDto>> GetByCustomerAsync(int customerId, int tenantId, TravelBranchScope scope,
        CancellationToken cancellationToken = default)
        => await ListByOwnerAsync("b.CustomerId = @OwnerId", customerId, tenantId, scope, cancellationToken);

    public async Task<IReadOnlyList<TravelBookingListItemDto>> GetByGroupAsync(int groupId, int tenantId, TravelBranchScope scope,
        CancellationToken cancellationToken = default)
        => await ListByOwnerAsync("b.GroupId = @OwnerId", groupId, tenantId, scope, cancellationToken);

    private async Task<IReadOnlyList<TravelBookingListItemDto>> ListByOwnerAsync(string ownerClause, int ownerId, int tenantId, TravelBranchScope scope,
        CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("OwnerId", ownerId);
        var clauses = new List<string> { "b.TenantId = @TenantId", "b.IsDeleted = 0", ownerClause };
        TravelSql.ApplyBranchScope(scope, "b.BranchId", clauses, p);
        return (await connection.QueryAsync<TravelBookingListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {FromJoins}
            WHERE {string.Join(" AND ", clauses)}
            ORDER BY b.CreatedAt DESC
            OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY
            """, p, cancellationToken: ct))).ToList();
    }

    public async Task<TravelBookingDto?> GetDetailAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { Id = id, TenantId = tenantId };
        var booking = await connection.QuerySingleOrDefaultAsync<TravelBookingDto>(new CommandDefinition($"""
            {ListSelect},
                   c.FullName AS CustomerName, g.Name AS GroupName, g.GroupNumber, b.EnquiryId, e.EnquiryNumber, b.VisaCaseId,
                   v.CaseNumber AS VisaCaseNumber, b.AssignedAgentUserId, b.BranchId, br.Name AS BranchName, b.Notes, b.CompletedAt, b.CancelReason
            {FromJoins}
            LEFT JOIN TravelEnquiries e ON e.Id = b.EnquiryId AND e.TenantId = b.TenantId
            LEFT JOIN TravelVisaCases v ON v.Id = b.VisaCaseId AND v.TenantId = b.TenantId
            LEFT JOIN Branches br ON br.Id = b.BranchId AND br.TenantId = b.TenantId
            WHERE b.Id = @Id AND b.TenantId = @TenantId AND b.IsDeleted = 0
            """, args, cancellationToken: cancellationToken));
        if (booking is null) return null;

        var flights = (await connection.QueryAsync<TravelFlightBookingDto>(new CommandDefinition("""
            SELECT f.Id, f.SupplierId, sp.Name AS SupplierName, f.Direction, f.Pnr, f.AirlinePnr, f.Status, f.TicketingTimeLimitUtc,
                   f.SellAmount, f.CostAmount, f.CurrencyCode, f.Notes
            FROM TravelFlightBookings f
            LEFT JOIN TravelSuppliers sp ON sp.Id = f.SupplierId AND sp.TenantId = f.TenantId
            WHERE f.BookingId = @Id AND f.TenantId = @TenantId AND f.IsDeleted = 0
            ORDER BY f.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        var segments = (await connection.QueryAsync<TravelFlightSegmentDto>(new CommandDefinition("""
            SELECT s.Id, s.FlightBookingId, s.Sequence, s.AirlineCode, s.FlightNumber, s.FromAirport, fa.City AS FromCity, s.ToAirport, ta.City AS ToCity,
                   s.DepartLocal, s.ArriveLocal, s.DepartUtc, s.ArriveUtc, s.CabinClass, s.BookingClass, s.Baggage
            FROM TravelFlightSegments s
            INNER JOIN TravelFlightBookings f ON f.Id = s.FlightBookingId AND f.TenantId = s.TenantId
            LEFT JOIN TravelAirports fa ON fa.IataCode = s.FromAirport
            LEFT JOIN TravelAirports ta ON ta.IataCode = s.ToAirport
            WHERE f.BookingId = @Id AND s.TenantId = @TenantId AND f.IsDeleted = 0
            ORDER BY s.FlightBookingId, s.Sequence
            """, args, cancellationToken: cancellationToken))).ToList();
        var passengers = (await connection.QueryAsync<TravelFlightPassengerDto>(new CommandDefinition("""
            SELECT fp.Id, fp.FlightBookingId, fp.TravelerId, CONCAT(t.GivenNames, ' ', t.Surname) AS TravelerName, fp.GroupMemberId,
                   fp.PassengerType, t.PassportNumber, fp.TicketNumber, fp.TicketStatus
            FROM TravelFlightPassengers fp
            INNER JOIN TravelFlightBookings f ON f.Id = fp.FlightBookingId AND f.TenantId = fp.TenantId
            INNER JOIN TravelTravelers t ON t.Id = fp.TravelerId AND t.TenantId = fp.TenantId
            WHERE f.BookingId = @Id AND fp.TenantId = @TenantId AND f.IsDeleted = 0
            ORDER BY fp.FlightBookingId, t.Surname, t.GivenNames
            """, args, cancellationToken: cancellationToken))).ToList();
        foreach (var f in flights)
        {
            f.Segments = segments.Where(s => s.FlightBookingId == f.Id).ToList();
            f.Passengers = passengers.Where(x => x.FlightBookingId == f.Id).ToList();
        }
        booking.Flights = flights.OrderBy(f => f.Segments.Count == 0 ? DateTime.MaxValue : f.Segments[0].DepartUtc).ThenBy(f => f.Id).ToList();

        var hotels = (await connection.QueryAsync<TravelHotelBookingDto>(new CommandDefinition("""
            SELECT h.Id, h.HotelId, ht.Name AS HotelName, ht.City, ht.Address AS HotelAddress, ht.Phone AS HotelPhone, h.SupplierId, sp.Name AS SupplierName,
                   h.GroupHotelId, h.CheckIn, h.CheckOut, h.MealPlan, h.ConfirmationNumber, h.Status, h.SpecialRequests, h.SellAmount, h.CostAmount, h.CurrencyCode
            FROM TravelHotelBookings h
            INNER JOIN TravelHotels ht ON ht.Id = h.HotelId AND ht.TenantId = h.TenantId
            LEFT JOIN TravelSuppliers sp ON sp.Id = h.SupplierId AND sp.TenantId = h.TenantId
            WHERE h.BookingId = @Id AND h.TenantId = @TenantId AND h.IsDeleted = 0
            ORDER BY h.CheckIn, h.Id
            """, args, cancellationToken: cancellationToken))).ToList();
        var rooms = (await connection.QueryAsync<TravelHotelBookingRoomDto>(new CommandDefinition("""
            SELECT r.HotelBookingId, r.RoomType, r.Quantity
            FROM TravelHotelBookingRooms r
            INNER JOIN TravelHotelBookings h ON h.Id = r.HotelBookingId AND h.TenantId = r.TenantId
            WHERE h.BookingId = @Id AND r.TenantId = @TenantId AND h.IsDeleted = 0
            """, args, cancellationToken: cancellationToken))).ToList();
        var guests = (await connection.QueryAsync<TravelHotelBookingGuestDto>(new CommandDefinition("""
            SELECT gu.HotelBookingId, gu.TravelerId, CONCAT(t.GivenNames, ' ', t.Surname) AS TravelerName
            FROM TravelHotelBookingGuests gu
            INNER JOIN TravelHotelBookings h ON h.Id = gu.HotelBookingId AND h.TenantId = gu.TenantId
            INNER JOIN TravelTravelers t ON t.Id = gu.TravelerId AND t.TenantId = gu.TenantId
            WHERE h.BookingId = @Id AND gu.TenantId = @TenantId AND h.IsDeleted = 0
            ORDER BY t.Surname, t.GivenNames
            """, args, cancellationToken: cancellationToken))).ToList();
        foreach (var h in hotels)
        {
            h.Rooms = rooms.Where(r => r.HotelBookingId == h.Id).OrderBy(r => Array.IndexOf(TravelValues.RoomTypes, r.RoomType)).ToList();
            h.Guests = guests.Where(x => x.HotelBookingId == h.Id).ToList();
        }
        booking.Hotels = hotels;

        booking.Cancellations = (await connection.QueryAsync<TravelBookingCancellationDto>(new CommandDefinition("""
            SELECT x.Id, x.ServiceType, x.FlightBookingId, x.HotelBookingId, x.FlightPassengerId,
                   CASE WHEN x.ServiceType = N'Flight'
                        THEN CONCAT(N'PNR ', COALESCE(f.Pnr, N'—'), CASE WHEN fp.Id IS NOT NULL THEN CONCAT(N' · ', t.GivenNames, N' ', t.Surname) ELSE N'' END)
                        ELSE ht.Name END AS ServiceLabel,
                   x.Reason, x.SupplierPenalty, x.CustomerPenalty, x.RefundFromSupplier, x.RefundToCustomer, x.CurrencyCode, x.RefundStatus,
                   x.CancelledAt, u.FullName AS CancelledByName
            FROM TravelBookingCancellations x
            LEFT JOIN TravelFlightBookings f ON f.Id = x.FlightBookingId AND f.TenantId = x.TenantId
            LEFT JOIN TravelFlightPassengers fp ON fp.Id = x.FlightPassengerId AND fp.TenantId = x.TenantId
            LEFT JOIN TravelTravelers t ON t.Id = fp.TravelerId AND t.TenantId = x.TenantId
            LEFT JOIN TravelHotelBookings hb ON hb.Id = x.HotelBookingId AND hb.TenantId = x.TenantId
            LEFT JOIN TravelHotels ht ON ht.Id = hb.HotelId AND ht.TenantId = x.TenantId
            LEFT JOIN Users u ON u.Id = x.CancelledBy AND u.TenantId = x.TenantId
            WHERE x.BookingId = @Id AND x.TenantId = @TenantId
            ORDER BY x.CancelledAt DESC, x.Id DESC
            """, args, cancellationToken: cancellationToken))).ToList();

        booking.History = (await connection.QueryAsync<TravelBookingHistoryDto>(new CommandDefinition("""
            SELECT h.Id, h.ServiceType, h.ServiceId, h.EventType, h.FromValue, h.ToValue, h.Note, u.FullName AS ChangedByName, h.ChangedAt
            FROM TravelBookingHistory h
            LEFT JOIN Users u ON u.Id = h.ChangedBy AND u.TenantId = h.TenantId
            WHERE h.BookingId = @Id AND h.TenantId = @TenantId
            ORDER BY h.ChangedAt DESC, h.Id DESC
            """, args, cancellationToken: cancellationToken))).ToList();
        return booking;
    }

    public async Task<TravelBookingServiceFacts> GetServiceFactsAsync(int bookingId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleAsync<TravelBookingServiceFacts>(new CommandDefinition("""
            SELECT
              (SELECT COUNT(1) FROM TravelFlightBookings f WHERE f.BookingId = @Id AND f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status <> N'Cancelled') AS OpenFlights,
              (SELECT COUNT(1) FROM TravelHotelBookings h WHERE h.BookingId = @Id AND h.TenantId = @TenantId AND h.IsDeleted = 0 AND h.Status <> N'Cancelled') AS OpenHotels,
              (SELECT COUNT(1) FROM TravelFlightBookings f WHERE f.BookingId = @Id AND f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status NOT IN (N'Ticketed', N'Cancelled'))
              + (SELECT COUNT(1) FROM TravelHotelBookings h WHERE h.BookingId = @Id AND h.TenantId = @TenantId AND h.IsDeleted = 0
                   AND h.Status NOT IN (N'CheckedOut', N'NoShow', N'Cancelled')) AS UnfinishedServices,
              (SELECT COUNT(1) FROM TravelFlightBookings f WHERE f.BookingId = @Id AND f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status = N'Cancelled')
              + (SELECT COUNT(1) FROM TravelHotelBookings h WHERE h.BookingId = @Id AND h.TenantId = @TenantId AND h.IsDeleted = 0 AND h.Status = N'Cancelled') AS CancelledServices,
              (SELECT MAX(x.EndUtc) FROM (
                  SELECT s.ArriveUtc AS EndUtc FROM TravelFlightSegments s
                  INNER JOIN TravelFlightBookings f ON f.Id = s.FlightBookingId AND f.TenantId = s.TenantId
                  WHERE f.BookingId = @Id AND f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status <> N'Cancelled'
                  UNION ALL
                  SELECT CAST(h.CheckOut AS DATETIME2) FROM TravelHotelBookings h
                  WHERE h.BookingId = @Id AND h.TenantId = @TenantId AND h.IsDeleted = 0 AND h.Status <> N'Cancelled') x) AS LastServiceUtc
            """, new { Id = bookingId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    // ── Files: writes ───────────────────────────────────────────────────────

    public async Task<int> CreateAsync(int tenantId, CreateTravelBookingDto dto, int? userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.Booking, utcNow, cancellationToken);
        // The owner must be an active travel customer or a group of this tenant.
        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            INSERT INTO TravelBookings (TenantId, BookingNumber, CustomerId, GroupId, EnquiryId, VisaCaseId, Title, Status, AssignedAgentUserId, BranchId, Notes, CreatedBy)
            SELECT @TenantId, @Number, @CustomerId, @GroupId, @EnquiryId, @VisaCaseId, @Title, N'Open', @AgentId, @BranchId, @Notes, @UserId
            WHERE (@CustomerId IS NULL OR EXISTS (SELECT 1 FROM TravelCustomerProfiles cp WHERE cp.CustomerId = @CustomerId AND cp.TenantId = @TenantId AND cp.IsDeleted = 0))
              AND (@GroupId IS NULL OR EXISTS (SELECT 1 FROM TravelGroups gr WHERE gr.Id = @GroupId AND gr.TenantId = @TenantId AND gr.IsDeleted = 0));
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
            {
                TenantId = tenantId, Number = number, dto.CustomerId, dto.GroupId, dto.EnquiryId, dto.VisaCaseId, dto.Title,
                AgentId = dto.AssignedAgentUserId, dto.BranchId, dto.Notes, UserId = userId
            }, tx, cancellationToken: cancellationToken)) ?? 0;
        if (id == 0)
        {
            tx.Rollback();
            return 0;
        }
        await HistoryAsync(connection, tx, tenantId, id, null, null, "FileStatus", null, "Open", "Booking file opened", userId, cancellationToken);
        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, int tenantId, UpdateTravelBookingDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelBookings SET Title = @Title, EnquiryId = @EnquiryId, VisaCaseId = @VisaCaseId, AssignedAgentUserId = @AgentId,
                Notes = @Notes, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Open'
            """, new { Id = id, TenantId = tenantId, dto.Title, dto.EnquiryId, dto.VisaCaseId, AgentId = dto.AssignedAgentUserId, dto.Notes },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> ChangeStatusAsync(int tenantId, int id, string expectedStatus, string newStatus, string? note, string? cancelReason,
        int? userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelBookings SET Status = @NewStatus,
                CompletedAt = CASE WHEN @NewStatus = N'Completed' THEN @Now WHEN @NewStatus = N'Open' THEN NULL ELSE CompletedAt END,
                CancelReason = CASE WHEN @NewStatus = N'Cancelled' THEN @CancelReason ELSE CancelReason END,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected
            """, new { Id = id, TenantId = tenantId, Expected = expectedStatus, NewStatus = newStatus, CancelReason = cancelReason, Now = utcNow },
            tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await HistoryAsync(connection, tx, tenantId, id, null, null, "FileStatus", expectedStatus, newStatus, note, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    // ── Flights ─────────────────────────────────────────────────────────────

    public async Task<TravelFlightState?> GetFlightStateAsync(int bookingId, int flightId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var args = new { BookingId = bookingId, FlightId = flightId, TenantId = tenantId };
        var state = await connection.QuerySingleOrDefaultAsync<TravelFlightState>(new CommandDefinition("""
            SELECT f.Id, f.BookingId, f.Status, f.Pnr, f.SellAmount, f.CostAmount, f.CurrencyCode,
                   (SELECT COUNT(1) FROM TravelFlightSegments s WHERE s.FlightBookingId = f.Id AND s.TenantId = f.TenantId) AS SegmentCount
            FROM TravelFlightBookings f
            WHERE f.Id = @FlightId AND f.BookingId = @BookingId AND f.TenantId = @TenantId AND f.IsDeleted = 0
            """, args, cancellationToken: cancellationToken));
        if (state is null) return null;
        var passengers = (await connection.QueryAsync<TravelFlightPassengerFacts>(new CommandDefinition("""
            SELECT Id, TravelerId, GroupMemberId, TicketStatus, TicketNumber FROM TravelFlightPassengers
            WHERE FlightBookingId = @FlightId AND TenantId = @TenantId
            """, args, cancellationToken: cancellationToken))).ToList();
        return state with { Passengers = passengers };
    }

    public async Task<bool> PnrInUseAsync(int tenantId, string pnr, int? excludeFlightId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelFlightBookings
            WHERE TenantId = @TenantId AND Pnr = @Pnr AND Status <> N'Cancelled' AND IsDeleted = 0 AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            """, new { TenantId = tenantId, Pnr = pnr, ExcludeId = excludeFlightId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<IReadOnlyList<string>> TicketNumbersInUseAsync(int tenantId, IReadOnlyList<string> ticketNumbers, int flightId,
        CancellationToken cancellationToken = default)
    {
        if (ticketNumbers.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT TicketNumber FROM TravelFlightPassengers
            WHERE TenantId = @TenantId AND TicketNumber IN @Numbers AND FlightBookingId <> @FlightId
            """, new { TenantId = tenantId, Numbers = ticketNumbers.ToArray(), FlightId = flightId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<int> SaveFlightAsync(int tenantId, int bookingId, int? flightId, SaveTravelFlightDto dto, IReadOnlyList<TravelFlightSegmentWrite> segments,
        bool replacePassengers, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var header = new
        {
            TenantId = tenantId, BookingId = bookingId, FlightId = flightId, dto.SupplierId, dto.Direction, dto.Pnr, dto.AirlinePnr,
            dto.SellAmount, dto.CostAmount, dto.CurrencyCode, dto.Notes, UserId = userId
        };
        int id;
        if (flightId is null)
        {
            id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                INSERT INTO TravelFlightBookings (TenantId, BookingId, SupplierId, Direction, Pnr, AirlinePnr, Status, SellAmount, CostAmount, CurrencyCode, Notes, CreatedBy)
                SELECT @TenantId, b.Id, @SupplierId, @Direction, @Pnr, @AirlinePnr, N'Requested', @SellAmount, @CostAmount, @CurrencyCode, @Notes, @UserId
                FROM TravelBookings b WHERE b.Id = @BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0 AND b.Status = N'Open';
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, header, tx, cancellationToken: cancellationToken)) ?? 0;
        }
        else
        {
            var rows = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE f SET SupplierId = @SupplierId, Direction = @Direction, Pnr = @Pnr, AirlinePnr = @AirlinePnr, SellAmount = @SellAmount,
                    CostAmount = @CostAmount, CurrencyCode = @CurrencyCode, Notes = @Notes, UpdatedAt = SYSUTCDATETIME()
                FROM TravelFlightBookings f
                INNER JOIN TravelBookings b ON b.Id = f.BookingId AND b.TenantId = f.TenantId AND b.Status = N'Open' AND b.IsDeleted = 0
                WHERE f.Id = @FlightId AND f.BookingId = @BookingId AND f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status <> N'Cancelled'
                """, header, tx, cancellationToken: cancellationToken));
            id = rows == 0 ? 0 : flightId.Value;
        }
        if (id == 0)
        {
            tx.Rollback();
            return 0;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM TravelFlightSegments WHERE FlightBookingId = @FlightId AND TenantId = @TenantId",
            new { FlightId = id, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        foreach (var s in segments)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelFlightSegments (TenantId, FlightBookingId, Sequence, AirlineCode, FlightNumber, FromAirport, ToAirport,
                    DepartLocal, ArriveLocal, DepartUtc, ArriveUtc, CabinClass, BookingClass, Baggage)
                VALUES (@TenantId, @FlightId, @Sequence, @AirlineCode, @FlightNumber, @FromAirport, @ToAirport,
                    @DepartLocal, @ArriveLocal, @DepartUtc, @ArriveUtc, @CabinClass, @BookingClass, @Baggage)
                """, new
                {
                    TenantId = tenantId, FlightId = id, s.Sequence, s.Segment.AirlineCode, s.Segment.FlightNumber, s.Segment.FromAirport,
                    s.Segment.ToAirport, s.Segment.DepartLocal, s.Segment.ArriveLocal, s.DepartUtc, s.ArriveUtc, s.Segment.CabinClass,
                    BookingClass = string.IsNullOrWhiteSpace(s.Segment.BookingClass) ? null : s.Segment.BookingClass,
                    Baggage = string.IsNullOrWhiteSpace(s.Segment.Baggage) ? null : s.Segment.Baggage.Trim()
                }, tx, cancellationToken: cancellationToken));
        }

        if (replacePassengers)
        {
            var keep = dto.Passengers.Select(p => p.TravelerId).ToArray();
            await connection.ExecuteAsync(new CommandDefinition(keep.Length == 0
                ? "DELETE FROM TravelFlightPassengers WHERE FlightBookingId = @FlightId AND TenantId = @TenantId AND TicketStatus = N'NotIssued'"
                : "DELETE FROM TravelFlightPassengers WHERE FlightBookingId = @FlightId AND TenantId = @TenantId AND TicketStatus = N'NotIssued' AND TravelerId NOT IN @Keep",
                new { FlightId = id, TenantId = tenantId, Keep = keep }, tx, cancellationToken: cancellationToken));
            foreach (var p in dto.Passengers)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE TravelFlightPassengers SET PassengerType = @PassengerType, GroupMemberId = @GroupMemberId
                    WHERE FlightBookingId = @FlightId AND TenantId = @TenantId AND TravelerId = @TravelerId;

                    IF @@ROWCOUNT = 0
                    INSERT INTO TravelFlightPassengers (TenantId, FlightBookingId, TravelerId, GroupMemberId, PassengerType)
                    SELECT @TenantId, @FlightId, t.Id, @GroupMemberId, @PassengerType
                    FROM TravelTravelers t WHERE t.Id = @TravelerId AND t.TenantId = @TenantId AND t.IsDeleted = 0;
                    """, new { TenantId = tenantId, FlightId = id, p.TravelerId, p.GroupMemberId, p.PassengerType }, tx, cancellationToken: cancellationToken));
            }
        }

        await HistoryAsync(connection, tx, tenantId, bookingId, "Flight", id, flightId is null ? "ServiceAdded" : "ServiceUpdated", null,
            dto.Pnr ?? dto.Direction, null, userId, cancellationToken);
        tx.Commit();
        return id;
    }

    public async Task<bool> TransitionFlightAsync(int tenantId, int bookingId, int flightId, string expectedStatus, string newStatus, string? pnr,
        DateTime? ticketingTimeLimitUtc, string? note, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE f SET Status = @NewStatus, Pnr = COALESCE(@Pnr, f.Pnr),
                TicketingTimeLimitUtc = CASE WHEN @NewStatus = N'OnHold' THEN @TimeLimit WHEN @NewStatus = N'Ticketed' THEN NULL ELSE f.TicketingTimeLimitUtc END,
                UpdatedAt = SYSUTCDATETIME()
            FROM TravelFlightBookings f
            INNER JOIN TravelBookings b ON b.Id = f.BookingId AND b.TenantId = f.TenantId AND b.Status = N'Open' AND b.IsDeleted = 0
            WHERE f.Id = @FlightId AND f.BookingId = @BookingId AND f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status = @Expected
            """, new
            {
                TenantId = tenantId, BookingId = bookingId, FlightId = flightId, Expected = expectedStatus, NewStatus = newStatus, Pnr = pnr,
                TimeLimit = ticketingTimeLimitUtc
            }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        var detail = newStatus == "OnHold" && ticketingTimeLimitUtc is DateTime limit ? $"Time limit {limit:yyyy-MM-dd HH:mm} UTC" : null;
        await HistoryAsync(connection, tx, tenantId, bookingId, "Flight", flightId, "FlightStatus", expectedStatus, newStatus,
            string.Join(" · ", new[] { detail, note }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } n ? n : null, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<bool> SetTicketsAsync(int tenantId, int bookingId, int flightId, string expectedStatus, IReadOnlyList<TravelTicketNumberDto> tickets,
        int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var locked = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT f.Id FROM TravelFlightBookings f WITH (UPDLOCK, ROWLOCK)
            INNER JOIN TravelBookings b ON b.Id = f.BookingId AND b.TenantId = f.TenantId AND b.Status = N'Open' AND b.IsDeleted = 0
            WHERE f.Id = @FlightId AND f.BookingId = @BookingId AND f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status = @Expected
            """, new { FlightId = flightId, BookingId = bookingId, TenantId = tenantId, Expected = expectedStatus }, tx, cancellationToken: cancellationToken));
        if (locked is null)
        {
            tx.Rollback();
            return false;
        }
        foreach (var t in tickets)
        {
            var previous = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition("""
                SELECT TicketNumber FROM TravelFlightPassengers WHERE Id = @Id AND FlightBookingId = @FlightId AND TenantId = @TenantId
                """, new { Id = t.PassengerId, FlightId = flightId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
            if (previous == t.TicketNumber) continue;
            var rows = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE TravelFlightPassengers SET TicketNumber = @Number, TicketStatus = N'Issued'
                WHERE Id = @Id AND FlightBookingId = @FlightId AND TenantId = @TenantId AND TicketStatus IN (N'NotIssued', N'Issued')
                """, new { Id = t.PassengerId, FlightId = flightId, TenantId = tenantId, Number = t.TicketNumber }, tx, cancellationToken: cancellationToken));
            if (rows == 0)
            {
                tx.Rollback();
                return false;
            }
            await HistoryAsync(connection, tx, tenantId, bookingId, "Flight", flightId, "TicketIssued", previous, t.TicketNumber,
                previous is null ? null : "Re-issued", userId, cancellationToken);
        }
        tx.Commit();
        return true;
    }

    public async Task<int?> CancelFlightAsync(int tenantId, int bookingId, int flightId, string expectedStatus, string ticketOutcome,
        TravelServiceCancellationWrite cancellation, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE f SET Status = N'Cancelled', TicketingTimeLimitUtc = NULL, UpdatedAt = SYSUTCDATETIME()
            FROM TravelFlightBookings f
            INNER JOIN TravelBookings b ON b.Id = f.BookingId AND b.TenantId = f.TenantId AND b.Status = N'Open' AND b.IsDeleted = 0
            WHERE f.Id = @FlightId AND f.BookingId = @BookingId AND f.TenantId = @TenantId AND f.IsDeleted = 0 AND f.Status = @Expected
            """, new { FlightId = flightId, BookingId = bookingId, TenantId = tenantId, Expected = expectedStatus }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return null;
        }
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelFlightPassengers SET TicketStatus = @Outcome
            WHERE FlightBookingId = @FlightId AND TenantId = @TenantId AND TicketStatus = N'Issued'
            """, new { FlightId = flightId, TenantId = tenantId, Outcome = ticketOutcome }, tx, cancellationToken: cancellationToken));
        var id = await InsertCancellationAsync(connection, tx, tenantId, bookingId, "Flight", flightId, null, null, cancellation, userId, cancellationToken);
        await HistoryAsync(connection, tx, tenantId, bookingId, "Flight", flightId, "Cancelled", expectedStatus, "Cancelled", cancellation.Reason, userId, cancellationToken);
        tx.Commit();
        return id;
    }

    public async Task<int?> CancelPassengerAsync(int tenantId, int bookingId, int flightId, int passengerId, string ticketOutcome,
        TravelServiceCancellationWrite cancellation, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE fp SET TicketStatus = @Outcome
            FROM TravelFlightPassengers fp
            INNER JOIN TravelFlightBookings f ON f.Id = fp.FlightBookingId AND f.TenantId = fp.TenantId AND f.Status = N'Ticketed' AND f.IsDeleted = 0
            INNER JOIN TravelBookings b ON b.Id = f.BookingId AND b.TenantId = f.TenantId AND b.Status = N'Open' AND b.IsDeleted = 0
            WHERE fp.Id = @PassengerId AND fp.FlightBookingId = @FlightId AND f.BookingId = @BookingId AND fp.TenantId = @TenantId AND fp.TicketStatus = N'Issued'
            """, new { PassengerId = passengerId, FlightId = flightId, BookingId = bookingId, TenantId = tenantId, Outcome = ticketOutcome },
            tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return null;
        }
        var id = await InsertCancellationAsync(connection, tx, tenantId, bookingId, "Flight", flightId, null, passengerId, cancellation, userId, cancellationToken);
        await HistoryAsync(connection, tx, tenantId, bookingId, "Flight", flightId, "TicketVoided", "Issued", ticketOutcome, cancellation.Reason, userId, cancellationToken);
        tx.Commit();
        return id;
    }

    public async Task<bool> DeleteFlightAsync(int tenantId, int bookingId, int flightId, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelFlightBookings SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @FlightId AND BookingId = @BookingId AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Requested'
            """, new { FlightId = flightId, BookingId = bookingId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await HistoryAsync(connection, tx, tenantId, bookingId, "Flight", flightId, "ServiceUpdated", "Requested", "Removed", null, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    // ── Hotels ──────────────────────────────────────────────────────────────

    public async Task<TravelHotelBookingState?> GetHotelStateAsync(int bookingId, int hotelBookingId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelHotelBookingState>(new CommandDefinition("""
            SELECT Id, BookingId, Status, CheckIn, CheckOut, GroupHotelId, SellAmount, CostAmount, CurrencyCode
            FROM TravelHotelBookings
            WHERE Id = @HotelBookingId AND BookingId = @BookingId AND TenantId = @TenantId AND IsDeleted = 0
            """, new { HotelBookingId = hotelBookingId, BookingId = bookingId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<int> SaveHotelAsync(int tenantId, int bookingId, int? hotelBookingId, SaveTravelHotelBookingDto dto, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var header = new
        {
            TenantId = tenantId, BookingId = bookingId, HotelBookingId = hotelBookingId, dto.HotelId, dto.SupplierId, dto.GroupHotelId,
            CheckIn = dto.CheckIn.Date, CheckOut = dto.CheckOut.Date, dto.MealPlan, dto.ConfirmationNumber, dto.SpecialRequests,
            dto.SellAmount, dto.CostAmount, dto.CurrencyCode, UserId = userId
        };
        int id;
        if (hotelBookingId is null)
        {
            id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                INSERT INTO TravelHotelBookings (TenantId, BookingId, HotelId, SupplierId, GroupHotelId, CheckIn, CheckOut, MealPlan, ConfirmationNumber,
                    Status, SpecialRequests, SellAmount, CostAmount, CurrencyCode, CreatedBy)
                SELECT @TenantId, b.Id, ht.Id, @SupplierId, @GroupHotelId, @CheckIn, @CheckOut, @MealPlan, @ConfirmationNumber,
                    N'Requested', @SpecialRequests, @SellAmount, @CostAmount, @CurrencyCode, @UserId
                FROM TravelBookings b
                INNER JOIN TravelHotels ht ON ht.Id = @HotelId AND ht.TenantId = b.TenantId AND ht.IsDeleted = 0
                WHERE b.Id = @BookingId AND b.TenantId = @TenantId AND b.IsDeleted = 0 AND b.Status = N'Open';
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, header, tx, cancellationToken: cancellationToken)) ?? 0;
        }
        else
        {
            var rows = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE h SET HotelId = ht.Id, SupplierId = @SupplierId, GroupHotelId = @GroupHotelId, CheckIn = @CheckIn, CheckOut = @CheckOut,
                    MealPlan = @MealPlan, ConfirmationNumber = @ConfirmationNumber, SpecialRequests = @SpecialRequests, SellAmount = @SellAmount,
                    CostAmount = @CostAmount, CurrencyCode = @CurrencyCode, UpdatedAt = SYSUTCDATETIME()
                FROM TravelHotelBookings h
                INNER JOIN TravelBookings b ON b.Id = h.BookingId AND b.TenantId = h.TenantId AND b.Status = N'Open' AND b.IsDeleted = 0
                INNER JOIN TravelHotels ht ON ht.Id = @HotelId AND ht.TenantId = h.TenantId AND ht.IsDeleted = 0
                WHERE h.Id = @HotelBookingId AND h.BookingId = @BookingId AND h.TenantId = @TenantId AND h.IsDeleted = 0
                  AND h.Status IN (N'Requested', N'Confirmed')
                """, header, tx, cancellationToken: cancellationToken));
            id = rows == 0 ? 0 : hotelBookingId.Value;
        }
        if (id == 0)
        {
            tx.Rollback();
            return 0;
        }

        var args = new { HotelBookingId = id, TenantId = tenantId };
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM TravelHotelBookingRooms WHERE HotelBookingId = @HotelBookingId AND TenantId = @TenantId;
            DELETE FROM TravelHotelBookingGuests WHERE HotelBookingId = @HotelBookingId AND TenantId = @TenantId;
            """, args, tx, cancellationToken: cancellationToken));
        foreach (var r in dto.Rooms)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelHotelBookingRooms (TenantId, HotelBookingId, RoomType, Quantity) VALUES (@TenantId, @HotelBookingId, @RoomType, @Quantity)
                """, new { TenantId = tenantId, HotelBookingId = id, r.RoomType, r.Quantity }, tx, cancellationToken: cancellationToken));
        }
        foreach (var travelerId in dto.GuestTravelerIds)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelHotelBookingGuests (TenantId, HotelBookingId, TravelerId)
                SELECT @TenantId, @HotelBookingId, t.Id FROM TravelTravelers t WHERE t.Id = @TravelerId AND t.TenantId = @TenantId AND t.IsDeleted = 0
                """, new { TenantId = tenantId, HotelBookingId = id, TravelerId = travelerId }, tx, cancellationToken: cancellationToken));
        }
        await HistoryAsync(connection, tx, tenantId, bookingId, "Hotel", id, hotelBookingId is null ? "ServiceAdded" : "ServiceUpdated", null,
            $"{dto.CheckIn:yyyy-MM-dd} → {dto.CheckOut:yyyy-MM-dd}", null, userId, cancellationToken);
        tx.Commit();
        return id;
    }

    public async Task<bool> TransitionHotelAsync(int tenantId, int bookingId, int hotelBookingId, string expectedStatus, string newStatus,
        string? confirmationNumber, string? note, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var args = new
        {
            TenantId = tenantId, BookingId = bookingId, HotelBookingId = hotelBookingId, Expected = expectedStatus, NewStatus = newStatus,
            Confirmation = confirmationNumber
        };
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE h SET Status = @NewStatus, ConfirmationNumber = COALESCE(@Confirmation, h.ConfirmationNumber), UpdatedAt = SYSUTCDATETIME()
            FROM TravelHotelBookings h
            INNER JOIN TravelBookings b ON b.Id = h.BookingId AND b.TenantId = h.TenantId AND b.Status = N'Open' AND b.IsDeleted = 0
            WHERE h.Id = @HotelBookingId AND h.BookingId = @BookingId AND h.TenantId = @TenantId AND h.IsDeleted = 0 AND h.Status = @Expected
            """, args, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        if (newStatus == "Confirmed" && confirmationNumber is not null)
        {
            // Keep the group's stay in step with the hotel's confirmation.
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE gh SET ConfirmationNumber = @Confirmation
                FROM TravelGroupHotels gh
                INNER JOIN TravelHotelBookings h ON h.GroupHotelId = gh.Id AND h.TenantId = gh.TenantId
                WHERE h.Id = @HotelBookingId AND h.TenantId = @TenantId AND gh.TenantId = @TenantId
                """, args, tx, cancellationToken: cancellationToken));
        }
        await HistoryAsync(connection, tx, tenantId, bookingId, "Hotel", hotelBookingId, "HotelStatus", expectedStatus, newStatus,
            confirmationNumber is null ? note : $"Confirmation {confirmationNumber}{(note is null ? "" : " · " + note)}", userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<int?> CancelHotelAsync(int tenantId, int bookingId, int hotelBookingId, string expectedStatus,
        TravelServiceCancellationWrite cancellation, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE h SET Status = N'Cancelled', UpdatedAt = SYSUTCDATETIME()
            FROM TravelHotelBookings h
            INNER JOIN TravelBookings b ON b.Id = h.BookingId AND b.TenantId = h.TenantId AND b.Status = N'Open' AND b.IsDeleted = 0
            WHERE h.Id = @HotelBookingId AND h.BookingId = @BookingId AND h.TenantId = @TenantId AND h.IsDeleted = 0 AND h.Status = @Expected
            """, new { HotelBookingId = hotelBookingId, BookingId = bookingId, TenantId = tenantId, Expected = expectedStatus }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return null;
        }
        var id = await InsertCancellationAsync(connection, tx, tenantId, bookingId, "Hotel", null, hotelBookingId, null, cancellation, userId, cancellationToken);
        await HistoryAsync(connection, tx, tenantId, bookingId, "Hotel", hotelBookingId, "Cancelled", expectedStatus, "Cancelled", cancellation.Reason, userId, cancellationToken);
        tx.Commit();
        return id;
    }

    public async Task<bool> DeleteHotelAsync(int tenantId, int bookingId, int hotelBookingId, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelHotelBookings SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @HotelBookingId AND BookingId = @BookingId AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Requested'
            """, new { HotelBookingId = hotelBookingId, BookingId = bookingId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await HistoryAsync(connection, tx, tenantId, bookingId, "Hotel", hotelBookingId, "ServiceUpdated", "Requested", "Removed", null, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    // ── Cancellations ───────────────────────────────────────────────────────

    public async Task<TravelCancellationState?> GetCancellationAsync(int bookingId, int cancellationId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TravelCancellationState>(new CommandDefinition("""
            SELECT Id, BookingId, RefundStatus FROM TravelBookingCancellations WHERE Id = @Id AND BookingId = @BookingId AND TenantId = @TenantId
            """, new { Id = cancellationId, BookingId = bookingId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateRefundStatusAsync(int tenantId, int bookingId, int cancellationId, string expectedStatus, string newStatus, string? note,
        int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelBookingCancellations SET RefundStatus = @NewStatus, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND BookingId = @BookingId AND TenantId = @TenantId AND RefundStatus = @Expected
            """, new { Id = cancellationId, BookingId = bookingId, TenantId = tenantId, Expected = expectedStatus, NewStatus = newStatus },
            tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await HistoryAsync(connection, tx, tenantId, bookingId, null, cancellationId, "RefundStatus", expectedStatus, newStatus, note, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    // ── Reference checks ────────────────────────────────────────────────────

    public async Task<bool> SupplierExistsAsync(int tenantId, int supplierId, CancellationToken cancellationToken = default)
        => await CountAsync("SELECT COUNT(1) FROM TravelSuppliers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = supplierId, TenantId = tenantId }, cancellationToken) > 0;

    public async Task<bool> HotelExistsAsync(int tenantId, int hotelId, CancellationToken cancellationToken = default)
        => await CountAsync("SELECT COUNT(1) FROM TravelHotels WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = hotelId, TenantId = tenantId }, cancellationToken) > 0;

    public async Task<bool> GroupStayExistsAsync(int tenantId, int groupId, int groupHotelId, CancellationToken cancellationToken = default)
        => await CountAsync("SELECT COUNT(1) FROM TravelGroupHotels WHERE Id = @Id AND GroupId = @GroupId AND TenantId = @TenantId",
            new { Id = groupHotelId, GroupId = groupId, TenantId = tenantId }, cancellationToken) > 0;

    public async Task<bool> EnquiryBelongsAsync(int tenantId, int enquiryId, int? customerId, CancellationToken cancellationToken = default)
        => await CountAsync("""
            SELECT COUNT(1) FROM TravelEnquiries WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND CustomerId = @CustomerId
            """, new { Id = enquiryId, TenantId = tenantId, CustomerId = customerId }, cancellationToken) > 0;

    public async Task<bool> VisaCaseBelongsAsync(int tenantId, int visaCaseId, int? customerId, int? groupId, CancellationToken cancellationToken = default)
        => await CountAsync("""
            SELECT COUNT(1) FROM TravelVisaCases
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
              AND ((@CustomerId IS NOT NULL AND CustomerId = @CustomerId) OR (@GroupId IS NOT NULL AND GroupId = @GroupId))
            """, new { Id = visaCaseId, TenantId = tenantId, CustomerId = customerId, GroupId = groupId }, cancellationToken) > 0;

    public async Task<IReadOnlyList<(int TravelerId, int? GroupMemberId)>> GetEligibleTravelersAsync(int tenantId, int? customerId, int? groupId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (groupId is int g)
        {
            return (await connection.QueryAsync<(int, int?)>(new CommandDefinition("""
                SELECT m.TravelerId, m.Id FROM TravelGroupMembers m
                WHERE m.GroupId = @GroupId AND m.TenantId = @TenantId AND m.IsDeleted = 0 AND m.Status <> N'Cancelled'
                """, new { GroupId = g, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
        }
        return (await connection.QueryAsync<(int, int?)>(new CommandDefinition("""
            SELECT t.Id, CAST(NULL AS INT) FROM TravelTravelers t
            WHERE t.CustomerId = @CustomerId AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """, new { CustomerId = customerId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<int> CountAsync(string sql, object args, CancellationToken ct)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, args, cancellationToken: ct));
    }

    private static async Task<int> InsertCancellationAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int bookingId, string serviceType,
        int? flightId, int? hotelBookingId, int? passengerId, TravelServiceCancellationWrite c, int? userId, CancellationToken ct)
        => await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelBookingCancellations (TenantId, BookingId, ServiceType, FlightBookingId, HotelBookingId, FlightPassengerId, Reason,
                SupplierPenalty, CustomerPenalty, RefundFromSupplier, RefundToCustomer, CurrencyCode, RefundStatus, CancelledBy)
            SELECT @TenantId, b.Id, @ServiceType, @FlightId, @HotelBookingId, @PassengerId, @Reason,
                @SupplierPenalty, @CustomerPenalty, @RefundFromSupplier, @RefundToCustomer, @CurrencyCode, @RefundStatus, @UserId
            FROM TravelBookings b WHERE b.Id = @BookingId AND b.TenantId = @TenantId;
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
            {
                TenantId = tenantId, BookingId = bookingId, ServiceType = serviceType, FlightId = flightId, HotelBookingId = hotelBookingId,
                PassengerId = passengerId, c.Reason, c.SupplierPenalty, c.CustomerPenalty, c.RefundFromSupplier, c.RefundToCustomer,
                c.CurrencyCode, c.RefundStatus, UserId = userId
            }, tx, cancellationToken: ct));

    private static Task HistoryAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int bookingId, string? serviceType, int? serviceId,
        string eventType, string? from, string? to, string? note, int? userId, CancellationToken ct)
        => connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TravelBookingHistory (TenantId, BookingId, ServiceType, ServiceId, EventType, FromValue, ToValue, Note, ChangedBy)
            SELECT @TenantId, b.Id, @ServiceType, @ServiceId, @EventType, LEFT(@From, 100), LEFT(@To, 100), LEFT(@Note, 1000), @UserId
            FROM TravelBookings b WHERE b.Id = @BookingId AND b.TenantId = @TenantId
            """, new
            {
                TenantId = tenantId, BookingId = bookingId, ServiceType = serviceType, ServiceId = serviceId, EventType = eventType,
                From = from, To = to, Note = note, UserId = userId
            }, tx, cancellationToken: ct));
}
