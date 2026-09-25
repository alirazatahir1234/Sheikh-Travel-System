using Microsoft.Extensions.Options;

namespace SheikhTravelSystem.Application.Features.WhatsApp;

public interface IWhatsAppRoutingService
{
    /// <summary>Resolve WhatsApp account Code (e.g. UAE, PK) from recipient phone.</summary>
    string ResolveAccountCode(string? recipientPhone);
}

public sealed class WhatsAppRoutingService(IOptions<WhatsAppRoutingOptions> options) : IWhatsAppRoutingService
{
    public string ResolveAccountCode(string? recipientPhone)
    {
        var digits = WhatsAppPhone.ToApiDigits(recipientPhone);
        var map = options.Value.CountryPrefixes;
        if (map is { Count: > 0 } && !string.IsNullOrEmpty(digits))
        {
            string? bestPrefix = null;
            string? bestCode = null;
            foreach (var (prefix, code) in map)
            {
                var p = new string((prefix ?? "").Where(char.IsDigit).ToArray());
                if (p.Length == 0 || string.IsNullOrWhiteSpace(code)) continue;
                if (!digits.StartsWith(p, StringComparison.Ordinal)) continue;
                if (bestPrefix is null || p.Length > bestPrefix.Length)
                {
                    bestPrefix = p;
                    bestCode = code.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(bestCode))
                return bestCode;
        }

        var fallback = options.Value.DefaultAccountCode?.Trim();
        return string.IsNullOrWhiteSpace(fallback) ? "UAE" : fallback;
    }
}
