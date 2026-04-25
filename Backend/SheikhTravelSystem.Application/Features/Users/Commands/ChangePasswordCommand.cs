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

public class ChangePasswordCommandHandler(IDbConnectionFactory dbFactory, IPasswordHasher hasher)
    : IRequestHandler<ChangePasswordCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var user = await connection.QuerySingleOrDefaultAsync<(int Id, string PasswordHash)>(
            new CommandDefinition(
                "SELECT Id, PasswordHash FROM Users WHERE Id = @UserId AND IsDeleted = 0",
                new { request.UserId },
                cancellationToken: cancellationToken));

        if (user == default)
            throw new NotFoundException("User", request.UserId);

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ConflictException("Current password is incorrect.");

        var newHash = hasher.Hash(request.NewPassword);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET PasswordHash = @Hash, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { Hash = newHash, UpdatedAt = DateTime.UtcNow, Id = request.UserId },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Password changed successfully.");
    }
}
