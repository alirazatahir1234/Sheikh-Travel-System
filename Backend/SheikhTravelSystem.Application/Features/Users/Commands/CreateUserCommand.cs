using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

public record CreateUserCommand(CreateUserDto User) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "User";
    public int? AuditEntityId => null;
}

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.User.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.User.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.User.Password).NotEmpty().MinimumLength(6);
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

public class CreateUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IPlatformScope platformScope,
    ICurrentUserService currentUser) : IRequestHandler<CreateUserCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var dto = request.User;
        var tenantId = ResolveTargetTenantId(dto.CompanyId);

        var exists = await userRepository.EmailExistsInTenantAsync(dto.Email, tenantId, cancellationToken);
        if (exists)
            throw new ConflictException($"User with email '{dto.Email}' already exists.");

        await userRepository.EnsureOrgBelongsToTenantAsync(
            tenantId, dto.BranchId, dto.DepartmentId, cancellationToken);

        UserRoleAssignment.EnsureCanAssignPlatformRole(dto.PlatformRoleCode, currentUser);

        var status = UserLifecycle.Normalize(dto.Status, true);
        var isActive = UserLifecycle.IsActiveStatus(status);
        var employeeType = EmployeeTypes.Normalize(dto.EmployeeType);
        var passwordHash = passwordHasher.Hash(dto.Password);
        var now = DateTime.UtcNow;

        var id = await userRepository.InsertAsync(
            new UserInsertModel(
                tenantId,
                dto.FullName,
                dto.Email,
                passwordHash,
                dto.Phone,
                dto.Role,
                isActive,
                now,
                now,
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

        if (!string.IsNullOrWhiteSpace(dto.PlatformRoleCode))
        {
            await userRepository.AssignPlatformRoleAsync(
                id, tenantId, dto.PlatformRoleCode, dto.BranchId, dto.DepartmentId,
                currentUser.UserId, cancellationToken);
        }
        else
        {
            await userRepository.SyncLegacyRoleAsync(
                id, tenantId, dto.Role, dto.BranchId, dto.DepartmentId,
                currentUser.UserId, cancellationToken);
        }

        return ApiResponse<int>.SuccessResponse(id, "User created successfully.");
    }

    private int ResolveTargetTenantId(int? companyId)
    {
        if (platformScope.IsSuperAdmin)
        {
            if (companyId is int cid && cid > 0)
            {
                platformScope.EnsureTenantAccess(cid);
                return cid;
            }

            return platformScope.TenantId;
        }

        if (companyId is int requested && requested > 0 && requested != platformScope.TenantId)
            throw new ForbiddenException("You cannot create users for another company.");

        return platformScope.TenantId;
    }
}
