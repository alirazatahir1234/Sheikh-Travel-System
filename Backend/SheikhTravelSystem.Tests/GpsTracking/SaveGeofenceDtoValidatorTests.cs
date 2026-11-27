using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class SaveGeofenceDtoValidatorTests
{
    private static readonly SaveGeofenceDtoValidator Validator = new();

    private static SaveGeofenceDto Circle() => new()
    {
        Name = "Pasrur Head Office",
        Type = "Office",
        AreaType = "circle",
        CenterLat = 32.26,
        CenterLng = 74.66,
        RadiusMeters = 250,
        AppliesTo = GeofenceAppliesTo.Vehicles,
        CooldownMinutes = 15
    };

    private static IEnumerable<string> Errors(SaveGeofenceDto dto)
        => Validator.Validate(dto).Errors.Select(e => e.ErrorMessage);

    [Fact]
    public void A_plain_circle_is_valid()
        => Validator.Validate(Circle()).IsValid.Should().BeTrue(string.Join("; ", Errors(Circle())));

    [Fact]
    public void Corridor_needs_a_line_and_a_width()
    {
        var corridor = new SaveGeofenceDto
        {
            Name = "GT Road", AreaType = "corridor", AppliesTo = GeofenceAppliesTo.All, CooldownMinutes = 15,
            GeoJson = """{"type":"LineString","coordinates":[[74.30,31.50],[74.40,31.55]]}""",
            CorridorWidthMeters = 200
        };
        Validator.Validate(corridor).IsValid.Should().BeTrue(string.Join("; ", Errors(corridor)));

        Errors(new SaveGeofenceDto
        {
            Name = "GT Road", AreaType = "corridor", AppliesTo = GeofenceAppliesTo.All, CooldownMinutes = 15,
            GeoJson = corridor.GeoJson
        }).Should().Contain(e => e.Contains("width"));
    }

    [Fact]
    public void Times_must_be_hour_minute()
    {
        var dto = new SaveGeofenceDto
        {
            Name = "Depot", AreaType = "circle", CenterLat = 1, CenterLng = 1, RadiusMeters = 100,
            AppliesTo = GeofenceAppliesTo.All, CooldownMinutes = 15, NotArrivedBy = "9am"
        };
        Errors(dto).Should().Contain(e => e.Contains("HH:mm"));
    }

    [Fact]
    public void Schedule_needs_both_hours_and_after_hours_needs_a_schedule()
    {
        var oneSided = new SaveGeofenceDto
        {
            Name = "Depot", AreaType = "circle", CenterLat = 1, CenterLng = 1, RadiusMeters = 100,
            AppliesTo = GeofenceAppliesTo.All, CooldownMinutes = 15, ScheduleDays = 63, ScheduleStart = "08:00"
        };
        Errors(oneSided).Should().Contain(e => e.Contains("both schedule start and end"));

        var noSchedule = new SaveGeofenceDto
        {
            Name = "Depot", AreaType = "circle", CenterLat = 1, CenterLng = 1, RadiusMeters = 100,
            AppliesTo = GeofenceAppliesTo.All, CooldownMinutes = 15, AfterHoursExitCritical = true
        };
        Errors(noSchedule).Should().Contain(e => e.Contains("Set a schedule"));
    }

    [Fact]
    public void Unknown_channels_and_scopes_are_rejected()
    {
        var dto = new SaveGeofenceDto
        {
            Name = "Depot", AreaType = "circle", CenterLat = 1, CenterLng = 1, RadiusMeters = 100,
            AppliesTo = "everyone", CooldownMinutes = 15, NotifyChannels = ["Webhook"]
        };
        var errors = Errors(dto).ToList();
        errors.Should().Contain(e => e.Contains("Applies to"));
        errors.Should().Contain(e => e.Contains("notify channel"));
    }

    [Fact]
    public void Rule_thresholds_stay_in_range()
    {
        var dto = new SaveGeofenceDto
        {
            Name = "Depot", AreaType = "circle", CenterLat = 1, CenterLng = 1, RadiusMeters = 100,
            AppliesTo = GeofenceAppliesTo.All, CooldownMinutes = 0, DwellMinutes = 0, SpeedLimitKmh = 500,
            BufferMeters = 900, Color = "teal"
        };
        var errors = Errors(dto).ToList();
        errors.Should().Contain(e => e.Contains("Dwell"));
        errors.Should().Contain(e => e.Contains("Speed"));
        errors.Should().Contain(e => e.Contains("buffer"));
        errors.Should().Contain(e => e.Contains("Repeat window"));
        errors.Should().Contain(e => e.Contains("#rrggbb"));
    }
}
