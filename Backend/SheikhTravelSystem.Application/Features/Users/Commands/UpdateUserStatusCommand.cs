using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
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
    IUserRepository userRepository,
    IPlatformScope platformScope) : IRequestHandler<UpdateUserStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantId = await userRepository.GetTenantIdAsync(request.Id, cancellationToken);
        if (!tenantId.HasValue)
            throw new NotFoundException("User", request.Id);

        platformScope.EnsureTenantAccess(tenantId.Value);

        var status = UserLifecycle.Normalize(request.Status, request.IsActive);
        var isActive = UserLifecycle.IsActiveStatus(status);

        await userRepository.UpdateStatusAsync(request.Id, isActive, status, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, $"User status set to {status}.");
    }
}
