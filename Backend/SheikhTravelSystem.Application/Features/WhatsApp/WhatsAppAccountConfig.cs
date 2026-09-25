using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp;

public interface IWhatsAppAccountConfig
{
    WhatsAppAccountDto Enrich(WhatsAppAccountDto account);
    WhatsAppAccountCredentials? ResolveCredentials(WhatsAppAccountRow account);
    WhatsAppAccountRow? ResolveAccountByPhoneNumberId(
        string phoneNumberId,
        IReadOnlyList<WhatsAppAccountRow> dbAccounts);
}

public sealed record WhatsAppAccountCredentials(
    string PhoneNumberId,
    string AccessToken,
    string? BusinessAccountId);

public sealed class WhatsAppAccountConfig(IOptions<WhatsAppOptions> options) : IWhatsAppAccountConfig
{
    public WhatsAppAccountDto Enrich(WhatsAppAccountDto account)
    {
        var cfg = FindConfig(account.Code);
        var phoneNumberId = FirstNonEmpty(cfg?.PhoneNumberId, account.PhoneNumberId);
        var hasToken = !string.IsNullOrWhiteSpace(cfg?.AccessToken);
        return account with
        {
            PhoneNumberId = phoneNumberId,
            HasAccessToken = hasToken
        };
    }

    public WhatsAppAccountCredentials? ResolveCredentials(WhatsAppAccountRow account)
    {
        var cfg = FindConfig(account.Code);
        var phoneNumberId = FirstNonEmpty(cfg?.PhoneNumberId, account.PhoneNumberId);
        var token = cfg?.AccessToken?.Trim();
        if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(token))
            return null;
        return new WhatsAppAccountCredentials(
            phoneNumberId,
            token,
            FirstNonEmpty(cfg?.EffectiveBusinessAccountId, account.BusinessAccountId));
    }

    public WhatsAppAccountRow? ResolveAccountByPhoneNumberId(
        string phoneNumberId,
        IReadOnlyList<WhatsAppAccountRow> dbAccounts)
    {
        if (string.IsNullOrWhiteSpace(phoneNumberId)) return null;
        foreach (var account in dbAccounts)
        {
            var cfg = FindConfig(account.Code);
            var id = FirstNonEmpty(cfg?.PhoneNumberId, account.PhoneNumberId);
            if (string.Equals(id, phoneNumberId, StringComparison.Ordinal))
                return account;
        }

        return dbAccounts.FirstOrDefault(a =>
            string.Equals(a.PhoneNumberId, phoneNumberId, StringComparison.Ordinal));
    }

    private WhatsAppAccountOptions? FindConfig(string code)
    {
        var opts = options.Value;
        var fromDict = opts.Accounts.TryGetValue(code, out var cfg) ? cfg : null;
        var named = ResolveNamedSection(code);

        if (fromDict is null && named is null)
            return null;
        if (fromDict is null)
            return named;
        if (named is null)
            return fromDict;

        // Merge: dictionary wins when set; otherwise named Uae/Pakistan section.
        return new WhatsAppAccountOptions
        {
            PhoneNumberId = FirstNonEmpty(fromDict.PhoneNumberId, named.PhoneNumberId) ?? "",
            AccessToken = FirstNonEmpty(fromDict.AccessToken, named.AccessToken) ?? "",
            BusinessAccountId = FirstNonEmpty(
                fromDict.EffectiveBusinessAccountId,
                named.EffectiveBusinessAccountId) ?? "",
            WabaId = FirstNonEmpty(fromDict.WabaId, named.WabaId) ?? ""
        };
    }

    private WhatsAppAccountOptions? ResolveNamedSection(string code)
    {
        if (string.Equals(code, "UAE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "Uae", StringComparison.OrdinalIgnoreCase))
            return HasAnyValue(options.Value.Uae) ? options.Value.Uae : null;

        if (string.Equals(code, "PK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "Pakistan", StringComparison.OrdinalIgnoreCase))
            return HasAnyValue(options.Value.Pakistan) ? options.Value.Pakistan : null;

        return null;
    }

    private static bool HasAnyValue(WhatsAppAccountOptions? o)
        => o is not null && (
            !string.IsNullOrWhiteSpace(o.PhoneNumberId)
            || !string.IsNullOrWhiteSpace(o.AccessToken)
            || !string.IsNullOrWhiteSpace(o.EffectiveBusinessAccountId));

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
