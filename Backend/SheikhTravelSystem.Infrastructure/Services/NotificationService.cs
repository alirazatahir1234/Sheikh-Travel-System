using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IDbConnectionFactory _dbFactory;

    public NotificationService(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
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
        
        var userIds = await connection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT Id FROM Users WHERE IsDeleted = 0 AND IsActive = 1",
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
