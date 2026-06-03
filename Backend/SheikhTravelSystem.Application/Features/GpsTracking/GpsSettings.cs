namespace SheikhTravelSystem.Application.Features.GpsTracking;

public class GpsSettings
{
    public const string SectionName = "GpsSettings";

    public int PositionRetentionDays { get; set; } = 90;
}
