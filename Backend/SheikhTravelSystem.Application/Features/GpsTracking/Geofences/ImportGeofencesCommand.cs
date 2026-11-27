using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>
/// Creates the geofences the ERP parsed from a KML, GeoJSON or CSV file. Each row is validated like a single create;
/// invalid rows and duplicate names are skipped with a reason instead of failing the whole file.
/// </summary>
public record ImportGeofencesCommand(ImportGeofencesDto Body) : IRequest<ApiResponse<ImportGeofencesResultDto>>, IAuditableCommand
{
    public string AuditAction => "Import";
    public string AuditEntityName => "Geofence";
    public int? AuditEntityId => null;
}

public class ImportGeofencesCommandValidator : AbstractValidator<ImportGeofencesCommand>
{
    public const int MaxRows = 500;

    public ImportGeofencesCommandValidator()
    {
        RuleFor(x => x.Body.Items).NotEmpty().Must(items => items.Length <= MaxRows)
            .WithMessage($"A file can hold 1 to {MaxRows} geofences.");
    }
}

public class ImportGeofencesCommandHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<ImportGeofencesCommand, ApiResponse<ImportGeofencesResultDto>>
{
    private static readonly SaveGeofenceDtoValidator RowValidator = new();

    public async Task<ApiResponse<ImportGeofencesResultDto>> Handle(ImportGeofencesCommand request, CancellationToken cancellationToken)
    {
        var valid = new List<SaveGeofenceDto>();
        var skipped = new List<ImportSkippedDto>();
        foreach (var row in request.Body.Items)
        {
            var result = RowValidator.Validate(row);
            if (result.IsValid)
                valid.Add(row);
            else
                skipped.Add(new ImportSkippedDto { Name = row.Name, Reason = result.Errors[0].ErrorMessage });
        }

        if (valid.Count == 0)
            return ApiResponse<ImportGeofencesResultDto>.SuccessResponse(
                new ImportGeofencesResultDto { Created = 0, Skipped = skipped }, "Nothing imported.");

        var imported = await repository.ImportAsync(valid, cancellationToken);
        if (!imported.Success || imported.Data is null)
            return imported;

        return ApiResponse<ImportGeofencesResultDto>.SuccessResponse(new ImportGeofencesResultDto
        {
            Created = imported.Data.Created,
            Skipped = [.. skipped, .. imported.Data.Skipped]
        }, imported.Message);
    }
}
