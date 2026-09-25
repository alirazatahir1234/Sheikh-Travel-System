namespace SheikhTravelSystem.Infrastructure.Services.WhatsApp;

/// <summary>Meta webhook GET verification (hub.mode / hub.verify_token / hub.challenge).</summary>
public static class WhatsAppWebhookVerifier
{
    public static bool TryGetChallenge(
        string? mode,
        string? verifyToken,
        string? challenge,
        string? configuredVerifyToken,
        out string? challengeResponse)
    {
        challengeResponse = null;
        var expected = configuredVerifyToken?.Trim();
        if (!string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrEmpty(expected))
            return false;
        if (!string.Equals(verifyToken, expected, StringComparison.Ordinal))
            return false;
        if (string.IsNullOrEmpty(challenge))
            return false;

        challengeResponse = challenge;
        return true;
    }
}
