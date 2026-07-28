using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
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
    IDbConnectionFactory dbFactory,
    IPasswordHasher passwordHasher,
    IPlatformScope platformScope,
    ICurrentUserService currentUser) : IRequestHandler<CreateUserCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.User;
        var tenantId = platformScope.TenantId;

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Email = @Email AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { dto.Email, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (exists)
            throw new ConflictException($"User with email '{dto.Email}' already exists.");

        await UserQueries.EnsureOrgBelongsToTenantAsync(
            connection, tenantId, dto.BranchId, dto.DepartmentId, cancellationToken);

        var status = UserLifecycle.Normalize(dto.Status, true);
        var isActive = UserLifecycle.IsActiveStatus(status);
        var employeeType = EmployeeTypes.Normalize(dto.EmployeeType);
        var passwordHash = passwordHasher.Hash(dto.Password);
        var now = DateTime.UtcNow;

        try
        {
            var id = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    @"INSERT INTO Users (
                        TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive, CreatedAt, IsDeleted,
                        BranchId, DepartmentId, JobTitle, EmployeeCode, EmployeeType, Status,
                        DefaultWorkspaceKey, DefaultDashboardKey, HomeRoute, TimeZone, Language, Theme, AvatarUrl,
                        PasswordChangedAt)
                      VALUES (
                        @TenantId, @FullName, @Email, @PasswordHash, @Phone, @Role, @IsActive, @CreatedAt, 0,
                        @BranchId, @DepartmentId, @JobTitle, @EmployeeCode, @EmployeeType, @Status,
                        @DefaultWorkspaceKey, @DefaultDashboardKey, @HomeRoute, @TimeZone, @Language, @Theme, @AvatarUrl,
                        @PasswordChangedAt);
                      SELECT SCOPE_IDENTITY();",
                    new
                    {
                        TenantId = tenantId,
                        dto.FullName,
                        dto.Email,
                        PasswordHash = passwordHash,
                        dto.Phone,
                        Role = (int)dto.Role,
                        IsActive = isActive,
                        CreatedAt = now,
                        PasswordChangedAt = now,
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

            if (!string.IsNullOrWhiteSpace(dto.PlatformRoleCode))
            {
                await UserRoleAssignment.AssignPlatformRoleAsync(
                    connection, id, tenantId, dto.PlatformRoleCode, dto.BranchId, dto.DepartmentId,
                    currentUser.UserId, cancellationToken);
            }
            else
            {
                await UserRoleAssignment.SyncLegacyRoleAsync(
                    connection, id, tenantId, dto.Role, dto.BranchId, dto.DepartmentId,
                    currentUser.UserId, cancellationToken);
            }

            return ApiResponse<int>.SuccessResponse(id, "User created successfully.");
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid column", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
        {
            var id = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    @"INSERT INTO Users (TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive, CreatedAt, IsDeleted, BranchId, DepartmentId)
                      VALUES (@TenantId, @FullName, @Email, @PasswordHash, @Phone, @Role, @IsActive, @CreatedAt, 0, @BranchId, @DepartmentId);
                      SELECT SCOPE_IDENTITY();",
                    new
                    {
                        TenantId = tenantId,
                        dto.FullName,
                        dto.Email,
                        PasswordHash = passwordHash,
                        dto.Phone,
                        Role = (int)dto.Role,
                        IsActive = isActive,
                        CreatedAt = DateTime.UtcNow,
                        dto.BranchId,
                        dto.DepartmentId
                    },
                    cancellationToken: cancellationToken));

            if (!string.IsNullOrWhiteSpace(dto.PlatformRoleCode))
            {
                await UserRoleAssignment.AssignPlatformRoleAsync(
                    connection, id, tenantId, dto.PlatformRoleCode, dto.BranchId, dto.DepartmentId,
                    currentUser.UserId, cancellationToken);
            }
            else
            {
                await UserRoleAssignment.SyncLegacyRoleAsync(
                    connection, id, tenantId, dto.Role, dto.BranchId, dto.DepartmentId,
                    currentUser.UserId, cancellationToken);
            }

            return ApiResponse<int>.SuccessResponse(id, "User created successfully.");
        }
    }
}
