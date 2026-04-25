namespace SheikhTravelSystem.Application.Features.AuditLogs.DTOs;

public record AuditLogDto(
    int Id,
    string Action,
    string EntityName,
    int? EntityId,
    string? OldValues,
    string? NewValues,
    int? UserId,
    string? UserName,
    string? IpAddress,
    DateTime CreatedAt
);
