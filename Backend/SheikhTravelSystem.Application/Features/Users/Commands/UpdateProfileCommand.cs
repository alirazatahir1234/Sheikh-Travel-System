using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

public record UpdateProfileCommand(
    int UserId,
    string FullName,
    string? PhoneNumber = null,
    string? TimeZone = null,
    string? Language = null,
    string? Theme = null,
    string? AvatarUrl = null,
    string? JobTitle = null) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "User";
    public int? AuditEntityId => UserId;
}

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).MaximumLength(30).When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        RuleFor(x => x.TimeZone).MaximumLength(100).When(x => x.TimeZone != null);
        RuleFor(x => x.Language).MaximumLength(20).When(x => x.Language != null);
        RuleFor(x => x.Theme).MaximumLength(50).When(x => x.Theme != null);
        RuleFor(x => x.AvatarUrl).MaximumLength(500).When(x => x.AvatarUrl != null);
        RuleFor(x => x.JobTitle).MaximumLength(200).When(x => x.JobTitle != null);
    }
}

public class UpdateProfileCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateProfileCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Id = @UserId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.UserId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("User", request.UserId);

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Users SET
                        FullName = @FullName,
                        Phone = @Phone,
                        TimeZone = COALESCE(@TimeZone, TimeZone),
                        Language = COALESCE(@Language, Language),
                        Theme = COALESCE(@Theme, Theme),
                        AvatarUrl = COALESCE(@AvatarUrl, AvatarUrl),
                        JobTitle = COALESCE(@JobTitle, JobTitle),
                        UpdatedAt = @UpdatedAt
                      WHERE Id = @UserId",
                    new
                    {
                        request.FullName,
                        Phone = request.PhoneNumber,
                        request.TimeZone,
                        request.Language,
                        request.Theme,
                        request.AvatarUrl,
                        request.JobTitle,
                        UpdatedAt = DateTime.UtcNow,
                        request.UserId
                    },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE Users SET FullName = @FullName, Phone = @Phone, UpdatedAt = @UpdatedAt WHERE Id = @UserId",
                    new
                    {
                        request.FullName,
                        Phone = request.PhoneNumber,
                        UpdatedAt = DateTime.UtcNow,
                        request.UserId
                    },
                    cancellationToken: cancellationToken));
        }

        return ApiResponse<bool>.SuccessResponse(true, "Profile updated successfully.");
    }
}
