namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// AI Operations permissions. Prefer fine-grained codes on endpoints;
/// <see cref="View"/> / <see cref="Manage"/> remain for catalog/UI and legacy grants
/// (authorization aliases map them onto finer codes).
/// </summary>
public static class AiPermissions
{
    public const string View = "Ai.View";
    public const string Manage = "Ai.Manage";
    public const string ExecuteWrite = "Ai.ExecuteWrite";

    public const string Chat = "Ai.Chat";
    public const string ViewPredictions = "Ai.ViewPredictions";
    public const string RunPredictions = "Ai.RunPredictions";
    public const string ViewRecommendations = "Ai.ViewRecommendations";
    public const string RefreshRecommendations = "Ai.RefreshRecommendations";
    public const string ViewProviderHealth = "Ai.ViewProviderHealth";

    /// <summary>Alias of <see cref="Manage"/> for provider config / admin AI surfaces.</summary>
    public const string ManageProviders = Manage;

    public static readonly string[] All =
    [
        View,
        Manage,
        ExecuteWrite,
        Chat,
        ViewPredictions,
        RunPredictions,
        ViewRecommendations,
        RefreshRecommendations,
        ViewProviderHealth
    ];

    /// <summary>Read-oriented operations implied by legacy <see cref="View"/>.</summary>
    public static readonly string[] ImpliedByView =
    [
        Chat,
        ViewPredictions,
        ViewRecommendations
    ];

    /// <summary>Mutation / admin operations implied by legacy <see cref="Manage"/>.</summary>
    public static readonly string[] ImpliedByManage =
    [
        RunPredictions,
        RefreshRecommendations,
        ViewProviderHealth,
        Chat,
        ViewPredictions,
        ViewRecommendations
    ];
}
