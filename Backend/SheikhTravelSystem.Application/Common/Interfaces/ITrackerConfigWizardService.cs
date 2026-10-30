namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Multi-turn tracker registration wizard for SheikhGo AI Assistant.
/// Asks one field at a time, persists draft in AiPendingActions, registers on CONFIRM.
/// </summary>
public interface ITrackerConfigWizardService
{
    /// <summary>
    /// Continues an active wizard, starts a new one from trigger/IMEI, or executes CONFIRM.
    /// Returns null when the message is unrelated to tracker configuration.
    /// </summary>
    Task<TrackerConfigWizardTurn?> TryHandleAsync(
        Guid sessionId,
        int tenantId,
        int userId,
        string message,
        TrackerConfigWizardPermissions permissions,
        CancellationToken cancellationToken = default);

    Task ClearAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public record TrackerConfigWizardPermissions(
    bool CanExecuteWrite,
    bool CanManageDevices);

public record TrackerConfigWizardTurn(
    string Answer,
    string Mode,
    bool PendingConfirmation,
    AiPendingActionDto? PendingAction,
    string ToolUsed = "ConfigureTracker");
