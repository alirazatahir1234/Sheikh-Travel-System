namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>One observation of a tracker fix: the GPS time the tracker reported and when the server received it.</summary>
public readonly record struct TrackerClockSample(DateTime FixUtc, DateTime ServerUtc)
{
    public double LagMinutes => (ServerUtc - FixUtc).TotalMinutes;
}

/// <summary>
/// Detects trackers whose clock is a whole number of hours off (e.g. a JIMI VG03 set to the wrong
/// timezone reports fixTime 5h behind serverTime, so morning trips show as night driving).
///
/// Uses the minimum lag over distinct fixes: network delay, buffering and frozen keepalive fixes only
/// ever ADD lag, so the minimum is the best estimate of the clock error. One fresh fix with a correct
/// clock therefore drops the result to 0 immediately.
/// </summary>
public static class TrackerClockSkewDetector
{
    public const int MinSamples = 3;
    public const int MaxSkewHours = 14;
    public const double ToleranceMinutes = 10;
    /// <summary>Upper slack for "near the skew" — real fixes arrive seconds to a few minutes late.</summary>
    public const double LateSlackMinutes = 15;
    public static readonly TimeSpan MinServerSpan = TimeSpan.FromMinutes(10);
    /// <summary>Minimum lag (either sign) before a single sample is worth investigating.</summary>
    public const double SuspiciousLagMinutes = 50;

    /// <summary>Whole-hour clock error, or 0 when there is no clear evidence of one.</summary>
    public static int DetectSkewHours(IEnumerable<TrackerClockSample> samples) => Evaluate(samples) ?? 0;

    /// <summary>
    /// 0 = healthy (a fresh correct-clock fix is present), ±H = clock is H hours off,
    /// null = inconclusive (too few samples, backlog upload, parked) — callers keep their previous decision.
    /// </summary>
    public static int? Evaluate(IEnumerable<TrackerClockSample> samples)
    {
        // A frozen fix (parked keepalive) repeats the same FixUtc with later ServerUtc — keep first sighting only.
        var distinct = samples
            .GroupBy(s => s.FixUtc)
            .Select(g => g.MinBy(s => s.ServerUtc))
            .ToList();
        if (distinct.Count == 0)
            return null;

        var lags = distinct.Select(s => s.LagMinutes).OrderBy(l => l).ToList();
        var minLag = lags[0];

        // Any fresh correct-clock fix → healthy, regardless of how many samples we have
        // (works for trackers running ahead too, whose minimum lag stays negative).
        if (lags.Any(l => Math.Abs(l) < SuspiciousLagMinutes))
            return 0;

        if (distinct.Count < MinSamples)
            return null;
        if (distinct.Max(s => s.ServerUtc) - distinct.Min(s => s.ServerUtc) < MinServerSpan)
            return null;

        var hours = (int)Math.Round(minLag / 60.0, MidpointRounding.AwayFromZero);
        if (hours == 0 || Math.Abs(hours) > MaxSkewHours)
            return null;

        var expected = hours * 60.0;
        if (Math.Abs(minLag - expected) > ToleranceMinutes)
            return null;

        // A backlog upload has steadily shrinking lags; a skewed clock keeps them pinned near the skew.
        var near = lags.Count(l => l >= expected - ToleranceMinutes && l <= expected + LateSlackMinutes);
        return near * 3 >= lags.Count * 2 ? hours : null;
    }

    /// <summary>
    /// True when this row still carries the clock error. Rows recorded after the tracker was fixed
    /// (lag ≈ 0) are never shifted, even if the detected skew has not been re-evaluated yet.
    /// </summary>
    public static bool RowCarriesSkew(DateTime fixUtc, DateTime serverUtc, int skewHours)
    {
        if (skewHours == 0)
            return false;

        var lag = (serverUtc - fixUtc).TotalMinutes;
        var expected = skewHours * 60.0;
        return skewHours > 0
            ? lag >= expected - ToleranceMinutes
            : lag <= expected + ToleranceMinutes;
    }
}
