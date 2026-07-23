namespace SheikhTravelSystem.Application.Common;

public static class AiPermissions
{
    public const string View = "Ai.View";
    public const string Manage = "Ai.Manage";
    public const string ExecuteWrite = "Ai.ExecuteWrite";

    public static readonly string[] All =
    [
        View,
        Manage,
        ExecuteWrite
    ];
}
