using System.Security.Cryptography;
using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

/// <summary>
/// Resets a user's password and returns a temporary password.
/// </summary>
public record ResetUserPasswordCommand(int Id) : IRequest<ApiResponse<ResetUserPasswordResponse>>, IAuditableCommand
{
    public string AuditAction => "ResetPassword";
    public string AuditEntityName => "User";
    public int? AuditEntityId => Id;
}

/// <summary>
/// Response payload for password reset operation.
/// </summary>
public record ResetUserPasswordResponse(string TemporaryPassword);

/// <summary>
/// Validates reset password request.
/// </summary>
public class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

/// <summary>
/// Handles password reset operations for users.
/// </summary>
public class ResetUserPasswordCommandHandler(IDbConnectionFactory dbFactory, IPasswordHasher passwordHasher)
    : IRequestHandler<ResetUserPasswordCommand, ApiResponse<ResetUserPasswordResponse>>
{
    /// <summary>
    /// Generates a temporary password, stores hashed value, and revokes existing refresh token.
    /// </summary>
    public async Task<ApiResponse<ResetUserPasswordResponse>> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("User", request.Id);

        var temporaryPassword = GenerateTemporaryPassword();
        var passwordHash = passwordHasher.Hash(temporaryPassword);

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Users
                  SET PasswordHash = @PasswordHash,
                      RefreshToken = NULL,
                      RefreshTokenExpiryTime = NULL,
                      UpdatedAt = @UpdatedAt
                  WHERE Id = @Id",
                new { PasswordHash = passwordHash, UpdatedAt = DateTime.UtcNow, request.Id },
                cancellationToken: cancellationToken));

        var response = new ResetUserPasswordResponse(temporaryPassword);
        return ApiResponse<ResetUserPasswordResponse>.SuccessResponse(response, "Password reset successfully.");
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
