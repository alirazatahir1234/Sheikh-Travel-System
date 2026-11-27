using System.Text.RegularExpressions;
using FluentValidation;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Rules for one geofence payload (create, update and each imported row).</summary>
public sealed partial class SaveGeofenceDtoValidator : AbstractValidator<SaveGeofenceDto>
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    [GeneratedRegex("^([01][0-9]|2[0-3]):[0-5][0-9]$")]
    private static partial Regex HourMinute();

    public static bool IsHourMinute(string? value) => value is not null && HourMinute().IsMatch(value);

    public SaveGeofenceDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
            .WithMessage("Name is required (max 100 characters).");
        RuleFor(x => x.Type).MaximumLength(50);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Color).Must(c => c is null || HexColor().IsMatch(c))
            .WithMessage("Colour must be a #rrggbb value.");

        RuleFor(x => x.AreaType)
            .Must(t => (t ?? string.Empty).Trim().ToLowerInvariant() is "circle" or "polygon" or "rectangle" or "corridor")
            .WithMessage("Shape must be circle, polygon, rectangle or corridor.");
        RuleFor(x => x).Custom((dto, ctx) =>
        {
            if (!GpsGeoHelper.TryValidateGeofenceGeometry(
                    dto.AreaType, dto.RadiusMeters, dto.GeoJson, dto.CorridorWidthMeters, out var error) && error is not null)
                ctx.AddFailure(error);
        });
        RuleFor(x => x.BufferMeters).InclusiveBetween(0, 500)
            .WithMessage("GPS tolerance buffer must be between 0 and 500 m.");

        RuleFor(x => x.DwellMinutes).InclusiveBetween(1, 10_080).When(x => x.DwellMinutes.HasValue)
            .WithMessage("Dwell limit must be between 1 minute and 7 days.");
        RuleFor(x => x.IdleMinutes).InclusiveBetween(1, 1_440).When(x => x.IdleMinutes.HasValue)
            .WithMessage("Idle limit must be between 1 and 1440 minutes.");
        RuleFor(x => x.SpeedLimitKmh).InclusiveBetween(1m, 300m).When(x => x.SpeedLimitKmh.HasValue)
            .WithMessage("Speed limit must be between 1 and 300 km/h.");
        RuleFor(x => x.NotArrivedBy).Must(IsHourMinute).When(x => x.NotArrivedBy is not null)
            .WithMessage("Arrival deadline must be HH:mm.");
        RuleFor(x => x.CooldownMinutes).InclusiveBetween(1, 1_440)
            .WithMessage("Repeat window must be between 1 and 1440 minutes.");

        RuleFor(x => x.ScheduleDays).InclusiveBetween(1, GeofenceSchedule.AllDays).When(x => x.ScheduleDays.HasValue)
            .WithMessage("Pick at least one day for the schedule.");
        RuleFor(x => x.ScheduleStart).Must(IsHourMinute).When(x => x.ScheduleStart is not null)
            .WithMessage("Schedule start must be HH:mm.");
        RuleFor(x => x.ScheduleEnd).Must(IsHourMinute).When(x => x.ScheduleEnd is not null)
            .WithMessage("Schedule end must be HH:mm.");
        RuleFor(x => x).Must(x => (x.ScheduleStart is null) == (x.ScheduleEnd is null))
            .WithMessage("Set both schedule start and end, or neither.");
        RuleFor(x => x).Must(x => !x.AfterHoursExitCritical || x.ScheduleDays.HasValue)
            .WithMessage("Set a schedule to treat exits outside hours as critical.");

        RuleFor(x => x.AppliesTo).Must(a => GeofenceAppliesTo.Values.Contains(a))
            .WithMessage("Applies to must be all, groups, vehicles or drivers.");
        RuleFor(x => x.NotifyChannels)
            .Must(c => c is null || c.All(ch => GeofenceNotifyChannels.All.Contains(ch, StringComparer.OrdinalIgnoreCase)))
            .WithMessage("Unknown notify channel.");
    }
}
