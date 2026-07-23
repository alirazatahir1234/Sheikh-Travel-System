using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

public record ChangePasswordCommand(int UserId, string CurrentPassword, string NewPassword) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "ChangePassword";
    public string AuditEntityName => "User";
    public int? AuditEntityId => UserId;
}

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from current password.");
    }
}

public class ChangePasswordCommandHandler(
    IDbConnectionFactory dbFactory,
    IPasswordHasher hasher,
    ISecurityEngine securityEngine,
    ITenantContext tenantContext)
    : IRequestHandler<ChangePasswordCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var user = await connection.QuerySingleOrDefaultAsync<(int Id, int TenantId, string PasswordHash)>(
            new CommandDefinition(
                "SELECT Id, TenantId, PasswordHash FROM Users WHERE Id = @UserId AND IsDeleted = 0",
                new { request.UserId },
                cancellationToken: cancellationToken));

        if (user == default)
            throw new NotFoundException("User", request.UserId);

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ConflictException("Current password is incorrect.");

        await EnsurePasswordPolicyAsync(user.TenantId, request.NewPassword, cancellationToken);

        var newHash = hasher.Hash(request.NewPassword);

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Users SET PasswordHash = @Hash, UpdatedAt = @UpdatedAt,
                  PasswordChangedAt = @PasswordChangedAt, FailedLoginAttempts = 0, LockoutEndUtc = NULL
                  WHERE Id = @Id",
                new
                {
                    Hash = newHash,
                    UpdatedAt = DateTime.UtcNow,
                    PasswordChangedAt = DateTime.UtcNow,
                    Id = request.UserId
                },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Password changed successfully.");
    }

    private async Task EnsurePasswordPolicyAsync(int tenantId, string password, CancellationToken cancellationToken)
    {
        try
        {
            var map = await securityEngine.GetEffectiveMapAsync(tenantId, cancellationToken);
            var minLen = securityEngine.GetInt(map, SecurityPolicyKeys.PasswordMinLength, 6);
            if (password.Length < minLen)
                throw new ConflictException($"Password must be at least {minLen} characters.");

            if (securityEngine.GetBool(map, SecurityPolicyKeys.PasswordComplexity, false))
            {
                var ok = password.Any(char.IsUpper)
                    && password.Any(char.IsLower)
                    && password.Any(char.IsDigit)
                    && password.Any(c => !char.IsLetterOrDigit(c));
                if (!ok)
                    throw new ConflictException("Password must include upper, lower, digit, and symbol characters.");
            }
        }
        catch (ConflictException)
        {
            throw;
        }
        catch
        {
            // Registry unavailable — keep FluentValidation floor.
        }
    }
}
