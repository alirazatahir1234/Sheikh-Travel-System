using System.Text.RegularExpressions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.GpsControl;

public sealed class StatusGpsCommandResultParser : IGpsCommandResultParser
{
    public string ParserKey => "status";

    public IReadOnlyDictionary<string, string>? Parse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText)) return null;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(responseText, @"(\w+)\s*[:=]\s*([^;]+)"))
            map[m.Groups[1].Value.Trim()] = m.Groups[2].Value.Trim();
        return map.Count > 0 ? map : null;
    }
}

public sealed class VersionGpsCommandResultParser : IGpsCommandResultParser
{
    public string ParserKey => "version";

    public IReadOnlyDictionary<string, string>? Parse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText)) return null;
        var m = Regex.Match(responseText, @"VERSION\s*[:=]?\s*(.+)", RegexOptions.IgnoreCase);
        return m.Success
            ? new Dictionary<string, string> { ["FirmwareVersion"] = m.Groups[1].Value.Trim() }
            : new Dictionary<string, string> { ["Raw"] = responseText.Trim() };
    }
}

public sealed class IccidGpsCommandResultParser : IGpsCommandResultParser
{
    public string ParserKey => "iccid";

    public IReadOnlyDictionary<string, string>? Parse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText)) return null;
        var m = Regex.Match(responseText, @"ICCID\s*[:=]?\s*(\d+)", RegexOptions.IgnoreCase);
        return m.Success
            ? new Dictionary<string, string> { ["ICCID"] = m.Groups[1].Value }
            : new Dictionary<string, string> { ["Raw"] = responseText.Trim() };
    }
}

public sealed class ImsiGpsCommandResultParser : IGpsCommandResultParser
{
    public string ParserKey => "imsi";

    public IReadOnlyDictionary<string, string>? Parse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText)) return null;
        var m = Regex.Match(responseText, @"IMSI\s*[:=]?\s*(\d+)", RegexOptions.IgnoreCase);
        return m.Success
            ? new Dictionary<string, string> { ["IMSI"] = m.Groups[1].Value }
            : new Dictionary<string, string> { ["Raw"] = responseText.Trim() };
    }
}

public sealed class ParamGpsCommandResultParser : IGpsCommandResultParser
{
    public string ParserKey => "param";

    public IReadOnlyDictionary<string, string>? Parse(string responseText) =>
        new StatusGpsCommandResultParser().Parse(responseText);
}

public sealed class SignalGpsCommandResultParser : IGpsCommandResultParser
{
    public string ParserKey => "signal";

    public IReadOnlyDictionary<string, string>? Parse(string responseText) =>
        new StatusGpsCommandResultParser().Parse(responseText);
}

public sealed class GpsCommandResultParserRegistry(IEnumerable<IGpsCommandResultParser> parsers)
    : IGpsCommandResultParserRegistry
{
    public IReadOnlyDictionary<string, string>? Parse(string? parserKey, string responseText)
    {
        if (string.IsNullOrWhiteSpace(parserKey) || string.IsNullOrWhiteSpace(responseText))
            return null;
        var parser = parsers.FirstOrDefault(p =>
            string.Equals(p.ParserKey, parserKey, StringComparison.OrdinalIgnoreCase));
        return parser?.Parse(responseText);
    }
}
