using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

public record UpdateUserCommand(int Id, UpdateUserDto User) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "User";
    public int? AuditEntityId => Id;
}

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.User.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.User.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.User.Phone).NotEmpty();
        RuleFor(x => x.User.Role).IsInEnum();
        RuleFor(x => x.User.JobTitle).MaximumLength(200).When(x => x.User.JobTitle != null);
        RuleFor(x => x.User.EmployeeCode).MaximumLength(50).When(x => x.User.EmployeeCode != null);
        RuleFor(x => x.User.Status)
            .Must(s => s == null || UserLifecycle.All.Contains(s))
            .WithMessage("Invalid status.");
        RuleFor(x => x.User.EmployeeType)
            .Must(t => t == null || EmployeeTypes.All.Contains(t))
            .WithMessage("Invalid employee type.");
    }
}

public class UpdateUserCommandHandler(
    IUserRepository userRepository,
    IPlatformScope platformScope,
    ICurrentUserService currentUser) : IRequestHandler<UpdateUserCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var dto = request.User;

        var currentTenantId = await userRepository.GetTenantIdAsync(request.Id, cancellationToken);
        if (!currentTenantId.HasValue)
            throw new NotFoundException("User", request.Id);

        platformScope.EnsureTenantAccess(currentTenantId.Value);

        var tenantId = ResolveTargetTenantId(currentTenantId.Value, dto.CompanyId);
        if (tenantId != currentTenantId.Value)
            platformScope.EnsureTenantAccess(tenantId);

        var emailConflict = await userRepository.EmailExistsForOtherAsync(
            dto.Email, request.Id, tenantId, cancellationToken);
        if (emailConflict)
            throw new ConflictException($"Email '{dto.Email}' is already in use.");

        await userRepository.EnsureOrgBelongsToTenantAsync(
            tenantId, dto.BranchId, dto.DepartmentId, cancellationToken);

        var status = UserLifecycle.Normalize(dto.Status, dto.IsActive);
        var isActive = UserLifecycle.IsActiveStatus(status);
        var employeeType = EmployeeTypes.Normalize(dto.EmployeeType);

        await userRepository.UpdateAsync(
            request.Id,
            new UserUpdateModel(
                tenantId,
                dto.FullName,
                dto.Email,
                dto.Phone,
                dto.Role,
                isActive,
                DateTime.UtcNow,
                dto.BranchId,
                dto.DepartmentId,
                dto.JobTitle,
                dto.EmployeeCode,
                employeeType,
                status,
                dto.DefaultWorkspaceKey,
                dto.DefaultDashboardKey,
                dto.HomeRoute,
                dto.TimeZone,
                dto.Language,
                dto.Theme,
                dto.AvatarUrl),
            cancellationToken);

        await userRepository.SyncLegacyRoleAsync(
            request.Id,
            tenantId,
            dto.Role,
            dto.BranchId,
            dto.DepartmentId,
            currentUser.UserId,
            cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "User updated successfully.");
    }

    private int ResolveTargetTenantId(int currentTenantId, int? companyId)
    {
        if (!platformScope.IsSuperAdmin)
        {
            if (companyId is int requested && requested > 0 && requested != currentTenantId)
                throw new ForbiddenException("You cannot move users to another company.");
            return currentTenantId;
        }

        if (companyId is int cid && cid > 0)
            return cid;

        return currentTenantId;
    }
}
