namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Central gate for whether an email address's domain can receive mail (usable MX).
/// Used by the SMTP notification channel so all notification emails share one policy.
/// </summary>
public interface IEmailRecipientMailabilityGuard
{
    /// <summary>
    /// Evaluates whether SMTP should be attempted. Never throws for DNS/policy reasons.
    /// </summary>
    Task<EmailMailabilityDecision> EvaluateAsync(
        string emailAddress,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Policy outcome for recipient domain MX checks.
/// </summary>
/// <param name="ShouldSend">
/// True when SMTP may proceed. False when the domain clearly cannot receive mail
/// (no usable MX, null MX / RFC 7505, NXDOMAIN). Transient DNS failures return
/// <c>ShouldSend=true</c> so legitimate mail is not blocked by flaky DNS; a warning is logged.
/// </param>
/// <param name="Reason">Short machine-readable reason for logs / delivery detail.</param>
/// <param name="Domain">Normalized domain when parsed from the address.</param>
/// <param name="IsTransientDnsUncertainty">
/// True when DNS could not be confirmed and send was allowed despite uncertainty.
/// </param>
public sealed record EmailMailabilityDecision(
    bool ShouldSend,
    string Reason,
    string? Domain = null,
    bool IsTransientDnsUncertainty = false);
