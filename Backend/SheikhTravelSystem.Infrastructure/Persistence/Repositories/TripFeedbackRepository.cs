using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class TripFeedbackRepository(IDbConnectionFactory dbFactory) : ITripFeedbackRepository
{
    public async Task<long> InsertFeedbackLinkAsync(
        int tenantId, int tripId, byte[] tokenHash, string tokenProtected, DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TripFeedbackLinks SET UsedAt = SYSUTCDATETIME()
            WHERE TripId = @TripId AND UsedAt IS NULL
            """, new { TripId = tripId }, cancellationToken: cancellationToken));

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO TripFeedbackLinks (TenantId, TripId, TokenHash, TokenProtected, ExpiresAt)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, @TripId, @TokenHash, @TokenProtected, @ExpiresAt)
            """, new { TenantId = tenantId, TripId = tripId, TokenHash = tokenHash, TokenProtected = tokenProtected, ExpiresAt = expiresAt },
            cancellationToken: cancellationToken));
    }

    public async Task<TripFeedbackInviteRow?> GetFeedbackLinkByHashAsync(
        byte[] tokenHash, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TripFeedbackInviteRow>(new CommandDefinition("""
            SELECT Id, TenantId, TripId, TokenHash, TokenProtected, ExpiresAt, UsedAt
            FROM TripFeedbackLinks
            WHERE TokenHash = @TokenHash
            """, new { TokenHash = tokenHash }, cancellationToken: cancellationToken));
    }

    public async Task MarkFeedbackLinkUsedAsync(long linkId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE TripFeedbackLinks SET UsedAt = SYSUTCDATETIME() WHERE Id = @Id AND UsedAt IS NULL
            """, new { Id = linkId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> HasRatingForTripAsync(int tenantId, int tripId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM TripRatings WHERE TenantId = @TenantId AND TripId = @TripId
            """, new { TenantId = tenantId, TripId = tripId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> TryInsertDetailedRatingAsync(
        int tenantId, int tripId, int? driverId, SubmitTripFeedbackPayload body, string source,
        CancellationToken cancellationToken = default)
    {
        var score = (byte)Math.Round((body.Politeness + body.OnTimeDelivery + body.VehicleCondition) / 3m);
        using var connection = dbFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TripRatings (
                    TenantId, TripId, DriverId, Score, Politeness, OnTimeDelivery, VehicleCondition,
                    Comment, Source, IsHidden)
                VALUES (
                    @TenantId, @TripId, @DriverId, @Score, @Politeness, @OnTimeDelivery, @VehicleCondition,
                    @Comment, @Source, 0)
                """, new
            {
                TenantId = tenantId,
                TripId = tripId,
                DriverId = driverId,
                Score = score,
                Politeness = (byte)body.Politeness,
                OnTimeDelivery = (byte)body.OnTimeDelivery,
                VehicleCondition = (byte)body.VehicleCondition,
                Comment = string.IsNullOrWhiteSpace(body.Comment) ? null : body.Comment.Trim(),
                Source = source
            }, cancellationToken: cancellationToken));
            return true;
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public async Task RefreshDriverAggregateRatingAsync(int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE d SET
                Rating = agg.AvgStars,
                UpdatedAt = GETUTCDATE()
            FROM Drivers d
            OUTER APPLY (
                SELECT CAST(ROUND(AVG(
                    (CAST(COALESCE(r.Politeness, r.Score) AS DECIMAL(5,2))
                   + CAST(COALESCE(r.OnTimeDelivery, r.Score) AS DECIMAL(5,2))
                   + CAST(COALESCE(r.VehicleCondition, r.Score) AS DECIMAL(5,2))) / 3.0), 2) AS DECIMAL(4,2)) AS AvgStars
                FROM TripRatings r
                WHERE r.TenantId = d.TenantId AND r.DriverId = d.Id AND r.IsHidden = 0
            ) agg
            WHERE d.Id = @DriverId AND d.TenantId = @TenantId AND d.IsDeleted = 0
            """, new { DriverId = driverId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<(IReadOnlyList<TripClientRatingListRow> Items, int Total)> ListRatingsAsync(
        int tenantId, int page, int pageSize, int? driverId, bool includeHidden,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        using var connection = dbFactory.CreateConnection();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1)
            FROM TripRatings r
            WHERE r.TenantId = @TenantId
              AND (@DriverId IS NULL OR r.DriverId = @DriverId)
              AND (@IncludeHidden = 1 OR r.IsHidden = 0)
            """, new { TenantId = tenantId, DriverId = driverId, IncludeHidden = includeHidden ? 1 : 0 },
            cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<TripClientRatingListRow>(new CommandDefinition("""
            SELECT
                r.Id, r.TripId, t.TripNumber, r.DriverId, d.FullName AS DriverName,
                r.Score, r.Politeness, r.OnTimeDelivery, r.VehicleCondition,
                r.Comment, r.Source, r.IsHidden, r.CreatedAt
            FROM TripRatings r
            LEFT JOIN Trips t ON t.Id = r.TripId AND t.TenantId = r.TenantId
            LEFT JOIN Drivers d ON d.Id = r.DriverId AND d.TenantId = r.TenantId
            WHERE r.TenantId = @TenantId
              AND (@DriverId IS NULL OR r.DriverId = @DriverId)
              AND (@IncludeHidden = 1 OR r.IsHidden = 0)
            ORDER BY r.CreatedAt DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
            """, new
        {
            TenantId = tenantId,
            DriverId = driverId,
            IncludeHidden = includeHidden ? 1 : 0,
            Skip = (page - 1) * pageSize,
            Take = pageSize
        }, cancellationToken: cancellationToken))).ToList();

        return (items, total);
    }

    public async Task SetRatingHiddenAsync(int tenantId, int ratingId, bool hidden, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var driverId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            UPDATE TripRatings SET IsHidden = @Hidden
            OUTPUT INSERTED.DriverId
            WHERE Id = @Id AND TenantId = @TenantId
            """, new { Id = ratingId, TenantId = tenantId, Hidden = hidden }, cancellationToken: cancellationToken));
        if (driverId is > 0)
            await RefreshDriverAggregateRatingAsync(tenantId, driverId.Value, cancellationToken);
    }

    public async Task SetDriverPublicOptOutAsync(int tenantId, int driverId, bool optOut, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Drivers SET PublicRatingOptOut = @OptOut, UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
            """, new { Id = driverId, TenantId = tenantId, OptOut = optOut }, cancellationToken: cancellationToken));
    }

    public async Task<DriverClientRatingSummary?> GetDriverSummaryAsync(
        int tenantId, int driverId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DriverClientRatingSummary>(new CommandDefinition("""
            SELECT
                CAST(ROUND(AVG(
                    (CAST(COALESCE(r.Politeness, r.Score) AS DECIMAL(5,2))
                   + CAST(COALESCE(r.OnTimeDelivery, r.Score) AS DECIMAL(5,2))
                   + CAST(COALESCE(r.VehicleCondition, r.Score) AS DECIMAL(5,2))) / 3.0), 2) AS DECIMAL(4,2)) AS AverageRating,
                COUNT(1) AS ReviewCount,
                CAST(ROUND(AVG(CAST(COALESCE(r.Politeness, r.Score) AS DECIMAL(5,2))), 2) AS DECIMAL(4,2)) AS AvgPoliteness,
                CAST(ROUND(AVG(CAST(COALESCE(r.OnTimeDelivery, r.Score) AS DECIMAL(5,2))), 2) AS DECIMAL(4,2)) AS AvgOnTime,
                CAST(ROUND(AVG(CAST(COALESCE(r.VehicleCondition, r.Score) AS DECIMAL(5,2))), 2) AS DECIMAL(4,2)) AS AvgVehicle
            FROM TripRatings r
            WHERE r.TenantId = @TenantId AND r.DriverId = @DriverId AND r.IsHidden = 0
            """, new { TenantId = tenantId, DriverId = driverId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PublicDriverRatingRow>> ListPublicDriverRatingsAsync(
        int tenantId, int take, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 50);
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PublicDriverRatingRow>(new CommandDefinition("""
            SELECT TOP (@Take)
                d.Id AS DriverId,
                d.FullName AS DisplayName,
                CAST(ROUND(AVG(
                    (CAST(COALESCE(r.Politeness, r.Score) AS DECIMAL(5,2))
                   + CAST(COALESCE(r.OnTimeDelivery, r.Score) AS DECIMAL(5,2))
                   + CAST(COALESCE(r.VehicleCondition, r.Score) AS DECIMAL(5,2))) / 3.0), 2) AS DECIMAL(4,2)) AS AverageRating,
                COUNT(r.Id) AS ReviewCount,
                CAST(ROUND(AVG(CAST(COALESCE(r.Politeness, r.Score) AS DECIMAL(5,2))), 2) AS DECIMAL(4,2)) AS AvgPoliteness,
                CAST(ROUND(AVG(CAST(COALESCE(r.OnTimeDelivery, r.Score) AS DECIMAL(5,2))), 2) AS DECIMAL(4,2)) AS AvgOnTime,
                CAST(ROUND(AVG(CAST(COALESCE(r.VehicleCondition, r.Score) AS DECIMAL(5,2))), 2) AS DECIMAL(4,2)) AS AvgVehicle
            FROM Drivers d
            INNER JOIN TripRatings r ON r.DriverId = d.Id AND r.TenantId = d.TenantId AND r.IsHidden = 0
            WHERE d.TenantId = @TenantId
              AND d.IsDeleted = 0
              AND d.IsActive = 1
              AND d.PublicRatingOptOut = 0
            GROUP BY d.Id, d.FullName
            HAVING COUNT(r.Id) >= 1
            ORDER BY AverageRating DESC, ReviewCount DESC
            """, new { TenantId = tenantId, Take = take }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<TripFeedbackFormContext?> GetFeedbackFormByTripAsync(
        int tenantId, int tripId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TripFeedbackFormContext>(new CommandDefinition("""
            SELECT
                t.TenantId,
                t.Id AS TripId,
                t.TripNumber,
                t.DriverId,
                CASE WHEN d.FullName IS NULL THEN NULL
                     ELSE LEFT(d.FullName, CASE WHEN CHARINDEX(' ', d.FullName) > 0
                         THEN CHARINDEX(' ', d.FullName) - 1 ELSE LEN(d.FullName) END)
                END AS DriverFirstName,
                CASE WHEN EXISTS (SELECT 1 FROM TripRatings r WHERE r.TenantId = t.TenantId AND r.TripId = t.Id)
                     THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS AlreadySubmitted,
                DATEADD(day, 14, COALESCE(t.ActualEnd, t.ScheduledEnd, t.UpdatedAt)) AS ExpiresAt
            FROM Trips t
            LEFT JOIN Drivers d ON d.Id = t.DriverId
            WHERE t.Id = @TripId AND t.TenantId = @TenantId AND t.IsDeleted = 0
            """, new { TripId = tripId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains("UQ_TripRatings", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("2627", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
