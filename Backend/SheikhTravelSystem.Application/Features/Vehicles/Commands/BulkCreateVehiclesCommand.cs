using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record BulkCreateVehiclesOptions(bool DryRun = false, bool SkipDuplicates = true);

public record BulkCreateVehiclesCommand(
    IReadOnlyList<CreateVehicleDto> Vehicles,
    BulkCreateVehiclesOptions? Options = null)
    : IRequest<ApiResponse<BulkCreateVehiclesResult>>, IAuditableCommand
{
    public string AuditAction => "BulkCreate";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => null;
}

public record BulkCreateVehiclesResult(
    int Succeeded,
    int Failed,
    int Skipped,
    bool DryRun,
    IReadOnlyList<BulkCreateVehicleSuccess> Created,
    IReadOnlyList<BulkCreateVehicleFailure> Errors,
    IReadOnlyList<BulkCreateVehicleSkipped> SkippedRows);

public record BulkCreateVehicleSuccess(int Row, string RegistrationNumber, int VehicleId, bool DryRun = false);
public record BulkCreateVehicleFailure(int Row, string? RegistrationNumber, string Error);
public record BulkCreateVehicleSkipped(int Row, string RegistrationNumber, string Reason);

public class BulkCreateVehiclesCommandValidator : AbstractValidator<BulkCreateVehiclesCommand>
{
    public const int MaxBatchSize = 150;

    public BulkCreateVehiclesCommandValidator()
    {
        RuleFor(x => x.Vehicles).NotNull().NotEmpty();
        RuleFor(x => x.Vehicles.Count).LessThanOrEqualTo(MaxBatchSize)
            .WithMessage($"Bulk import supports at most {MaxBatchSize} vehicles per request.");
    }
}

public class BulkCreateVehiclesCommandHandler(
    IMediator mediator,
    IVehicleRepository vehicleRepository,
    ITenantContext tenantContext)
    : IRequestHandler<BulkCreateVehiclesCommand, ApiResponse<BulkCreateVehiclesResult>>
{
    public async Task<ApiResponse<BulkCreateVehiclesResult>> Handle(
        BulkCreateVehiclesCommand request,
        CancellationToken cancellationToken)
    {
        var options = request.Options ?? new BulkCreateVehiclesOptions();
        var created = new List<BulkCreateVehicleSuccess>();
        var errors = new List<BulkCreateVehicleFailure>();
        var skipped = new List<BulkCreateVehicleSkipped>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tenantId = tenantContext.GetRequiredTenantId();

        for (var i = 0; i < request.Vehicles.Count; i++)
        {
            var row = i + 1;
            var dto = request.Vehicles[i];
            var reg = dto.RegistrationNumber?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(reg))
            {
                errors.Add(new BulkCreateVehicleFailure(row, null, "Registration number is required."));
                continue;
            }

            if (!seen.Add(reg))
            {
                errors.Add(new BulkCreateVehicleFailure(row, reg, "Duplicate registration in this import file."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add(new BulkCreateVehicleFailure(row, reg, "Name is required."));
                continue;
            }

            if (options.SkipDuplicates && await vehicleRepository.RegistrationExistsAsync(tenantId, reg, cancellationToken))
            {
                skipped.Add(new BulkCreateVehicleSkipped(row, reg, "Registration already exists in this company."));
                continue;
            }

            if (options.DryRun)
            {
                created.Add(new BulkCreateVehicleSuccess(row, reg, 0, DryRun: true));
                continue;
            }

            try
            {
                var result = await mediator.Send(
                    new CreateVehicleCommand(dto with { RegistrationNumber = reg, Name = dto.Name.Trim() }),
                    cancellationToken);
                if (result.Success && result.Data > 0)
                    created.Add(new BulkCreateVehicleSuccess(row, reg, result.Data));
                else
                    errors.Add(new BulkCreateVehicleFailure(row, reg, result.Message ?? "Create failed."));
            }
            catch (ConflictException ex)
            {
                if (options.SkipDuplicates)
                    skipped.Add(new BulkCreateVehicleSkipped(row, reg, ex.Message));
                else
                    errors.Add(new BulkCreateVehicleFailure(row, reg, ex.Message));
            }
            catch (Exception ex)
            {
                errors.Add(new BulkCreateVehicleFailure(row, reg, ex.Message));
            }
        }

        return ApiResponse<BulkCreateVehiclesResult>.SuccessResponse(
            new BulkCreateVehiclesResult(
                created.Count(c => !c.DryRun),
                errors.Count,
                skipped.Count,
                options.DryRun,
                created,
                errors,
                skipped),
            options.DryRun ? "Dry run completed." : "Bulk vehicle import completed.");
    }
}
