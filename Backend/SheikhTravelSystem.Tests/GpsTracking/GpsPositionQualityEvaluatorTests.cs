using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsPositionQualityEvaluatorTests
{
    private static readonly GpsPositionQualityOptions DefaultOptions = new();

    [Fact]
    public void Rejects_traccar_invalid_when_enabled()
    {
        var source = new PositionIngestSourceContext(TraccarValid: false);
        GpsPositionQualityEvaluator.ShouldRejectTraccarPosition(source, DefaultOptions).Should().BeTrue();
    }

    [Fact]
    public void Does_not_reject_invalid_without_source()
    {
        GpsPositionQualityEvaluator.ShouldRejectTraccarPosition(null, DefaultOptions).Should().BeFalse();
    }

    [Fact]
    public void Frozen_coordinates_after_streak_with_movement_speed()
    {
        var previous = new VehicleLocationSnapshot(
            12m, false, 31.601743, 74.298417, null, 2, true, DateTime.UtcNow.AddMinutes(-2));
        var dto = new IngestPositionDto(
            2, null, null, 1, 31.601743, 74.298417, 12m);

        var plan = GpsPositionQualityEvaluator.Evaluate(
            dto, previous, null, DefaultOptions, DateTime.UtcNow);

        plan.IsGpsValid.Should().BeFalse();
        plan.QualityCode.Should().Be(GpsPositionQualityEvaluator.QualityFrozenCoordinates);
        plan.UpdateVclCoordinates.Should().BeFalse();
        plan.UpdateVclKinematics.Should().BeFalse();
        plan.IdenticalCoordinateStreak.Should().Be(3);
    }

    [Fact]
    public void Slow_vehicle_at_rest_does_not_trigger_frozen()
    {
        var previous = new VehicleLocationSnapshot(
            0m, false, 31.601743, 74.298417, null, 5, true, DateTime.UtcNow.AddHours(-1));
        var dto = new IngestPositionDto(
            2, null, null, 1, 31.601743, 74.298417, 0m);

        var plan = GpsPositionQualityEvaluator.Evaluate(
            dto, previous, null, DefaultOptions, DateTime.UtcNow);

        plan.IsGpsValid.Should().BeTrue();
        plan.QualityCode.Should().BeNull();
    }

    [Fact]
    public void Impossible_teleport_flags_unreliable()
    {
        var previous = new VehicleLocationSnapshot(
            40m, true, 31.60, 74.30, null, 0, true, DateTime.UtcNow.AddSeconds(-30));
        var dto = new IngestPositionDto(
            2, null, null, 1, 32.10, 75.10, 40m);

        var plan = GpsPositionQualityEvaluator.Evaluate(
            dto, previous, null, DefaultOptions, DateTime.UtcNow);

        plan.IsGpsValid.Should().BeFalse();
        plan.QualityCode.Should().Be(GpsPositionQualityEvaluator.QualityImpossibleTeleport);
        plan.SkipMovementAlerts.Should().BeTrue();
    }

    [Fact]
    public void Coordinate_move_resets_streak()
    {
        var previous = new VehicleLocationSnapshot(
            12m, false, 31.601743, 74.298417, null, 4, false, DateTime.UtcNow.AddMinutes(-1));
        var dto = new IngestPositionDto(
            2, null, null, 1, 31.610000, 74.310000, 12m);

        var plan = GpsPositionQualityEvaluator.Evaluate(
            dto, previous, null, DefaultOptions, DateTime.UtcNow);

        plan.IsGpsValid.Should().BeTrue();
        plan.IdenticalCoordinateStreak.Should().Be(0);
    }

    [Fact]
    public void Lbs_detection_from_satellite_count()
    {
        GpsPositionQualityEvaluator.IsLbsOnlyFromTraccar(true, 0, 50).Should().BeTrue();
        GpsPositionQualityEvaluator.IsLbsOnlyFromTraccar(true, 8, 12).Should().BeFalse();
    }

    [Fact]
    public void Trustworthy_traccar_gnss_reacquisition_overrides_teleport()
    {
        var previous = new VehicleLocationSnapshot(
            12m, false, 31.601743, 74.298417, null, 0, false, DateTime.UtcNow.AddSeconds(-30));
        var dto = new IngestPositionDto(
            2, null, null, 1, 31.987900, 74.529500, 12m);
        var source = new PositionIngestSourceContext(
            TraccarValid: true, SatelliteCount: 8, LbsOnlyFix: false, FixTimeUtc: DateTime.UtcNow);

        var plan = GpsPositionQualityEvaluator.Evaluate(
            dto, previous, source, DefaultOptions, DateTime.UtcNow);

        plan.IsGpsValid.Should().BeTrue();
        plan.UpdateVclCoordinates.Should().BeTrue();
        plan.QualityCode.Should().BeNull();
    }

    [Fact]
    public void Unreliable_previous_allows_large_coordinate_recovery()
    {
        var previous = new VehicleLocationSnapshot(
            12m, false, 31.601743, 74.298417, null, 4, false, DateTime.UtcNow.AddSeconds(-10));
        var dto = new IngestPositionDto(
            2, null, null, 1, 31.987900, 74.529500, 0m);

        var plan = GpsPositionQualityEvaluator.Evaluate(
            dto, previous, null, DefaultOptions, DateTime.UtcNow);

        plan.IsGpsValid.Should().BeTrue();
        plan.UpdateVclCoordinates.Should().BeTrue();
        plan.IdenticalCoordinateStreak.Should().Be(0);
    }
}
