using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

public record DeleteUserCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "User";
    public int? AuditEntityId => Id;
}

public class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IPlatformScope platformScope) : IRequestHandler<DeleteUserCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = await userRepository.GetTenantIdAsync(request.Id, cancellationToken);
        if (!tenantId.HasValue)
            throw new NotFoundException("User", request.Id);

        platformScope.EnsureTenantAccess(tenantId.Value);

        await userRepository.SoftDeleteAsync(request.Id, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "User deleted successfully.");
    }
}
