using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Adds a company-specific geofence type ("+ Add custom type"). Red is reserved for restricted types.</summary>
public record CreateGeofenceTypeCommand(CreateGeofenceTypeDto Type) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "GeofenceType";
    public int? AuditEntityId => null;
}

public class CreateGeofenceTypeCommandValidator : AbstractValidator<CreateGeofenceTypeCommand>
{
    public CreateGeofenceTypeCommandValidator()
    {
        RuleFor(x => x.Type.Name).NotEmpty().MaximumLength(50).WithMessage("Type name is required (max 50 characters).");
        RuleFor(x => x.Type.Name)
            .Must(n => GeofenceTypeCatalog.Find(n) is null)
            .WithMessage("That type already exists.");
        RuleFor(x => x.Type.Color).Matches("^#[0-9a-fA-F]{6}$").WithMessage("Colour must be a #rrggbb value.");
        RuleFor(x => x.Type)
            .Must(t => t.IsRestricted || !string.Equals(t.Color, GeofenceTypeCatalog.RestrictedColor, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Red is reserved for restricted types.");
    }
}

public class CreateGeofenceTypeCommandHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<CreateGeofenceTypeCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateGeofenceTypeCommand request, CancellationToken cancellationToken)
        => repository.CreateTypeAsync(request.Type, cancellationToken);
}
