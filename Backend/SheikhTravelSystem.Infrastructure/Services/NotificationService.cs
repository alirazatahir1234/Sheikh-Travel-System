using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ITenantContext _tenantContext;

    public NotificationService(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
    }

    public async Task CreateAsync(int? userId, string title, string message, NotificationType type, int? referenceId = null, CancellationToken cancellationToken = default)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO Notifications (UserId, Title, Message, Type, ReferenceId, IsRead, CreatedAt, IsDeleted)
                  VALUES (@UserId, @Title, @Message, @Type, @ReferenceId, 0, @CreatedAt, 0)",
                new { UserId = userId, Title = title, Message = message, Type = (int)type, ReferenceId = referenceId, CreatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));
    }

    public async Task CreateForAllAsync(string title, string message, NotificationType type, int? referenceId = null, CancellationToken cancellationToken = default)
    {
        using var connection = _dbFactory.CreateConnection();
        
        var tenantId = _tenantContext.TenantId ?? 1;
        var userIds = await connection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT Id FROM Users WHERE IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));

        foreach (var userId in userIds)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"INSERT INTO Notifications (UserId, Title, Message, Type, ReferenceId, IsRead, CreatedAt, IsDeleted)
                      VALUES (@UserId, @Title, @Message, @Type, @ReferenceId, 0, @CreatedAt, 0)",
                    new { UserId = userId, Title = title, Message = message, Type = (int)type, ReferenceId = referenceId, CreatedAt = DateTime.UtcNow },
                    cancellationToken: cancellationToken));
        }
    }
}
