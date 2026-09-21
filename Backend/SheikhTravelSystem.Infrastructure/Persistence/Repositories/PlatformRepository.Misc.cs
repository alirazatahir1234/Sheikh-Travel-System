using Dapper;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class PlatformRepository
{
    public async Task<IReadOnlyList<string>> GetBranchLabelsAsync(
        int tenantId, IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return Array.Empty<string>();
        using var connection = dbFactory.CreateConnection();
        var labels = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT Name FROM Branches
            WHERE TenantId = @TenantId AND Id IN @Ids
            ORDER BY Name
            """,
            new { TenantId = tenantId, Ids = ids.ToArray() },
            cancellationToken: cancellationToken));
        return labels.ToList();
    }

    public async Task<IReadOnlyList<string>> GetDepartmentLabelsAsync(
        int tenantId, IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return Array.Empty<string>();
        using var connection = dbFactory.CreateConnection();
        var labels = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT Name FROM Departments
            WHERE TenantId = @TenantId AND Id IN @Ids
            ORDER BY Name
            """,
            new { TenantId = tenantId, Ids = ids.ToArray() },
            cancellationToken: cancellationToken));
        return labels.ToList();
    }

    public async Task<int?> GetUserTenantIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Users WHERE Id = @Id AND IsDeleted = 0",
            new { Id = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> ApproveGpsDeviceCommandAsync(
        int commandId, bool approve, string? note, string? approvedBy, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var status = approve ? "pending" : "cancelled";
        var approval = approve ? "Approved" : "Rejected";
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE GpsDeviceCommands
            SET Status = @Status,
                ApprovalStatus = @Approval,
                ApprovedBy = @By,
                ApprovedAt = SYSUTCDATETIME(),
                UpdatedAt = SYSUTCDATETIME(),
                ErrorMessage = CASE WHEN @Approve = 0 THEN COALESCE(@Note, N'Rejected') ELSE ErrorMessage END
            WHERE Id = @Id AND IsDeleted = 0 AND Status = N'PendingApproval'
            """,
            new
            {
                Id = commandId,
                Status = status,
                Approval = approval,
                By = approvedBy,
                Approve = approve,
                Note = note
            },
            cancellationToken: cancellationToken));
    }
}
