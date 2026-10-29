using System.Collections.Concurrent;
using DnsClient;
using DnsClient.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Notifications;

/// <summary>
/// Outcome of a raw MX DNS query (before caching / send policy).
/// </summary>
public enum MxDnsOutcome
{
    /// <summary>NOERROR — inspect <see cref="MxDnsLookupResult.Exchanges"/> (may be empty / null-MX only).</summary>
    Success,

    /// <summary>NXDOMAIN — domain does not exist.</summary>
    NxDomain,

    /// <summary>Timeout, SERVFAIL, network, or other uncertain failure.</summary>
    TransientFailure
}

/// <param name="Outcome">DNS result class.</param>
/// <param name="Exchanges">
/// MX exchange hostnames (empty string / "." already filtered out). Empty list means no usable MX.
/// </param>
/// <param name="Detail">Optional diagnostic text (RCODE / exception).</param>
public sealed record MxDnsLookupResult(
    MxDnsOutcome Outcome,
    IReadOnlyList<string> Exchanges,
    string? Detail = null);

/// <summary>Abstraction over MX DNS so policy + cache can be unit-tested without real DNS.</summary>
public interface IMxDnsLookup
{
    Task<MxDnsLookupResult> LookupMxAsync(string domain, CancellationToken cancellationToken = default);
}

/// <summary>DnsClient-backed MX lookup.</summary>
public sealed class DnsClientMxDnsLookup(ILookupClient dns) : IMxDnsLookup
{
    public async Task<MxDnsLookupResult> LookupMxAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await dns.QueryAsync(domain, QueryType.MX, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (response.HasError)
            {
                if (response.Header.ResponseCode == DnsHeaderResponseCode.NotExistentDomain)
                    return new MxDnsLookupResult(MxDnsOutcome.NxDomain, []);

                return new MxDnsLookupResult(
                    MxDnsOutcome.TransientFailure,
                    [],
                    response.Header.ResponseCode.ToString());
            }

            var exchanges = response.Answers
                .OfType<MxRecord>()
                .Select(r => r.Exchange?.Value?.Trim().TrimEnd('.') ?? "")
                .Where(x => x.Length > 0)
                .ToList();

            return new MxDnsLookupResult(MxDnsOutcome.Success, exchanges);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new MxDnsLookupResult(MxDnsOutcome.TransientFailure, [], ex.GetType().Name);
        }
    }
}

/// <summary>
/// MX-based recipient domain gate for outbound notification email.
/// <para>
/// <b>Policy</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Skip SMTP</b> when the domain has zero usable MX records, only a null MX
/// (<c>0 .</c> / RFC 7505), or NXDOMAIN — cached for the configured TTL (default 10 min).
/// </item>
/// <item>
/// <b>Allow SMTP</b> when at least one non-null MX exists — cached for the same TTL.
/// </item>
/// <item>
/// <b>Transient DNS</b> (timeout, SERVFAIL, network errors): log a warning and
/// <b>allow</b> the send once so flaky DNS does not silently drop real mail.
/// Transient outcomes are cached briefly (default 2 min) to limit DNS hammering.
/// </item>
/// </list>
/// Domains with no MX answer but a host A/AAAA are treated as not mailable (no implicit
/// A fallback) — matches bounce-prevention goals for parked / Afternic-style domains.
/// </summary>
public sealed class EmailRecipientMailabilityGuard : IEmailRecipientMailabilityGuard
{
    private readonly IMxDnsLookup _dns;
    private readonly ILogger<EmailRecipientMailabilityGuard> _logger;
    private readonly TimeSpan _positiveTtl;
    private readonly TimeSpan _negativeTtl;
    private readonly TimeSpan _transientTtl;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public EmailRecipientMailabilityGuard(
        IMxDnsLookup dns,
        IConfiguration configuration,
        ILogger<EmailRecipientMailabilityGuard> logger)
    {
        _dns = dns;
        _logger = logger;

        var section = configuration.GetSection("Notifications:Email:MxLookup");
        var cacheMinutes = section.GetValue("CacheMinutes", 10);
        if (cacheMinutes < 1) cacheMinutes = 1;
        if (cacheMinutes > 60) cacheMinutes = 60;
        _positiveTtl = TimeSpan.FromMinutes(cacheMinutes);
        _negativeTtl = _positiveTtl;

        var transientSeconds = section.GetValue("TransientCacheSeconds", 120);
        if (transientSeconds < 30) transientSeconds = 30;
        if (transientSeconds > 600) transientSeconds = 600;
        _transientTtl = TimeSpan.FromSeconds(transientSeconds);
    }

    public async Task<EmailMailabilityDecision> EvaluateAsync(
        string emailAddress,
        CancellationToken cancellationToken = default)
    {
        if (!TryExtractDomain(emailAddress, out var domain))
        {
            return new EmailMailabilityDecision(
                ShouldSend: false,
                Reason: "Invalid email address — cannot extract domain",
                Domain: null);
        }

        if (_cache.TryGetValue(domain, out var cached) && cached.ExpiresUtc > DateTime.UtcNow)
            return cached.Decision;

        var lookup = await _dns.LookupMxAsync(domain, cancellationToken).ConfigureAwait(false);

        EmailMailabilityDecision decision;
        TimeSpan ttl;

        switch (lookup.Outcome)
        {
            case MxDnsOutcome.NxDomain:
                decision = new EmailMailabilityDecision(
                    false,
                    $"Domain NXDOMAIN — no mail exchange for '{domain}'",
                    domain);
                ttl = _negativeTtl;
                break;

            case MxDnsOutcome.TransientFailure:
                decision = new EmailMailabilityDecision(
                    true,
                    $"DNS MX lookup failed ({lookup.Detail ?? "transient"}) for '{domain}' — allowing SMTP once",
                    domain,
                    IsTransientDnsUncertainty: true);
                ttl = _transientTtl;
                _logger.LogWarning(
                    "Transient DNS MX failure for {Domain}: {Detail}. Allowing SMTP attempt.",
                    domain, lookup.Detail);
                break;

            case MxDnsOutcome.Success when lookup.Exchanges.Count == 0:
                decision = new EmailMailabilityDecision(
                    false,
                    $"No usable MX for '{domain}' (missing or null MX / RFC 7505)",
                    domain);
                ttl = _negativeTtl;
                break;

            default:
                decision = new EmailMailabilityDecision(
                    true,
                    $"Usable MX found for '{domain}' ({lookup.Exchanges.Count})",
                    domain);
                ttl = _positiveTtl;
                break;
        }

        _cache[domain] = new CacheEntry(decision, DateTime.UtcNow.Add(ttl));
        return decision;
    }

    /// <summary>Exposes cache size for diagnostics / tests.</summary>
    internal int CachedDomainCount => _cache.Count;

    internal void ClearCache() => _cache.Clear();

    internal static bool TryExtractDomain(string? email, out string domain)
    {
        domain = "";
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var trimmed = email.Trim();
        var at = trimmed.LastIndexOf('@');
        if (at <= 0 || at >= trimmed.Length - 1)
            return false;

        var raw = trimmed[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant();
        if (raw.Length == 0 || raw.Contains(' ') || raw.Contains('@'))
            return false;

        // Reject IP-literal hosts for notification mail (rare; avoid DNS noise)
        if (raw.StartsWith('[') || System.Net.IPAddress.TryParse(raw, out _))
            return false;

        domain = raw;
        return true;
    }

    private sealed record CacheEntry(EmailMailabilityDecision Decision, DateTime ExpiresUtc);
}
