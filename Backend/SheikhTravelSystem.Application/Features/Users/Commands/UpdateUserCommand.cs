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
    }
}

public class UpdateUserCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateUserCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.User;

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("User", request.Id);

        var emailConflict = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Email = @Email AND Id != @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { dto.Email, request.Id },
                cancellationToken: cancellationToken));

        if (emailConflict)
            throw new ConflictException($"Email '{dto.Email}' is already in use.");

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Users SET FullName = @FullName, Email = @Email, Phone = @Phone,
                  Role = @Role, IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { dto.FullName, dto.Email, dto.Phone, Role = (int)dto.Role, dto.IsActive, UpdatedAt = DateTime.UtcNow, request.Id },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "User updated successfully.");
    }
}
