using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

public record UpdateUserStatusCommand(int Id, bool IsActive, string? Status = null)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateStatus";
    public string AuditEntityName => "User";
    public int? AuditEntityId => Id;
}

public class UpdateUserStatusCommandValidator : AbstractValidator<UpdateUserStatusCommand>
{
    public UpdateUserStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status)
            .Must(s => s == null || UserLifecycle.All.Contains(s))
            .WithMessage("Invalid status.");
    }
}

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

        var status = UserLifecycle.Normalize(request.Status, request.IsActive);
        var isActive = UserLifecycle.IsActiveStatus(status);

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE Users SET IsActive = @IsActive, Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                    new { IsActive = isActive, Status = status, UpdatedAt = DateTime.UtcNow, request.Id },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE Users SET IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                    new { IsActive = isActive, UpdatedAt = DateTime.UtcNow, request.Id },
                    cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, $"User status set to {status}.");
    }
}
