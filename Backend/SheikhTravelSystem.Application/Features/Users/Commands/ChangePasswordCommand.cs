using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

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
    IUserRepository userRepository,
    IPasswordHasher hasher,
    ISecurityEngine securityEngine)
    : IRequestHandler<ChangePasswordCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetPasswordInfoAsync(request.UserId, cancellationToken);
        if (user is null)
            throw new NotFoundException("User", request.UserId);

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ConflictException("Current password is incorrect.");

        await EnsurePasswordPolicyAsync(user.TenantId, request.NewPassword, cancellationToken);

        var newHash = hasher.Hash(request.NewPassword);
        await userRepository.ChangePasswordAsync(request.UserId, newHash, cancellationToken);

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
