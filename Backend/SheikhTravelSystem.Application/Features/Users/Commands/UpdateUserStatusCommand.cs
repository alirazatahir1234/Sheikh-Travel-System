using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

/// <summary>
/// Updates user activation status.
/// </summary>
public record UpdateUserStatusCommand(int Id, bool IsActive) : IRequest<ApiResponse<bool>>;

/// <summary>
/// Validates user status update request.
/// </summary>
public class UpdateUserStatusCommandValidator : AbstractValidator<UpdateUserStatusCommand>
{
    public UpdateUserStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

/// <summary>
/// Handles activation/deactivation of users.
/// </summary>
public class UpdateUserStatusCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateUserStatusCommand, ApiResponse<bool>>
{
    /// <summary>
    /// Persists user activation status update.
    /// </summary>
    public async Task<ApiResponse<bool>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("User", request.Id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { request.IsActive, UpdatedAt = DateTime.UtcNow, request.Id },
                cancellationToken: cancellationToken));

        var state = request.IsActive ? "activated" : "deactivated";
        return ApiResponse<bool>.SuccessResponse(true, $"User {state} successfully.");
    }
}
