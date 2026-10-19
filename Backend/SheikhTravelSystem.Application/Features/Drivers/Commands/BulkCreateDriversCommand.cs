using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record BulkCreateDriversOptions(bool DryRun = false, bool SkipDuplicates = true);

public record BulkCreateDriversCommand(
    IReadOnlyList<CreateDriverDto> Drivers,
    BulkCreateDriversOptions? Options = null)
    : IRequest<ApiResponse<BulkCreateDriversResult>>, IAuditableCommand
{
    public string AuditAction => "BulkCreate";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => null;
}

public record BulkCreateDriversResult(
    int Succeeded,
    int Failed,
    int Skipped,
    bool DryRun,
    IReadOnlyList<BulkCreateDriverSuccess> Created,
    IReadOnlyList<BulkCreateDriverFailure> Errors,
    IReadOnlyList<BulkCreateDriverSkipped> SkippedRows);

public record BulkCreateDriverSuccess(int Row, string Phone, int DriverId, bool DryRun = false);
public record BulkCreateDriverFailure(int Row, string? Phone, string Error);
public record BulkCreateDriverSkipped(int Row, string Phone, string Reason);

public class BulkCreateDriversCommandValidator : AbstractValidator<BulkCreateDriversCommand>
{
    public const int MaxBatchSize = 150;

    public BulkCreateDriversCommandValidator()
    {
        RuleFor(x => x.Drivers).NotNull().NotEmpty();
        RuleFor(x => x.Drivers.Count).LessThanOrEqualTo(MaxBatchSize)
            .WithMessage($"Bulk import supports at most {MaxBatchSize} drivers per request.");
    }
}

public class BulkCreateDriversCommandHandler(
    IMediator mediator,
    IDriverRepository driverRepository,
    ITenantContext tenantContext)
    : IRequestHandler<BulkCreateDriversCommand, ApiResponse<BulkCreateDriversResult>>
{
    public async Task<ApiResponse<BulkCreateDriversResult>> Handle(
        BulkCreateDriversCommand request,
        CancellationToken cancellationToken)
    {
        var options = request.Options ?? new BulkCreateDriversOptions();
        var created = new List<BulkCreateDriverSuccess>();
        var errors = new List<BulkCreateDriverFailure>();
        var skipped = new List<BulkCreateDriverSkipped>();
        var seenPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tenantId = tenantContext.GetRequiredTenantId();

        for (var i = 0; i < request.Drivers.Count; i++)
        {
            var row = i + 1;
            var dto = request.Drivers[i];
            var phone = dto.Phone?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(phone))
            {
                errors.Add(new BulkCreateDriverFailure(row, null, "Phone is required."));
                continue;
            }

            if (!seenPhones.Add(phone))
            {
                errors.Add(new BulkCreateDriverFailure(row, phone, "Duplicate phone in this import file."));
                continue;
            }

            if (options.SkipDuplicates)
            {
                var availability = await driverRepository.CheckAvailabilityAsync(
                    tenantId, phone, dto.Email, dto.LicenseNumber, null, cancellationToken);
                if (!availability.PhoneAvailable || !availability.LicenseAvailable
                    || (!string.IsNullOrWhiteSpace(dto.Email) && !availability.EmailAvailable))
                {
                    var reason = !availability.PhoneAvailable
                        ? "Phone already exists in this company."
                        : !availability.LicenseAvailable
                            ? "License number already exists in this company."
                            : "Email already exists in this company.";
                    skipped.Add(new BulkCreateDriverSkipped(row, phone, reason));
                    continue;
                }
            }

            if (options.DryRun)
            {
                created.Add(new BulkCreateDriverSuccess(row, phone, 0, DryRun: true));
                continue;
            }

            try
            {
                var normalized = dto with
                {
                    FirstName = dto.FirstName?.Trim() ?? "",
                    LastName = dto.LastName?.Trim() ?? "",
                    Phone = phone,
                    Email = dto.Email?.Trim(),
                    LicenseNumber = dto.LicenseNumber?.Trim() ?? ""
                };
                var result = await mediator.Send(new CreateDriverCommand(normalized), cancellationToken);
                if (result.Success && result.Data > 0)
                    created.Add(new BulkCreateDriverSuccess(row, phone, result.Data));
                else
                    errors.Add(new BulkCreateDriverFailure(row, phone, result.Message ?? "Create failed."));
            }
            catch (ConflictException ex)
            {
                if (options.SkipDuplicates)
                    skipped.Add(new BulkCreateDriverSkipped(row, phone, ex.Message));
                else
                    errors.Add(new BulkCreateDriverFailure(row, phone, ex.Message));
            }
            catch (Exception ex)
            {
                errors.Add(new BulkCreateDriverFailure(row, phone, ex.Message));
            }
        }

        return ApiResponse<BulkCreateDriversResult>.SuccessResponse(
            new BulkCreateDriversResult(
                created.Count(c => !c.DryRun),
                errors.Count,
                skipped.Count,
                options.DryRun,
                created,
                errors,
                skipped),
            options.DryRun ? "Dry run completed." : "Bulk driver import completed.");
    }
}
