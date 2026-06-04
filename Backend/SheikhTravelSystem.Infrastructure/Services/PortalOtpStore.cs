using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal;

namespace SheikhTravelSystem.Infrastructure.Services;

public sealed class PortalOtpStore(IOptions<PortalAuthSettings> options) : IPortalOtpService
{
    private readonly ConcurrentDictionary<string, OtpEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void Store(string phone, string code)
    {
        var expiry = DateTime.UtcNow.AddMinutes(Math.Max(1, options.Value.OtpExpiryMinutes));
        _entries[phone.Trim()] = new OtpEntry(code, expiry);
    }

    public bool TryValidate(string phone, string code, out string? error)
    {
        error = null;
        var key = phone.Trim();
        if (!_entries.TryGetValue(key, out var entry))
        {
            error = "No OTP requested for this phone. Send a code first.";
            return false;
        }

        if (entry.ExpiresAt < DateTime.UtcNow)
        {
            _entries.TryRemove(key, out _);
            error = "OTP expired. Request a new code.";
            return false;
        }

        if (!string.Equals(entry.Code, code.Trim(), StringComparison.Ordinal))
        {
            error = "Invalid OTP code.";
            return false;
        }

        _entries.TryRemove(key, out _);
        return true;
    }

    private sealed record OtpEntry(string Code, DateTime ExpiresAt);
}
