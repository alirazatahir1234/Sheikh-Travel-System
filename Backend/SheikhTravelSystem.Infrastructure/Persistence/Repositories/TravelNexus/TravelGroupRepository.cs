using System.Data;
using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Groups.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.TravelNexus;

/// <summary>
/// Umrah groups. Root predicate <c>g.TenantId = @TenantId</c>; every join repeats the tenant predicate. Status-dependent
/// writes lock the group row in its expected status and write history in the same transaction.
/// </summary>
public sealed class TravelGroupRepository(IDbConnectionFactory dbFactory) : ITravelGroupRepository
{
    private const string ListSelect = """
        SELECT g.Id, g.GroupNumber, g.Name, g.PackageId, p.Name AS PackageName, p.PackageType, g.DepartureDate, g.ReturnDate, g.Capacity,
               (SELECT COUNT(1) FROM TravelGroupMembers m WHERE m.TenantId = g.TenantId AND m.GroupId = g.Id AND m.IsDeleted = 0 AND m.Status <> N'Cancelled') AS ActiveMembers,
               (SELECT COUNT(1) FROM TravelGroupMembers m WHERE m.TenantId = g.TenantId AND m.GroupId = g.Id AND m.IsDeleted = 0 AND m.Status = N'Confirmed') AS ConfirmedMembers,
               g.Status,
               CASE WHEN lt.Id IS NOT NULL THEN CONCAT(lt.GivenNames, ' ', lt.Surname) ELSE g.LeaderName END AS LeaderDisplayName,
               g.VisaCaseId, g.CreatedAt
        """;

    private const string FromJoins = """
        FROM TravelGroups g
        INNER JOIN TravelPackages p ON p.Id = g.PackageId AND p.TenantId = g.TenantId
        LEFT JOIN TravelTravelers lt ON lt.Id = g.GroupLeaderTravelerId AND lt.TenantId = g.TenantId
        """;

    // ── Reads ───────────────────────────────────────────────────────────────

    public async Task<PagedResult<TravelGroupListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelGroupListFilter f, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var clauses = new List<string> { "g.TenantId = @TenantId", "g.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "g.BranchId", clauses, p);
        if (f.Search is not null) { clauses.Add("(g.GroupNumber LIKE @Search OR g.Name LIKE @Search OR p.Name LIKE @Search)"); p.Add("Search", $"%{TravelSql.EscapeLike(f.Search)}%"); }
        if (f.Status is not null) { clauses.Add("g.Status = @Status"); p.Add("Status", f.Status); }
        if (f.PackageId.HasValue) { clauses.Add("g.PackageId = @PackageId"); p.Add("PackageId", f.PackageId); }
        if (f.DepartureFrom.HasValue) { clauses.Add("g.DepartureDate >= @DepFrom"); p.Add("DepFrom", f.DepartureFrom.Value.Date); }
        if (f.DepartureTo.HasValue) { clauses.Add("g.DepartureDate <= @DepTo"); p.Add("DepTo", f.DepartureTo.Value.Date); }

        var where = string.Join(" AND ", clauses);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(1) {FromJoins} WHERE {where}", p, cancellationToken: cancellationToken));
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);
        var items = (await connection.QueryAsync<TravelGroupListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {FromJoins}
            WHERE {where}
            ORDER BY g.DepartureDate DESC, g.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, p, cancellationToken: cancellationToken))).ToList();
        return new PagedResult<TravelGroupListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<TravelGroupDto?> GetByIdAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Id", id);
        var clauses = new List<string> { "g.TenantId = @TenantId", "g.Id = @Id", "g.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "g.BranchId", clauses, p);
        return await connection.QuerySingleOrDefaultAsync<TravelGroupDto>(new CommandDefinition($"""
            {ListSelect},
                   g.GroupLeaderTravelerId, g.LeaderName, CASE WHEN lt.Id IS NOT NULL THEN lt.Phone ELSE g.LeaderPhone END AS LeaderPhone,
                   v.CaseNumber AS VisaCaseNumber, v.Status AS VisaCaseStatus, g.BranchId, b.Name AS BranchName, g.CancelReason, g.ClosedAt,
                   g.Notes, p.CurrencyCode AS PackageCurrencyCode, p.Status AS PackageStatus
            {FromJoins}
            LEFT JOIN TravelVisaCases v ON v.Id = g.VisaCaseId AND v.TenantId = g.TenantId
            LEFT JOIN Branches b ON b.Id = g.BranchId AND b.TenantId = g.TenantId
            WHERE {string.Join(" AND ", clauses)}
            """, p, cancellationToken: cancellationToken));
    }

    public async Task<TravelGroupState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("Id", id);
        var clauses = new List<string> { "g.TenantId = @TenantId", "g.Id = @Id", "g.IsDeleted = 0" };
        TravelSql.ApplyBranchScope(scope, "g.BranchId", clauses, p);
        return await connection.QuerySingleOrDefaultAsync<TravelGroupState>(new CommandDefinition(
            $"SELECT g.Id, g.Status, g.PackageId, g.Capacity, g.DepartureDate, g.ReturnDate, g.VisaCaseId, g.BranchId, g.Name FROM TravelGroups g WHERE {string.Join(" AND ", clauses)}",
            p, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        var scopeClauses = new List<string> { "1 = 1" };
        TravelSql.ApplyBranchScope(scope, "g.BranchId", scopeClauses, p);
        var rows = await connection.QueryAsync<(string Status, int Total)>(new CommandDefinition($"""
            SELECT g.Status, COUNT(1) AS Total FROM TravelGroups g
            WHERE g.TenantId = @TenantId AND g.IsDeleted = 0 AND {string.Join(" AND ", scopeClauses)}
            GROUP BY g.Status
            """, p, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Status, r => r.Total);
    }

    public async Task<IReadOnlyList<TravelGroupListItemDto>> GetByCustomerAsync(int customerId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelGroupListItemDto>(new CommandDefinition($"""
            {ListSelect}
            {FromJoins}
            WHERE g.TenantId = @TenantId AND g.IsDeleted = 0
              AND EXISTS (SELECT 1 FROM TravelGroupMembers m WHERE m.TenantId = @TenantId AND m.GroupId = g.Id AND m.CustomerId = @CustomerId AND m.IsDeleted = 0)
            ORDER BY g.DepartureDate DESC
            """, new { TenantId = tenantId, CustomerId = customerId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelGroupMemberDto>> GetMembersAsync(int groupId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelGroupMemberDto>(new CommandDefinition("""
            SELECT m.Id, m.TravelerId, CONCAT(t.GivenNames, ' ', t.Surname) AS TravelerName, t.Gender, t.DateOfBirth, m.CustomerId,
                   c.FullName AS CustomerName, t.MahramTravelerId, t.PassportNumber, t.PassportExpiryDate, m.RoomPreference, m.PriceCategory,
                   m.QuotedPrice, m.CurrencyCode, m.Status, m.CancelReason, a.Decision AS VisaDecision, a.VisaNumber, a.VisaExpiryDate
            FROM TravelGroupMembers m
            INNER JOIN TravelGroups g ON g.Id = m.GroupId AND g.TenantId = m.TenantId
            INNER JOIN TravelTravelers t ON t.Id = m.TravelerId AND t.TenantId = m.TenantId
            INNER JOIN Customers c ON c.Id = m.CustomerId AND c.TenantId = m.TenantId
            LEFT JOIN TravelVisaApplicants a ON a.VisaCaseId = g.VisaCaseId AND a.TravelerId = m.TravelerId AND a.TenantId = m.TenantId AND a.IsDeleted = 0
            WHERE m.GroupId = @GroupId AND m.TenantId = @TenantId AND m.IsDeleted = 0
            ORDER BY CASE m.Status WHEN N'Cancelled' THEN 1 ELSE 0 END, c.FullName, t.Surname, t.GivenNames
            """, new { GroupId = groupId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelGroupMemberFacts>> GetMemberFactsAsync(int groupId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelGroupMemberFacts>(new CommandDefinition("""
            SELECT m.Id AS MemberId, m.TravelerId, m.CustomerId, t.Gender, m.PriceCategory, m.RoomPreference, t.MahramTravelerId, m.Status
            FROM TravelGroupMembers m
            INNER JOIN TravelTravelers t ON t.Id = m.TravelerId AND t.TenantId = m.TenantId
            WHERE m.GroupId = @GroupId AND m.TenantId = @TenantId AND m.IsDeleted = 0
            """, new { GroupId = groupId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<Application.Features.TravelNexus.Bookings.DTOs.TravelGroupTicketFact>> GetTicketFactsAsync(int groupId, int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<Application.Features.TravelNexus.Bookings.DTOs.TravelGroupTicketFact>(new CommandDefinition("""
            SELECT DISTINCT fp.TravelerId, f.Direction
            FROM TravelBookings b
            INNER JOIN TravelFlightBookings f ON f.BookingId = b.Id AND f.TenantId = b.TenantId AND f.IsDeleted = 0 AND f.Status = N'Ticketed'
            INNER JOIN TravelFlightPassengers fp ON fp.FlightBookingId = f.Id AND fp.TenantId = b.TenantId AND fp.TicketStatus = N'Issued'
            WHERE b.GroupId = @GroupId AND b.TenantId = @TenantId AND b.IsDeleted = 0 AND b.Status <> N'Cancelled'
            """, new { GroupId = groupId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<int>> GetActiveMemberTravelerIdsAsync(int groupId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT TravelerId FROM TravelGroupMembers
            WHERE GroupId = @GroupId AND TenantId = @TenantId AND IsDeleted = 0 AND Status <> N'Cancelled'
            """, new { GroupId = groupId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelGroupStayDto>> GetStaysAsync(int groupId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelGroupStayDto>(new CommandDefinition("""
            SELECT s.Id, s.HotelId, h.Name AS HotelName, h.City, s.CheckIn, s.CheckOut, s.Sequence, s.ConfirmationNumber,
                   (SELECT COUNT(1) FROM TravelGroupRooms r WHERE r.TenantId = s.TenantId AND r.GroupHotelId = s.Id AND r.IsDeleted = 0) AS RoomCount,
                   (SELECT COUNT(1) FROM TravelRoomAssignments ra WHERE ra.TenantId = s.TenantId AND ra.GroupHotelId = s.Id) AS AssignedCount
            FROM TravelGroupHotels s
            INNER JOIN TravelHotels h ON h.Id = s.HotelId AND h.TenantId = s.TenantId
            WHERE s.GroupId = @GroupId AND s.TenantId = @TenantId AND s.IsDeleted = 0
            ORDER BY s.CheckIn, s.Sequence
            """, new { GroupId = groupId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    private sealed record RoomRow(int Id, int GroupHotelId, string? RoomNumber, string RoomType, int Capacity, string GenderPolicy);

    public async Task<IReadOnlyList<TravelGroupRoomDto>> GetRoomsAsync(int groupId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rooms = await connection.QueryAsync<RoomRow>(new CommandDefinition("""
            SELECT r.Id, r.GroupHotelId, r.RoomNumber, r.RoomType, r.Capacity, r.GenderPolicy
            FROM TravelGroupRooms r
            INNER JOIN TravelGroupHotels s ON s.Id = r.GroupHotelId AND s.TenantId = r.TenantId AND s.IsDeleted = 0
            WHERE s.GroupId = @GroupId AND r.TenantId = @TenantId AND r.IsDeleted = 0
            ORDER BY r.GroupHotelId, r.Id
            """, new { GroupId = groupId, TenantId = tenantId }, cancellationToken: cancellationToken));
        var assignments = (await connection.QueryAsync<(int RoomId, int MemberId)>(new CommandDefinition("""
            SELECT ra.RoomId, ra.MemberId
            FROM TravelRoomAssignments ra
            INNER JOIN TravelGroupHotels s ON s.Id = ra.GroupHotelId AND s.TenantId = ra.TenantId AND s.IsDeleted = 0
            WHERE s.GroupId = @GroupId AND ra.TenantId = @TenantId
            """, new { GroupId = groupId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToLookup(a => a.RoomId, a => a.MemberId);

        return rooms.Select(r => new TravelGroupRoomDto
        {
            Id = r.Id, GroupHotelId = r.GroupHotelId, RoomNumber = r.RoomNumber, RoomType = r.RoomType, Capacity = r.Capacity,
            GenderPolicy = r.GenderPolicy, MemberIds = assignments[r.Id].ToList()
        }).ToList();
    }

    public async Task<IReadOnlyList<TravelGroupTransportDto>> GetTransportAsync(int groupId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelGroupTransportDto>(new CommandDefinition("""
            SELECT t.Id, t.LegType, t.DepartAt, t.FromPlace, t.ToPlace, t.ProviderName, t.VehicleType, t.Seats, t.FleetBookingId,
                   b.BookingNumber AS FleetBookingNumber, t.Notes
            FROM TravelGroupTransport t
            LEFT JOIN Bookings b ON b.Id = t.FleetBookingId AND b.TenantId = t.TenantId
            WHERE t.GroupId = @GroupId AND t.TenantId = @TenantId AND t.IsDeleted = 0
            ORDER BY t.DepartAt
            """, new { GroupId = groupId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<TravelGroupHistoryDto>> GetHistoryAsync(int groupId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<TravelGroupHistoryDto>(new CommandDefinition("""
            SELECT h.Id, h.EventType, h.MemberId, CONCAT(t.GivenNames, ' ', t.Surname) AS MemberName, h.FromValue, h.ToValue, h.Note,
                   u.FullName AS ChangedByName, h.ChangedAt
            FROM TravelGroupStatusHistory h
            LEFT JOIN TravelGroupMembers m ON m.Id = h.MemberId AND m.TenantId = h.TenantId
            LEFT JOIN TravelTravelers t ON t.Id = m.TravelerId AND t.TenantId = h.TenantId
            LEFT JOIN Users u ON u.Id = h.ChangedBy AND u.TenantId = h.TenantId
            WHERE h.GroupId = @GroupId AND h.TenantId = @TenantId
            ORDER BY h.ChangedAt DESC, h.Id DESC
            """, new { GroupId = groupId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<int>> FindOverlappingTravelersAsync(int tenantId, IReadOnlyList<int> travelerIds, DateTime departure, DateTime returnDate,
        int excludeGroupId, CancellationToken cancellationToken = default)
    {
        if (travelerIds.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT DISTINCT m.TravelerId
            FROM TravelGroupMembers m
            INNER JOIN TravelGroups g ON g.Id = m.GroupId AND g.TenantId = m.TenantId
            WHERE m.TenantId = @TenantId AND m.TravelerId IN @Ids AND m.IsDeleted = 0 AND m.Status <> N'Cancelled'
              AND g.IsDeleted = 0 AND g.Status NOT IN (N'Cancelled', N'Closed') AND g.Id <> @Exclude
              AND g.DepartureDate <= @ReturnDate AND @Departure <= g.ReturnDate
            """, new { TenantId = tenantId, Ids = travelerIds.ToArray(), Exclude = excludeGroupId, Departure = departure.Date, ReturnDate = returnDate.Date },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<bool> FleetBookingExistsAsync(int tenantId, int bookingId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Bookings WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = bookingId, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    // ── Writes ──────────────────────────────────────────────────────────────

    public async Task<int> CreateAsync(int tenantId, CreateTravelGroupDto dto, DateTime returnDate, IReadOnlyList<TravelGroupStayPlan> stays, int? userId,
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var number = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.Group, nowUtc, cancellationToken);
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelGroups (TenantId, GroupNumber, PackageId, Name, DepartureDate, ReturnDate, Capacity, Status, BranchId, Notes, CreatedBy)
            SELECT @TenantId, @Number, p.Id, @Name, @Departure, @ReturnDate, @Capacity, N'Draft', @BranchId, @Notes, @UserId
            FROM TravelPackages p WHERE p.Id = @PackageId AND p.TenantId = @TenantId AND p.IsDeleted = 0;
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
            {
                TenantId = tenantId, Number = number, dto.PackageId, dto.Name, Departure = dto.DepartureDate.Date, ReturnDate = returnDate.Date,
                dto.Capacity, dto.BranchId, dto.Notes, UserId = userId
            }, tx, cancellationToken: cancellationToken));

        foreach (var s in stays)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelGroupHotels (TenantId, GroupId, HotelId, CheckIn, CheckOut, Sequence)
                SELECT @TenantId, @GroupId, h.Id, @CheckIn, @CheckOut, @Sequence FROM TravelHotels h WHERE h.Id = @HotelId AND h.TenantId = @TenantId
                """, new { TenantId = tenantId, GroupId = id, s.HotelId, s.CheckIn, s.CheckOut, s.Sequence }, tx, cancellationToken: cancellationToken));
        }
        await HistoryAsync(connection, tx, tenantId, id, null, "GroupStatus", null, "Draft", "Group created", userId, cancellationToken);
        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(int id, int tenantId, UpdateTravelGroupDto dto, IReadOnlyList<string> editableStatuses, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var previousLeader = await connection.QuerySingleOrDefaultAsync<(int Id, int? Leader)?>(new CommandDefinition("""
            SELECT Id, GroupLeaderTravelerId FROM TravelGroups WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status IN @Statuses
            """, new { Id = id, TenantId = tenantId, Statuses = editableStatuses.ToArray() }, tx, cancellationToken: cancellationToken));
        if (previousLeader is null)
        {
            tx.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelGroups SET Name = @Name, DepartureDate = @Departure, ReturnDate = @ReturnDate, Capacity = @Capacity,
                GroupLeaderTravelerId = @Leader, LeaderName = @LeaderName, LeaderPhone = @LeaderPhone, Notes = @Notes, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new
            {
                Id = id, TenantId = tenantId, dto.Name, Departure = dto.DepartureDate.Date, ReturnDate = dto.ReturnDate.Date, dto.Capacity,
                Leader = dto.GroupLeaderTravelerId,
                LeaderName = dto.GroupLeaderTravelerId is null && !string.IsNullOrWhiteSpace(dto.LeaderName) ? dto.LeaderName.Trim() : null,
                LeaderPhone = dto.GroupLeaderTravelerId is null && !string.IsNullOrWhiteSpace(dto.LeaderPhone) ? dto.LeaderPhone.Trim() : null,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
            }, tx, cancellationToken: cancellationToken));

        if (previousLeader.Value.Leader != dto.GroupLeaderTravelerId)
            await HistoryAsync(connection, tx, tenantId, id, null, "LeaderChanged", previousLeader.Value.Leader?.ToString(),
                dto.GroupLeaderTravelerId?.ToString(), dto.GroupLeaderTravelerId is null ? dto.LeaderName : null, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<bool> ChangeStatusAsync(int tenantId, TravelGroupStatusChange change, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelGroups SET Status = @NewStatus,
                CancelReason = COALESCE(@CancelReason, CancelReason),
                ClosedAt = CASE WHEN @NewStatus IN (N'Closed', N'Cancelled') THEN SYSUTCDATETIME() ELSE ClosedAt END,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected
            """, new { Id = change.GroupId, TenantId = tenantId, Expected = change.ExpectedStatus, change.NewStatus, change.CancelReason },
            tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await HistoryAsync(connection, tx, tenantId, change.GroupId, null, "GroupStatus", change.ExpectedStatus, change.NewStatus, change.Note, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<int?> StartVisaProcessingAsync(int tenantId, int groupId, string expectedStatus, int visaTypeId, string countryCode,
        DateTime travelDate, int? userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var group = await connection.QuerySingleOrDefaultAsync<(int Id, int? BranchId, string Name, string GroupNumber)?>(new CommandDefinition("""
            SELECT Id, BranchId, Name, GroupNumber FROM TravelGroups WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = @Expected AND VisaCaseId IS NULL
            """, new { Id = groupId, TenantId = tenantId, Expected = expectedStatus }, tx, cancellationToken: cancellationToken));
        if (group is null)
        {
            tx.Rollback();
            return null;
        }

        var caseNumber = await TravelNumberAllocator.NextAsync(connection, tx, tenantId, TravelNumberAllocator.Kind.VisaCase, nowUtc, cancellationToken);
        var caseId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO TravelVisaCases (TenantId, CaseNumber, CustomerId, GroupId, CountryCode, VisaTypeId, IntendedTravelDate, Status, Priority,
                BranchId, Notes, CreatedBy)
            VALUES (@TenantId, @Number, NULL, @GroupId, @CountryCode, @VisaTypeId, @TravelDate, N'Draft', N'Normal', @BranchId, @Notes, @UserId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, new
            {
                TenantId = tenantId, Number = caseNumber, GroupId = groupId, CountryCode = countryCode, VisaTypeId = visaTypeId,
                TravelDate = travelDate.Date, group.Value.BranchId, Notes = $"Group visa case for {group.Value.GroupNumber} {group.Value.Name}", UserId = userId
            }, tx, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TravelVisaStatusHistory (TenantId, VisaCaseId, EventType, FromValue, ToValue, Note, ChangedBy)
            VALUES (@TenantId, @CaseId, N'CaseStatus', NULL, N'Draft', @Note, @UserId)
            """, new { TenantId = tenantId, CaseId = caseId, Note = $"Opened for group {group.Value.GroupNumber}", UserId = userId }, tx, cancellationToken: cancellationToken));

        var applicants = (await connection.QueryAsync<int>(new CommandDefinition("""
            INSERT INTO TravelVisaApplicants (TenantId, VisaCaseId, TravelerId, CreatedBy)
            OUTPUT inserted.Id
            SELECT @TenantId, @CaseId, m.TravelerId, @UserId
            FROM TravelGroupMembers m
            WHERE m.TenantId = @TenantId AND m.GroupId = @GroupId AND m.IsDeleted = 0 AND m.Status = N'Confirmed'
            """, new { TenantId = tenantId, CaseId = caseId, GroupId = groupId, UserId = userId }, tx, cancellationToken: cancellationToken))).ToList();
        foreach (var applicantId in applicants)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TravelVisaStatusHistory (TenantId, VisaCaseId, ApplicantId, EventType, FromValue, ToValue, ChangedBy)
                VALUES (@TenantId, @CaseId, @ApplicantId, N'ApplicantAdded', NULL, N'Pending', @UserId)
                """, new { TenantId = tenantId, CaseId = caseId, ApplicantId = applicantId, UserId = userId }, tx, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelGroups SET Status = N'VisaProcessing', VisaCaseId = @CaseId, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = groupId, TenantId = tenantId, CaseId = caseId }, tx, cancellationToken: cancellationToken));
        await HistoryAsync(connection, tx, tenantId, groupId, null, "GroupStatus", expectedStatus, "VisaProcessing",
            $"Visa case {caseNumber} opened with {applicants.Count} applicant(s)", userId, cancellationToken);

        tx.Commit();
        return caseId;
    }

    public async Task<int?> RegisterMembersAsync(int tenantId, int groupId, IReadOnlyList<TravelGroupNewMember> members, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var capacity = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT Capacity FROM TravelGroups WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Open'
            """, new { Id = groupId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        var active = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TravelGroupMembers WHERE GroupId = @Id AND TenantId = @TenantId AND IsDeleted = 0 AND Status <> N'Cancelled'
            """, new { Id = groupId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (capacity is null || active + members.Count > capacity)
        {
            tx.Rollback();
            return null;
        }

        foreach (var m in members)
        {
            var memberId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO TravelGroupMembers (TenantId, GroupId, TravelerId, CustomerId, RoomPreference, PriceCategory, QuotedPrice, CurrencyCode, CreatedBy)
                SELECT @TenantId, @GroupId, t.Id, t.CustomerId, @RoomPreference, @PriceCategory, @QuotedPrice, @CurrencyCode, @UserId
                FROM TravelTravelers t WHERE t.Id = @TravelerId AND t.CustomerId = @CustomerId AND t.TenantId = @TenantId AND t.IsDeleted = 0;
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
                {
                    TenantId = tenantId, GroupId = groupId, m.TravelerId, m.CustomerId, m.RoomPreference, m.PriceCategory, m.QuotedPrice,
                    m.CurrencyCode, UserId = userId
                }, tx, cancellationToken: cancellationToken));
            await HistoryAsync(connection, tx, tenantId, groupId, memberId, "MemberRegistered", null, "Registered", null, userId, cancellationToken);
        }
        tx.Commit();
        return members.Count;
    }

    public async Task<bool> ConfirmMemberAsync(int tenantId, int groupId, int memberId, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelGroupMembers SET Status = N'Confirmed', UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @MemberId AND GroupId = @GroupId AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Registered'
            """, new { MemberId = memberId, GroupId = groupId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await HistoryAsync(connection, tx, tenantId, groupId, memberId, "MemberConfirmed", "Registered", "Confirmed", null, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<bool> CancelMemberAsync(int tenantId, int groupId, int memberId, string reason, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var previous = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT Status FROM TravelGroupMembers WITH (UPDLOCK, ROWLOCK)
            WHERE Id = @MemberId AND GroupId = @GroupId AND TenantId = @TenantId AND IsDeleted = 0 AND Status <> N'Cancelled'
            """, new { MemberId = memberId, GroupId = groupId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (previous is null)
        {
            tx.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelGroupMembers SET Status = N'Cancelled', CancelReason = @Reason, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @MemberId AND TenantId = @TenantId;
            DELETE FROM TravelRoomAssignments WHERE MemberId = @MemberId AND TenantId = @TenantId;
            UPDATE TravelGroups SET GroupLeaderTravelerId = NULL, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @GroupId AND TenantId = @TenantId
              AND GroupLeaderTravelerId = (SELECT TravelerId FROM TravelGroupMembers WHERE Id = @MemberId AND TenantId = @TenantId);
            """, new { MemberId = memberId, GroupId = groupId, TenantId = tenantId, Reason = reason }, tx, cancellationToken: cancellationToken));
        await HistoryAsync(connection, tx, tenantId, groupId, memberId, "MemberCancelled", previous, "Cancelled", reason, userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<bool> UpdateMemberAsync(int tenantId, int groupId, int memberId, UpdateTravelGroupMemberDto dto, string? currencyCode,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelGroupMembers SET RoomPreference = @RoomPreference, PriceCategory = @PriceCategory, QuotedPrice = @QuotedPrice,
                CurrencyCode = CASE WHEN @QuotedPrice IS NULL THEN NULL ELSE COALESCE(CurrencyCode, @Currency) END, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @MemberId AND GroupId = @GroupId AND TenantId = @TenantId AND IsDeleted = 0 AND Status <> N'Cancelled'
            """, new { MemberId = memberId, GroupId = groupId, TenantId = tenantId, dto.RoomPreference, dto.PriceCategory, dto.QuotedPrice, Currency = currencyCode },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<int?> SaveStayAsync(int tenantId, int groupId, int? stayId, SaveTravelGroupStayDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new { TenantId = tenantId, GroupId = groupId, StayId = stayId, dto.HotelId, CheckIn = dto.CheckIn.Date, CheckOut = dto.CheckOut.Date,
            Confirmation = string.IsNullOrWhiteSpace(dto.ConfirmationNumber) ? null : dto.ConfirmationNumber.Trim() };
        if (stayId is null)
        {
            return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                INSERT INTO TravelGroupHotels (TenantId, GroupId, HotelId, CheckIn, CheckOut, Sequence, ConfirmationNumber)
                SELECT @TenantId, g.Id, h.Id, @CheckIn, @CheckOut,
                       ISNULL((SELECT MAX(Sequence) FROM TravelGroupHotels x WHERE x.TenantId = @TenantId AND x.GroupId = g.Id AND x.IsDeleted = 0), 0) + 1,
                       @Confirmation
                FROM TravelGroups g
                INNER JOIN TravelHotels h ON h.Id = @HotelId AND h.TenantId = @TenantId
                WHERE g.Id = @GroupId AND g.TenantId = @TenantId AND g.IsDeleted = 0;
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, p, cancellationToken: cancellationToken));
        }
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelGroupHotels SET HotelId = @HotelId, CheckIn = @CheckIn, CheckOut = @CheckOut, ConfirmationNumber = @Confirmation
            WHERE Id = @StayId AND GroupId = @GroupId AND TenantId = @TenantId AND IsDeleted = 0
              AND EXISTS (SELECT 1 FROM TravelHotels h WHERE h.Id = @HotelId AND h.TenantId = @TenantId)
            """, p, cancellationToken: cancellationToken));
        return rows > 0 ? stayId : null;
    }

    public async Task<bool> DeleteStayAsync(int tenantId, int groupId, int stayId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelGroupHotels SET IsDeleted = 1 WHERE Id = @StayId AND GroupId = @GroupId AND TenantId = @TenantId AND IsDeleted = 0
            """, new { StayId = stayId, GroupId = groupId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (rows == 0)
        {
            tx.Rollback();
            return false;
        }
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM TravelRoomAssignments WHERE GroupHotelId = @StayId AND TenantId = @TenantId;
            UPDATE TravelGroupRooms SET IsDeleted = 1 WHERE GroupHotelId = @StayId AND TenantId = @TenantId;
            """, new { StayId = stayId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        tx.Commit();
        return true;
    }

    public async Task<bool> SaveRoomingAsync(int tenantId, int groupId, int stayId, IReadOnlyList<TravelRoomingRoomDto> rooms, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        var hotelName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            SELECT h.Name FROM TravelGroupHotels s WITH (UPDLOCK, ROWLOCK)
            INNER JOIN TravelHotels h ON h.Id = s.HotelId AND h.TenantId = s.TenantId
            WHERE s.Id = @StayId AND s.GroupId = @GroupId AND s.TenantId = @TenantId AND s.IsDeleted = 0
            """, new { StayId = stayId, GroupId = groupId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (hotelName is null)
        {
            tx.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM TravelRoomAssignments WHERE GroupHotelId = @StayId AND TenantId = @TenantId;
            UPDATE TravelGroupRooms SET IsDeleted = 1 WHERE GroupHotelId = @StayId AND TenantId = @TenantId AND IsDeleted = 0;
            """, new { StayId = stayId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));

        foreach (var room in rooms)
        {
            var roomId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO TravelGroupRooms (TenantId, GroupHotelId, RoomNumber, RoomType, Capacity, GenderPolicy)
                VALUES (@TenantId, @StayId, @RoomNumber, @RoomType, @Capacity, @GenderPolicy);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, new
                {
                    TenantId = tenantId, StayId = stayId, RoomNumber = string.IsNullOrWhiteSpace(room.RoomNumber) ? null : room.RoomNumber.Trim(),
                    room.RoomType, Capacity = TravelValues.RoomCapacity(room.RoomType), room.GenderPolicy
                }, tx, cancellationToken: cancellationToken));
            foreach (var memberId in room.MemberIds)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO TravelRoomAssignments (TenantId, GroupHotelId, RoomId, MemberId, CreatedBy)
                    SELECT @TenantId, @StayId, @RoomId, m.Id, @UserId
                    FROM TravelGroupMembers m
                    WHERE m.Id = @MemberId AND m.GroupId = @GroupId AND m.TenantId = @TenantId AND m.IsDeleted = 0 AND m.Status <> N'Cancelled'
                    """, new { TenantId = tenantId, StayId = stayId, RoomId = roomId, MemberId = memberId, GroupId = groupId, UserId = userId },
                    tx, cancellationToken: cancellationToken));
            }
        }

        await HistoryAsync(connection, tx, tenantId, groupId, null, "RoomAssigned", null, null,
            $"{hotelName}: {rooms.Count} room(s), {rooms.Sum(r => r.MemberIds.Count)} pilgrim(s)", userId, cancellationToken);
        tx.Commit();
        return true;
    }

    public async Task<int?> SaveTransportAsync(int tenantId, int groupId, int? legId, SaveTravelTransportLegDto dto, int? userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var p = new
        {
            TenantId = tenantId, GroupId = groupId, LegId = legId, dto.LegType, dto.DepartAt, FromPlace = dto.FromPlace.Trim(), ToPlace = dto.ToPlace.Trim(),
            ProviderName = string.IsNullOrWhiteSpace(dto.ProviderName) ? null : dto.ProviderName.Trim(),
            VehicleType = string.IsNullOrWhiteSpace(dto.VehicleType) ? null : dto.VehicleType.Trim(),
            dto.Seats, dto.FleetBookingId, Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(), UserId = userId
        };
        if (legId is null)
        {
            return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                INSERT INTO TravelGroupTransport (TenantId, GroupId, LegType, DepartAt, FromPlace, ToPlace, ProviderName, VehicleType, Seats,
                    FleetBookingId, Notes, CreatedBy)
                SELECT @TenantId, g.Id, @LegType, @DepartAt, @FromPlace, @ToPlace, @ProviderName, @VehicleType, @Seats, @FleetBookingId, @Notes, @UserId
                FROM TravelGroups g WHERE g.Id = @GroupId AND g.TenantId = @TenantId AND g.IsDeleted = 0;
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """, p, cancellationToken: cancellationToken));
        }
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelGroupTransport SET LegType = @LegType, DepartAt = @DepartAt, FromPlace = @FromPlace, ToPlace = @ToPlace,
                ProviderName = @ProviderName, VehicleType = @VehicleType, Seats = @Seats, FleetBookingId = @FleetBookingId, Notes = @Notes
            WHERE Id = @LegId AND GroupId = @GroupId AND TenantId = @TenantId AND IsDeleted = 0
            """, p, cancellationToken: cancellationToken));
        return rows > 0 ? legId : null;
    }

    public async Task<bool> DeleteTransportAsync(int tenantId, int groupId, int legId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TravelGroupTransport SET IsDeleted = 1 WHERE Id = @LegId AND GroupId = @GroupId AND TenantId = @TenantId AND IsDeleted = 0
            """, new { LegId = legId, GroupId = groupId, TenantId = tenantId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task AddHistoryAsync(int tenantId, int groupId, int? memberId, string eventType, string? fromValue, string? toValue, string? note, int? userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();
        await HistoryAsync(connection, tx, tenantId, groupId, memberId, eventType, fromValue, toValue, note, userId, cancellationToken);
        tx.Commit();
    }

    private static Task HistoryAsync(IDbConnection connection, IDbTransaction tx, int tenantId, int groupId, int? memberId, string eventType,
        string? from, string? to, string? note, int? userId, CancellationToken ct)
        => connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TravelGroupStatusHistory (TenantId, GroupId, MemberId, EventType, FromValue, ToValue, Note, ChangedBy)
            SELECT @TenantId, g.Id, @MemberId, @EventType, @From, @To, LEFT(@Note, 1000), @UserId
            FROM TravelGroups g WHERE g.Id = @GroupId AND g.TenantId = @TenantId
            """, new { TenantId = tenantId, GroupId = groupId, MemberId = memberId, EventType = eventType, From = from, To = to, Note = note, UserId = userId },
            tx, cancellationToken: ct));
}
