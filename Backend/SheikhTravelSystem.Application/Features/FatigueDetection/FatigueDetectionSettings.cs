namespace SheikhTravelSystem.Application.Features.FatigueDetection;

public class FatigueDetectionSettings
{
    public const string SectionName = "FatigueDetection";

    /// <summary>Background poll interval for ack timeout and auto-stop escalation.</summary>
    public int EscalationPollSeconds { get; set; } = 10;
}
