using System.Globalization;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Cuts a history window into trips and standstills, then rolls the same cut up per local day and
/// into period totals. The History workspace reads its timeline, its day rows and its KPI strip
/// from this one result, so they cannot disagree: totals are sums of days, days are sums of trips.
///
/// Rules (thresholds shared with <see cref="TripAnalyticsMapper"/>, <see cref="ReplayStopDetector"/>
/// and <see cref="HistoryReplayMapper"/>):
/// <list type="bullet">
/// <item>Moving is <see cref="TripAnalyticsMapper.IsMovingSegment"/>.</item>
/// <item>A standstill of <see cref="MinStopMinutes"/> or more ends a trip; a shorter one stays inside it.</item>
/// <item>A standstill of <see cref="ParkingMinutes"/> or more is parking, a shorter one is a stop.</item>
/// <item>Movement that never leaves <see cref="StopRadiusMeters"/> of where the vehicle stood is GPS jitter.</item>
/// <item>A silence longer than the moving-gap limit during which the vehicle was displaced is split
/// into standing plus an estimated drive. Only the estimate counts as driving, and it is flagged.</item>
/// <item>A trip is listed under, and counted in, the local day it starts.</item>
/// </list>
/// Feed it every fix of the window, before the route is thinned for drawing.
/// </summary>
public static class HistoryTimelineBuilder
{
    public const int MinStopMinutes = ReplayStopDetector.DefaultMinStopMinutes;
    public const double StopRadiusMeters = ReplayStopDetector.DefaultRadiusMeters;
    public const int ParkingMinutes = HistoryReplayMapper.ParkingStopMinutes;

    /// <summary>Speed assumed across a silence when neither bounding fix reports a usable speed.</summary>
    public const double DefaultNoSignalSpeedKmh = 30;
    public const double MinNoSignalSpeedKmh = 20;
    public const double MaxNoSignalSpeedKmh = 80;

    /// <summary>A jump faster than this between two fixes is a position glitch, not travel.</summary>
    public const double MaxPlausibleSpeedKmh = 160;

    /// <summary>
    /// Most uncached address lookups for one timeline. A cache miss waits for the free geocoder's
    /// rate limit (about one a second), so this bounds the first load of a long window. Answers
    /// from the address cache do not count, so later loads name the places left over.
    /// </summary>
    public const int MaxPlaceLookups = 40;

    /// <summary>A pause shorter than this inside a trip is not worth listing.</summary>
    public const int MinBriefStopSeconds = 60;

    /// <summary>After this many rejected fixes in a row the new position is accepted as real.</summary>
    private const int MaxConsecutiveGlitches = 3;

    private const string DateFormat = "yyyy-MM-dd";

    public static HistoryTimelineDto Build(
        IReadOnlyList<TripReplayPositionDto> points,
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo timeZone)
    {
        fromUtc = GpsUtcDateTime.AsUtc(fromUtc);
        toUtc = GpsUtcDateTime.AsUtc(toUtc);

        if (toUtc <= fromUtc)
        {
            return new HistoryTimelineDto(
                new HistoryTimelineTotalsDto(0m, 0, 0, 0, 0, 0, 0, 0m, 0m, 0),
                [],
                [],
                MinStopMinutes,
                ParkingMinutes);
        }

        var fixes = Normalize(points, fromUtc, toUtc);
        var runs = MergeRuns(BuildAtoms(fixes, fromUtc, toUtc));
        var segments = BuildSegments(runs, timeZone);
        var days = BuildDays(segments, fromUtc, toUtc, timeZone);
        var windowMinutes = Math.Max(1, (int)Math.Ceiling((toUtc - fromUtc).TotalMinutes));

        return new HistoryTimelineDto(
            BuildTotals(segments, days, windowMinutes),
            days,
            segments,
            MinStopMinutes,
            ParkingMinutes);
    }

    /// <summary>
    /// Names the places on the timeline. A place within <see cref="StopRadiusMeters"/> of one
    /// already named (the replay's stops, or an earlier standstill) takes that name; anything else
    /// is looked up once, cache first and never through a paid provider. After
    /// <see cref="MaxPlaceLookups"/> lookups that missed the cache, only cached answers are used,
    /// longest standstills first, because a miss waits for the free geocoder's rate limit; a place
    /// left unnamed keeps its coordinates until a later load. A trip takes the names of the
    /// standstills it leaves and reaches, so a trip row and the stop rows around it read the same.
    /// </summary>
    /// <param name="knownPlaces">Stops already named for this window; reused before any lookup.</param>
    public static async Task<HistoryTimelineDto> WithAddressesAsync(
        HistoryTimelineDto timeline,
        IReverseGeocodingService geocoder,
        CancellationToken cancellationToken,
        IReadOnlyList<TripStopDto>? knownPlaces = null)
    {
        var segments = timeline.Segments;
        if (segments.Count == 0) return timeline;

        var named = new List<(double Latitude, double Longitude, string Address)>();
        foreach (var place in knownPlaces ?? [])
        {
            if (!TripReplayAddressEnricher.IsCoarseAddress(place.Address))
                named.Add((place.Latitude, place.Longitude, place.Address!.Trim()));
        }

        string? Nearby(double latitude, double longitude)
        {
            foreach (var place in named)
            {
                if (TripAnalyticsMapper.HaversineKm(latitude, longitude, place.Latitude, place.Longitude) * 1000
                    <= StopRadiusMeters)
                    return place.Address;
            }
            return null;
        }

        // What needs a name: every standstill, and a trip end with no standstill beside it
        // (the edges of the window). The longest standstills are named first.
        var wanted = new List<(double Latitude, double Longitude, string? Address, int Weight)>();
        for (var i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            if (s.Kind != HistorySegmentKinds.Trip)
            {
                wanted.Add((s.StartLatitude, s.StartLongitude, s.StartAddress, s.DurationMinutes));
                continue;
            }

            if (StandstillBefore(segments, i) is null)
                wanted.Add((s.StartLatitude, s.StartLongitude, s.StartAddress, 0));
            if (StandstillAfter(segments, i) is null)
                wanted.Add((s.EndLatitude, s.EndLongitude, s.EndAddress, 0));
        }

        var misses = 0;
        foreach (var w in wanted.OrderByDescending(w => w.Weight))
        {
            if (Nearby(w.Latitude, w.Longitude) is not null) continue;
            if (!TripReplayAddressEnricher.IsCoarseAddress(w.Address))
            {
                named.Add((w.Latitude, w.Longitude, w.Address!.Trim()));
                continue;
            }
            if (misses >= MaxPlaceLookups) continue;

            var (address, fromCache) = await LookUpAsync(geocoder, w.Latitude, w.Longitude, cancellationToken);
            if (!fromCache) misses++;
            if (address is not null)
                named.Add((w.Latitude, w.Longitude, address));
        }

        var result = new List<HistoryTimelineSegmentDto>(segments.Count);
        foreach (var s in segments)
        {
            if (s.Kind == HistorySegmentKinds.Trip)
            {
                result.Add(s);
                continue;
            }

            var address = Nearby(s.StartLatitude, s.StartLongitude) ?? s.StartAddress;
            result.Add(s with { StartAddress = address, EndAddress = address });
        }

        for (var i = 0; i < result.Count; i++)
        {
            var s = result[i];
            if (s.Kind != HistorySegmentKinds.Trip) continue;

            result[i] = s with
            {
                StartAddress = StandstillBefore(result, i)?.StartAddress
                    ?? Nearby(s.StartLatitude, s.StartLongitude)
                    ?? s.StartAddress,
                EndAddress = StandstillAfter(result, i)?.StartAddress
                    ?? Nearby(s.EndLatitude, s.EndLongitude)
                    ?? s.EndAddress
            };
        }

        return timeline with { Segments = result };
    }

    /// <returns>The address, and whether the geocoder answered from its cache (no wait).</returns>
    private static async Task<(string? Address, bool FromCache)> LookUpAsync(
        IReverseGeocodingService geocoder,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await geocoder.GetAddressAsync(
                latitude,
                longitude,
                forceRefresh: false,
                cancellationToken,
                allowGoogle: false);
            return (TripReplayAddressEnricher.FormatResolvedAddress(result), result?.FromCache == true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return (null, false);
        }
    }

    // ---- fixes ----------------------------------------------------------------------------

    /// <summary>
    /// Fixes inside the window, in time order, one per instant, without null-island rows and
    /// without isolated jumps no vehicle can make.
    /// </summary>
    private static List<TripReplayPositionDto> Normalize(
        IReadOnlyList<TripReplayPositionDto> points,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var ordered = points
            .Select(p => p.Timestamp.Kind == DateTimeKind.Utc ? p : p with { Timestamp = GpsUtcDateTime.AsUtc(p.Timestamp) })
            .Where(p => p.Timestamp >= fromUtc && p.Timestamp <= toUtc)
            .Where(p => Math.Abs(p.Latitude) > 1e-6 || Math.Abs(p.Longitude) > 1e-6)
            .OrderBy(p => p.Timestamp)
            .ToList();

        var result = new List<TripReplayPositionDto>(ordered.Count);
        var rejected = 0;
        foreach (var p in ordered)
        {
            if (result.Count == 0)
            {
                result.Add(p);
                continue;
            }

            var last = result[^1];
            var hours = (p.Timestamp - last.Timestamp).TotalHours;
            if (hours <= 0) continue;

            var km = StraightKm(last, p);
            if (km * 1000 > StopRadiusMeters
                && km / hours > MaxPlausibleSpeedKmh
                && rejected < MaxConsecutiveGlitches)
            {
                rejected++;
                continue;
            }

            rejected = 0;
            result.Add(p);
        }

        return result;
    }

    // ---- atoms: one or two per pair of consecutive fixes ------------------------------------

    private enum AtomKind
    {
        Move,
        Stand
    }

    /// <param name="From">Where the interval starts. For a standstill, where the vehicle stands.</param>
    private sealed record Atom(
        AtomKind Kind,
        DateTime Start,
        DateTime End,
        TripReplayPositionDto From,
        TripReplayPositionDto To,
        double DistanceKm,
        bool NoSignal);

    private static Atom Standing(DateTime start, DateTime end, TripReplayPositionDto at) =>
        new(AtomKind.Stand, start, end, at, at, 0, false);

    private static bool IsMovingFix(TripReplayPositionDto p) =>
        p.SpeedKmh >= HistoryReplayMapper.MovingSpeedThresholdKmh;

    private static List<Atom> BuildAtoms(List<TripReplayPositionDto> fixes, DateTime fromUtc, DateTime toUtc)
    {
        var atoms = new List<Atom>(fixes.Count + 2);
        if (fixes.Count == 0) return atoms;

        // Before the first fix: a vehicle that reports standing, or reports nothing for longer
        // than the gap limit, was standing where it is first seen.
        var first = fixes[0];
        if (first.Timestamp > fromUtc
            && (!IsMovingFix(first) || (first.Timestamp - fromUtc).TotalMinutes > TripAnalyticsMapper.MaxMovingGapMinutes))
        {
            atoms.Add(Standing(fromUtc, first.Timestamp, first));
        }

        for (var i = 1; i < fixes.Count; i++)
        {
            var a = fixes[i - 1];
            var b = fixes[i];

            if (TripAnalyticsMapper.IsMovingSegment(a, b))
            {
                atoms.Add(new Atom(AtomKind.Move, a.Timestamp, b.Timestamp, a, b, PairDistanceKm(a, b), false));
                continue;
            }

            var silence = b.Timestamp - a.Timestamp;
            if (silence.TotalMinutes > TripAnalyticsMapper.MaxMovingGapMinutes
                && StraightKm(a, b) * 1000 > StopRadiusMeters)
            {
                AddSilentDisplacement(atoms, a, b, silence);
                continue;
            }

            atoms.Add(Standing(a.Timestamp, b.Timestamp, a));
        }

        // After the last fix: same reasoning — the vehicle is still standing where it was last seen.
        var last = fixes[^1];
        if (toUtc > last.Timestamp
            && (!IsMovingFix(last) || (toUtc - last.Timestamp).TotalMinutes > TripAnalyticsMapper.MaxMovingGapMinutes))
        {
            atoms.Add(Standing(last.Timestamp, toUtc, last));
        }

        return atoms;
    }

    /// <summary>
    /// No fix for longer than the gap limit, yet the vehicle is somewhere else. The whole silence is
    /// not driving (a tracker that sleeps while parked would turn every night into a trip), and it
    /// is not all standing either. Take the drive as distance ÷ the speed seen around the silence,
    /// and the rest as standing: at the far end when the vehicle was driving into the silence,
    /// otherwise at the near end.
    /// </summary>
    private static void AddSilentDisplacement(
        List<Atom> atoms,
        TripReplayPositionDto a,
        TripReplayPositionDto b,
        TimeSpan silence)
    {
        var seen = Math.Max((double)a.SpeedKmh, (double)b.SpeedKmh);
        var assumed = seen >= MinNoSignalSpeedKmh ? Math.Min(seen, MaxNoSignalSpeedKmh) : DefaultNoSignalSpeedKmh;
        var distanceKm = PairDistanceKm(a, b);

        var drive = TimeSpan.FromHours(distanceKm / assumed);
        if (drive < TimeSpan.FromMinutes(1)) drive = TimeSpan.FromMinutes(1);

        // Less than a minute left over is not a standstill: the vehicle drove through the silence.
        if (drive >= silence - TimeSpan.FromMinutes(1))
        {
            atoms.Add(new Atom(AtomKind.Move, a.Timestamp, b.Timestamp, a, b, distanceKm, true));
            return;
        }

        if (IsMovingFix(a) && !IsMovingFix(b))
        {
            var arrived = a.Timestamp + drive;
            atoms.Add(new Atom(AtomKind.Move, a.Timestamp, arrived, a, b, distanceKm, true));
            atoms.Add(Standing(arrived, b.Timestamp, b));
            return;
        }

        var departed = b.Timestamp - drive;
        atoms.Add(Standing(a.Timestamp, departed, a));
        atoms.Add(new Atom(AtomKind.Move, departed, b.Timestamp, a, b, distanceKm, true));
    }

    /// <summary>
    /// Odometer difference when both fixes carry one and it is plausible for the straight-line
    /// distance, otherwise the straight line. Never less than the straight line.
    /// </summary>
    private static double PairDistanceKm(TripReplayPositionDto a, TripReplayPositionDto b)
    {
        var straight = StraightKm(a, b);
        if (a.TotalDistanceKm is { } from && b.TotalDistanceKm is { } to)
        {
            var odometer = (double)(to - from);
            if (odometer >= 0 && odometer <= straight * 3 + 1)
                return Math.Max(odometer, straight);
        }

        return straight;
    }

    private static double StraightKm(TripReplayPositionDto a, TripReplayPositionDto b) =>
        TripAnalyticsMapper.HaversineKm(a.Latitude, a.Longitude, b.Latitude, b.Longitude);

    // ---- runs: consecutive atoms of one kind ------------------------------------------------

    private sealed class Run(AtomKind kind)
    {
        public AtomKind Kind { get; } = kind;
        public List<Atom> Atoms { get; } = [];
        public DateTime Start => Atoms[0].Start;
        public DateTime End => Atoms[^1].End;

        /// <summary>Where a standstill began.</summary>
        public TripReplayPositionDto Anchor => Atoms[0].From;
        public int Minutes => RoundMinutes(End - Start);
    }

    private static List<Run> MergeRuns(List<Atom> atoms)
    {
        var raw = new List<Run>();
        foreach (var atom in atoms)
        {
            if (atom.End <= atom.Start) continue;
            if (raw.Count == 0 || raw[^1].Kind != atom.Kind)
                raw.Add(new Run(atom.Kind));
            raw[^1].Atoms.Add(atom);
        }

        // Jitter: a moving run whose fixes all stay within the stop radius of the standstill beside
        // it is the same standstill.
        var runs = new List<Run>(raw.Count);
        for (var i = 0; i < raw.Count; i++)
        {
            var run = raw[i];
            if (run.Kind == AtomKind.Move)
            {
                var anchor = runs.Count > 0 && runs[^1].Kind == AtomKind.Stand
                    ? runs[^1].Anchor
                    : i + 1 < raw.Count && raw[i + 1].Kind == AtomKind.Stand
                        ? raw[i + 1].Anchor
                        : null;

                if (anchor is not null && StaysWithin(run, anchor))
                {
                    var standing = new Run(AtomKind.Stand);
                    standing.Atoms.AddRange(run.Atoms.Select(a => Standing(a.Start, a.End, anchor)));
                    run = standing;
                }
            }

            if (runs.Count > 0 && runs[^1].Kind == run.Kind)
                runs[^1].Atoms.AddRange(run.Atoms);
            else
                runs.Add(run);
        }

        return runs;
    }

    private static bool StaysWithin(Run run, TripReplayPositionDto anchor) =>
        run.Atoms.All(a =>
            !a.NoSignal
            && StraightKm(anchor, a.From) * 1000 <= StopRadiusMeters
            && StraightKm(anchor, a.To) * 1000 <= StopRadiusMeters);

    // ---- segments ---------------------------------------------------------------------------

    private static List<HistoryTimelineSegmentDto> BuildSegments(List<Run> runs, TimeZoneInfo timeZone)
    {
        var segments = new List<HistoryTimelineSegmentDto>();
        var trips = 0;
        var standstills = 0;

        var i = 0;
        while (i < runs.Count)
        {
            var run = runs[i];
            if (run.Kind == AtomKind.Stand)
            {
                // A short standstill that is not between two moving runs sits at the edge of the
                // window; it is left out rather than listed as a stop of unknown length.
                if (run.Minutes >= MinStopMinutes)
                    segments.Add(ToStandstill(run, ++standstills, timeZone));
                i++;
                continue;
            }

            // Runs alternate, so the trip takes every (short standstill, moving run) pair that follows.
            var moving = new List<Run> { run };
            var brief = new List<Run>();
            var j = i + 1;
            while (j + 1 < runs.Count
                   && runs[j].Kind == AtomKind.Stand
                   && runs[j].Minutes < MinStopMinutes
                   && runs[j + 1].Kind == AtomKind.Move)
            {
                brief.Add(runs[j]);
                moving.Add(runs[j + 1]);
                j += 2;
            }

            segments.Add(ToTrip(moving, brief, ++trips, timeZone));
            i = j;
        }

        return segments;
    }

    private static HistoryTimelineSegmentDto ToStandstill(Run run, int number, TimeZoneInfo timeZone)
    {
        var at = run.Anchor;
        return new HistoryTimelineSegmentDto(
            $"s{number}",
            run.Minutes >= ParkingMinutes ? HistorySegmentKinds.Parking : HistorySegmentKinds.Stop,
            LocalDate(run.Start, timeZone),
            run.Start,
            run.End,
            run.Minutes,
            null,
            null,
            at.Latitude,
            at.Longitude,
            at.Address,
            at.Latitude,
            at.Longitude,
            at.Address);
    }

    private static HistoryTimelineSegmentDto ToTrip(List<Run> moving, List<Run> brief, int number, TimeZoneInfo timeZone)
    {
        var atoms = moving.SelectMany(r => r.Atoms).ToList();
        var start = moving[0].Start;
        var end = moving[^1].End;
        var from = atoms[0].From;
        var to = atoms[^1].To;

        var noSignal = atoms
            .Where(a => a.NoSignal)
            .Select(a => new HistoryTimelineNoSignalDto(
                a.Start,
                a.End,
                a.From.Latitude,
                a.From.Longitude,
                a.To.Latitude,
                a.To.Longitude,
                RoundKm(a.DistanceKm)))
            .ToList();

        var briefStops = brief
            .Where(r => (r.End - r.Start).TotalSeconds >= MinBriefStopSeconds)
            .Select(r => new HistoryTimelineBriefStopDto(
                r.Start,
                r.End,
                (int)Math.Round((r.End - r.Start).TotalSeconds, MidpointRounding.AwayFromZero),
                r.Anchor.Latitude,
                r.Anchor.Longitude))
            .ToList();

        return new HistoryTimelineSegmentDto(
            $"t{number}",
            HistorySegmentKinds.Trip,
            LocalDate(start, timeZone),
            start,
            end,
            Math.Max(1, RoundMinutes(end - start)),
            RoundKm(atoms.Sum(a => a.DistanceKm)),
            Math.Round(atoms.Max(a => Math.Max(a.From.SpeedKmh, a.To.SpeedKmh)), 1),
            from.Latitude,
            from.Longitude,
            from.Address,
            to.Latitude,
            to.Longitude,
            to.Address,
            briefStops.Count > 0 ? briefStops : null,
            noSignal.Count > 0 ? noSignal : null);
    }

    // ---- days and totals: sums of the parts, never recomputed ---------------------------------

    private static List<HistoryTimelineDayDto> BuildDays(
        List<HistoryTimelineSegmentDto> segments,
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo timeZone)
    {
        var days = new List<HistoryTimelineDayDto>();
        var firstDay = TenantTimeZoneHelper.UtcToLocalDate(fromUtc, timeZone);
        var lastDay = TenantTimeZoneHelper.UtcToLocalDate(toUtc.AddTicks(-1), timeZone);
        var byDate = segments.ToLookup(s => s.Date);

        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            var start = LocalDayStartUtc(day, timeZone);
            var end = LocalDayStartUtc(day.AddDays(1), timeZone);
            var date = day.ToString(DateFormat, CultureInfo.InvariantCulture);
            var listed = byDate[date].ToList();
            var trips = listed.Where(s => s.Kind == HistorySegmentKinds.Trip).ToList();

            days.Add(new HistoryTimelineDayDto(
                date,
                start < fromUtc ? fromUtc : start,
                end > toUtc ? toUtc : end,
                trips.Sum(t => t.DistanceKm ?? 0m),
                trips.Sum(t => t.DurationMinutes),
                trips.Count,
                listed.Count - trips.Count,
                listed.Count(s => s.Kind == HistorySegmentKinds.Parking)));
        }

        return days;
    }

    private static HistoryTimelineTotalsDto BuildTotals(
        List<HistoryTimelineSegmentDto> segments,
        List<HistoryTimelineDayDto> days,
        int windowMinutes)
    {
        var distanceKm = days.Sum(d => d.DistanceKm);
        var drivingMinutes = days.Sum(d => d.DrivingMinutes);
        var trips = segments.Where(s => s.Kind == HistorySegmentKinds.Trip).ToList();
        var maxSpeed = trips.Count > 0 ? trips.Max(t => t.MaxSpeedKmh ?? 0m) : 0m;

        // Same reconciliation as the legacy strip: AVG SPEED = DISTANCE ÷ DRIVING, capped at max.
        var avgSpeed = drivingMinutes > 0
            ? Math.Round(distanceKm / (drivingMinutes / 60m), 1)
            : 0m;
        if (maxSpeed > 0m && avgSpeed > maxSpeed)
            avgSpeed = maxSpeed;

        var noSignalMinutes = RoundMinutes(TimeSpan.FromTicks(
            trips.SelectMany(t => t.NoSignal ?? []).Sum(n => (n.EndTime - n.StartTime).Ticks)));

        return new HistoryTimelineTotalsDto(
            distanceKm,
            drivingMinutes,
            Math.Max(0, windowMinutes - drivingMinutes),
            windowMinutes,
            days.Sum(d => d.TripCount),
            days.Sum(d => d.StopCount),
            days.Sum(d => d.ParkingCount),
            maxSpeed,
            avgSpeed,
            noSignalMinutes);
    }

    // ---- helpers ----------------------------------------------------------------------------

    private static HistoryTimelineSegmentDto? StandstillBefore(IReadOnlyList<HistoryTimelineSegmentDto> segments, int tripIndex)
    {
        if (tripIndex == 0) return null;
        var previous = segments[tripIndex - 1];
        return previous.Kind != HistorySegmentKinds.Trip && previous.EndTime == segments[tripIndex].StartTime
            ? previous
            : null;
    }

    private static HistoryTimelineSegmentDto? StandstillAfter(IReadOnlyList<HistoryTimelineSegmentDto> segments, int tripIndex)
    {
        if (tripIndex + 1 >= segments.Count) return null;
        var next = segments[tripIndex + 1];
        return next.Kind != HistorySegmentKinds.Trip && next.StartTime == segments[tripIndex].EndTime
            ? next
            : null;
    }

    private static int RoundMinutes(TimeSpan span) =>
        (int)Math.Round(span.TotalMinutes, MidpointRounding.AwayFromZero);

    private static decimal RoundKm(double km) =>
        Math.Round((decimal)km, 1, MidpointRounding.AwayFromZero);

    private static string LocalDate(DateTime utc, TimeZoneInfo timeZone) =>
        TenantTimeZoneHelper.UtcToLocalDate(utc, timeZone).ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>Local midnight as UTC; steps over a midnight that a DST change removed.</summary>
    private static DateTime LocalDayStartUtc(DateOnly day, TimeZoneInfo timeZone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        while (timeZone.IsInvalidTime(local))
            local = local.AddMinutes(30);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), DateTimeKind.Utc);
    }
}
