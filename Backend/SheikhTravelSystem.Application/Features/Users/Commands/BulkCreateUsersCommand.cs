using System.Security.Cryptography;
using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;
using ValidationException = FluentValidation.ValidationException;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

public enum BulkImportMode
{
    CreateOnly = 0,
    CreateOrUpdate = 1,
    UpdateOnly = 2
}

public record BulkCreateUsersOptions(
    bool DryRun = false,
    bool SkipDuplicates = true,
    BulkImportMode Mode = BulkImportMode.CreateOnly);

public record BulkCreateUsersCommand(
    IReadOnlyList<CreateUserDto> Users,
    BulkCreateUsersOptions? Options = null)
    : IRequest<ApiResponse<BulkCreateUsersResult>>, IAuditableCommand
{
    public string AuditAction => "BulkCreate";
    public string AuditEntityName => "User";
    public int? AuditEntityId => null;
}

public record BulkCreateUsersResult(
    int Succeeded,
    int Failed,
    int Skipped,
    bool DryRun,
    IReadOnlyList<BulkCreateUserSuccess> Created,
    IReadOnlyList<BulkCreateUserFailure> Errors,
    IReadOnlyList<BulkCreateUserSkipped> SkippedRows);

public record BulkCreateUserSuccess(
    int Row,
    string Email,
    int UserId,
    string? TemporaryPassword,
    bool DryRun = false);

public record BulkCreateUserFailure(
    int Row,
    string? Email,
    string Error);

public record BulkCreateUserSkipped(
    int Row,
    string Email,
    string Reason);

public class BulkCreateUsersCommandValidator : AbstractValidator<BulkCreateUsersCommand>
{
    public const int MaxBatchSize = 200;

    public BulkCreateUsersCommandValidator()
    {
        RuleFor(x => x.Users).NotNull().NotEmpty()
            .WithMessage("At least one user is required.");
        RuleFor(x => x.Users.Count).LessThanOrEqualTo(MaxBatchSize)
            .WithMessage($"Bulk import supports at most {MaxBatchSize} users per request.");
    }
}

public class BulkCreateUsersCommandHandler(
    IMediator mediator,
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope)
    : IRequestHandler<BulkCreateUsersCommand, ApiResponse<BulkCreateUsersResult>>
{
    public async Task<ApiResponse<BulkCreateUsersResult>> Handle(
        BulkCreateUsersCommand request,
        CancellationToken cancellationToken)
    {
        var options = request.Options ?? new BulkCreateUsersOptions();
        if (options.Mode is BulkImportMode.CreateOrUpdate or BulkImportMode.UpdateOnly)
        {
            return ApiResponse<BulkCreateUsersResult>.FailResponse(
                "Create or update and update-only import modes are not available yet.");
        }

        var created = new List<BulkCreateUserSuccess>();
        var errors = new List<BulkCreateUserFailure>();
        var skipped = new List<BulkCreateUserSkipped>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tenantId = platformScope.TenantId;

        using var connection = dbFactory.CreateConnection();

        for (var i = 0; i < request.Users.Count; i++)
        {
            var row = i + 1;
            var dto = request.Users[i];
            var email = dto.Email?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(email))
            {
                errors.Add(new BulkCreateUserFailure(row, null, "Email is required."));
                continue;
            }

            if (!seenEmails.Add(email))
            {
                errors.Add(new BulkCreateUserFailure(row, email, "Duplicate email in this import file."));
                continue;
            }

            if (options.SkipDuplicates && tenantId > 0)
            {
                var exists = await connection.ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Email = @Email AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                        new { Email = email, TenantId = tenantId },
                        cancellationToken: cancellationToken));

                if (exists)
                {
                    skipped.Add(new BulkCreateUserSkipped(row, email, "Email already exists in this company."));
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.PlatformRoleCode) && tenantId > 0)
            {
                var code = dto.PlatformRoleCode.Trim().ToUpperInvariant();
                var roleExists = await connection.ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        "SELECT CASE WHEN EXISTS(SELECT 1 FROM Roles WHERE TenantId = @TenantId AND Code = @Code AND IsActive = 1) THEN 1 ELSE 0 END",
                        new { TenantId = tenantId, Code = code },
                        cancellationToken: cancellationToken));

                if (!roleExists)
                {
                    errors.Add(new BulkCreateUserFailure(row, email, $"Platform role '{code}' was not found for this company."));
                    continue;
                }
            }

            try
            {
                await UserQueries.EnsureOrgBelongsToTenantAsync(
                    connection, tenantId, dto.BranchId, dto.DepartmentId, cancellationToken);
            }
            catch (ConflictException ex)
            {
                errors.Add(new BulkCreateUserFailure(row, email, ex.Message));
                continue;
            }

            if (options.DryRun)
            {
                created.Add(new BulkCreateUserSuccess(row, email, 0, null, DryRun: true));
                continue;
            }

            string? temporaryPassword = null;
            var password = dto.Password?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(password))
            {
                temporaryPassword = GenerateTemporaryPassword();
                password = temporaryPassword;
            }

            var normalized = dto with
            {
                FullName = dto.FullName?.Trim() ?? "",
                Email = email,
                Password = password,
                Phone = dto.Phone?.Trim() ?? "",
                PlatformRoleCode = string.IsNullOrWhiteSpace(dto.PlatformRoleCode)
                    ? null
                    : dto.PlatformRoleCode.Trim().ToUpperInvariant(),
                JobTitle = string.IsNullOrWhiteSpace(dto.JobTitle) ? null : dto.JobTitle.Trim(),
                EmployeeCode = string.IsNullOrWhiteSpace(dto.EmployeeCode) ? null : dto.EmployeeCode.Trim(),
                EmployeeType = string.IsNullOrWhiteSpace(dto.EmployeeType) ? null : dto.EmployeeType.Trim(),
                Status = string.IsNullOrWhiteSpace(dto.Status) ? null : dto.Status.Trim(),
                DefaultWorkspaceKey = string.IsNullOrWhiteSpace(dto.DefaultWorkspaceKey) ? null : dto.DefaultWorkspaceKey.Trim(),
                DefaultDashboardKey = string.IsNullOrWhiteSpace(dto.DefaultDashboardKey) ? null : dto.DefaultDashboardKey.Trim(),
                HomeRoute = string.IsNullOrWhiteSpace(dto.HomeRoute) ? null : dto.HomeRoute.Trim(),
                TimeZone = string.IsNullOrWhiteSpace(dto.TimeZone) ? null : dto.TimeZone.Trim(),
                Language = string.IsNullOrWhiteSpace(dto.Language) ? null : dto.Language.Trim(),
                Theme = string.IsNullOrWhiteSpace(dto.Theme) ? null : dto.Theme.Trim(),
                AvatarUrl = string.IsNullOrWhiteSpace(dto.AvatarUrl) ? null : dto.AvatarUrl.Trim()
            };

            try
            {
                var result = await mediator.Send(new CreateUserCommand(normalized), cancellationToken);
                if (result.Success && result.Data > 0)
                {
                    created.Add(new BulkCreateUserSuccess(row, email, result.Data, temporaryPassword));
                }
                else
                {
                    errors.Add(new BulkCreateUserFailure(
                        row,
                        email,
                        result.Message ?? "Create failed."));
                }
            }
            catch (ConflictException ex)
            {
                if (options.SkipDuplicates)
                {
                    skipped.Add(new BulkCreateUserSkipped(row, email, ex.Message));
                }
                else
                {
                    errors.Add(new BulkCreateUserFailure(row, email, ex.Message));
                }
            }
            catch (ValidationException ex)
            {
                var msg = string.Join("; ", ex.Errors.Select(e => e.ErrorMessage).Distinct());
                errors.Add(new BulkCreateUserFailure(row, email, string.IsNullOrWhiteSpace(msg) ? "Validation failed." : msg));
            }
            catch (NotFoundException ex)
            {
                errors.Add(new BulkCreateUserFailure(row, email, ex.Message));
            }
            catch (Exception ex)
            {
                errors.Add(new BulkCreateUserFailure(row, email, ex.Message));
            }
        }

        var payload = new BulkCreateUsersResult(
            created.Count,
            errors.Count,
            skipped.Count,
            options.DryRun,
            created,
            errors,
            skipped);

        var message = options.DryRun
            ? $"Dry run: {created.Count} would be created, {errors.Count} failed, {skipped.Count} skipped."
            : errors.Count == 0
                ? $"Created {created.Count} user(s); skipped {skipped.Count}."
                : $"Created {created.Count} user(s); {errors.Count} failed; skipped {skipped.Count}.";

        return ApiResponse<BulkCreateUsersResult>.SuccessResponse(payload, message);
    }

    private static string GenerateTemporaryPassword()
    {
        const string uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowercase = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%";
        const string all = uppercase + lowercase + digits + symbols;
        const int length = 12;

        var chars = new char[length];
        chars[0] = uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)];
        chars[1] = lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

        for (var i = 4; i < length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
