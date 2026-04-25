using Dapper;
using Microsoft.AspNetCore.Http;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

/// <summary>
/// Writes audit log entries to the AuditLogs table, capturing the current
/// authenticated user and client IP address automatically.
/// </summary>
public class AuditService(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUserService,
    IHttpContextAccessor httpContextAccessor) : IAuditService
{
    public async Task LogAsync(string action, string entityName, int? entityId, CancellationToken cancellationToken = default)
    {
        var ipAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO AuditLogs ([Action], EntityName, EntityId, UserId, IpAddress, CreatedAt, IsDeleted)
                  VALUES (@Action, @EntityName, @EntityId, @UserId, @IpAddress, @CreatedAt, 0)",
                new
                {
                    Action    = action,
                    EntityName = entityName,
                    EntityId  = entityId,
                    UserId    = currentUserService.UserId,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));
    }
}
