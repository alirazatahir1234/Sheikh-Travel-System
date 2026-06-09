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
public record UpdateUserStatusCommand(int Id, bool IsActive) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateStatus";
    public string AuditEntityName => "User";
    public int? AuditEntityId => Id;
}

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
public class UpdateUserStatusCommandHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope) : IRequestHandler<UpdateUserStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var tenantId = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT TenantId FROM Users WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!tenantId.HasValue)
            throw new NotFoundException("User", request.Id);

        platformScope.EnsureTenantAccess(tenantId.Value);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { request.IsActive, UpdatedAt = DateTime.UtcNow, request.Id },
                cancellationToken: cancellationToken));

        var state = request.IsActive ? "activated" : "deactivated";
        return ApiResponse<bool>.SuccessResponse(true, $"User {state} successfully.");
    }
}
