using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

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

public class UpdateProfileCommandHandler(IUserRepository userRepository)
    : IRequestHandler<UpdateProfileCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var exists = await userRepository.ExistsAsync(request.UserId, cancellationToken);
        if (!exists)
            throw new NotFoundException("User", request.UserId);

        await userRepository.UpdateProfileAsync(
            request.UserId,
            request.FullName,
            request.PhoneNumber,
            request.TimeZone,
            request.Language,
            request.Theme,
            request.AvatarUrl,
            request.JobTitle,
            cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Profile updated successfully.");
    }
}
