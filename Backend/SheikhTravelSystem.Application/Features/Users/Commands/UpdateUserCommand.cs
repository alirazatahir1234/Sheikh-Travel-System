using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
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
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope) : IRequestHandler<UpdateUserCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.User;

        var tenantId = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT TenantId FROM Users WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!tenantId.HasValue)
            throw new NotFoundException("User", request.Id);

        platformScope.EnsureTenantAccess(tenantId.Value);

        var emailConflict = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Users WHERE Email = @Email AND Id != @Id AND TenantId = @TenantId AND IsDeleted = 0
                ) THEN 1 ELSE 0 END",
                new { dto.Email, request.Id, TenantId = tenantId.Value },
                cancellationToken: cancellationToken));

        if (emailConflict)
            throw new ConflictException($"Email '{dto.Email}' is already in use.");

        await UserQueries.EnsureOrgBelongsToTenantAsync(
            connection, tenantId.Value, dto.BranchId, dto.DepartmentId, cancellationToken);

        var status = UserLifecycle.Normalize(dto.Status, dto.IsActive);
        var isActive = UserLifecycle.IsActiveStatus(status);
        var employeeType = EmployeeTypes.Normalize(dto.EmployeeType);

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Users SET
                        FullName = @FullName, Email = @Email, Phone = @Phone,
                        Role = @Role, IsActive = @IsActive, UpdatedAt = @UpdatedAt,
                        BranchId = @BranchId, DepartmentId = @DepartmentId,
                        JobTitle = @JobTitle, EmployeeCode = @EmployeeCode, EmployeeType = @EmployeeType,
                        Status = @Status,
                        DefaultWorkspaceKey = @DefaultWorkspaceKey,
                        DefaultDashboardKey = @DefaultDashboardKey,
                        HomeRoute = @HomeRoute, TimeZone = @TimeZone,
                        Language = @Language, Theme = @Theme, AvatarUrl = @AvatarUrl
                      WHERE Id = @Id",
                    new
                    {
                        dto.FullName,
                        dto.Email,
                        dto.Phone,
                        Role = (int)dto.Role,
                        IsActive = isActive,
                        UpdatedAt = DateTime.UtcNow,
                        request.Id,
                        dto.BranchId,
                        dto.DepartmentId,
                        dto.JobTitle,
                        dto.EmployeeCode,
                        EmployeeType = employeeType,
                        Status = status,
                        dto.DefaultWorkspaceKey,
                        dto.DefaultDashboardKey,
                        dto.HomeRoute,
                        dto.TimeZone,
                        dto.Language,
                        dto.Theme,
                        dto.AvatarUrl
                    },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Users SET FullName = @FullName, Email = @Email, Phone = @Phone,
                      Role = @Role, IsActive = @IsActive, UpdatedAt = @UpdatedAt,
                      BranchId = @BranchId, DepartmentId = @DepartmentId
                      WHERE Id = @Id",
                    new
                    {
                        dto.FullName,
                        dto.Email,
                        dto.Phone,
                        Role = (int)dto.Role,
                        IsActive = isActive,
                        UpdatedAt = DateTime.UtcNow,
                        request.Id,
                        dto.BranchId,
                        dto.DepartmentId
                    },
                    cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, "User updated successfully.");
    }
}
