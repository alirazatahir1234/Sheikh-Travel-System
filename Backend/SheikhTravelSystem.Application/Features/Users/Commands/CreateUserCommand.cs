using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

public record CreateUserCommand(CreateUserDto User) : IRequest<ApiResponse<int>>;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.User.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.User.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.User.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.User.Phone).NotEmpty();
        RuleFor(x => x.User.Role).IsInEnum();
    }
}

public class CreateUserCommandHandler(IDbConnectionFactory dbFactory, IPasswordHasher passwordHasher)
    : IRequestHandler<CreateUserCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.User;

        var exists = await connection.ExecuteScalarAsync<bool>(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Email = @Email AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { dto.Email });

        if (exists)
            throw new ConflictException($"User with email '{dto.Email}' already exists.");

        var passwordHash = passwordHasher.Hash(dto.Password);

        var id = await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, IsActive, CreatedAt, IsDeleted)
              VALUES (@FullName, @Email, @PasswordHash, @Phone, @Role, 1, @CreatedAt, 0);
              SELECT SCOPE_IDENTITY();",
            new { dto.FullName, dto.Email, PasswordHash = passwordHash, dto.Phone, Role = (int)dto.Role, CreatedAt = DateTime.UtcNow });

        return ApiResponse<int>.SuccessResponse(id, "User created successfully.");
    }
}
