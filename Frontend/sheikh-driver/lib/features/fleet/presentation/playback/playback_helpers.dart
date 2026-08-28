import 'dart:math' as math;
import 'package:intl/intl.dart';

import '../../domain/fleet_models.dart';

const defaultOverspeedKmh = 90.0;

enum TripEventFilter {
  all,
  stops,
  overspeed,
  fuel,
  sos,
  ignition,
  geofence,
}

enum PlaybackSegmentKind {
  normal,
  overspeed,
  ignitionOff,
  stop,
}

class PlaybackPolylineSegment {
  const PlaybackPolylineSegment({
    required this.points,
    required this.kind,
  });

  final List<HistoryReplayPoint> points;
  final PlaybackSegmentKind kind;
}

int indexForTimestamp(List<HistoryReplayPoint> playback, DateTime time) {
  if (playback.isEmpty) return 0;
  if (playback.length == 1) return 0;

  var low = 0;
  var high = playback.length - 1;
  while (low <= high) {
    final mid = (low + high) >> 1;
    final ts = playback[mid].timestamp;
    if (ts.isAtSameMomentAs(time)) {
      return mid;
    }
    if (ts.isBefore(time)) {
      low = mid + 1;
    } else {
      high = mid - 1;
    }
  }

  final right = low.clamp(0, playback.length - 1);
  final left = (low - 1).clamp(0, playback.length - 1);
  final leftDiff = playback[left].timestamp.difference(time).abs();
  final rightDiff = playback[right].timestamp.difference(time).abs();
  return leftDiff <= rightDiff ? left : right;
}

int lowerBoundPlaybackIndex(List<HistoryReplayPoint> playback, DateTime time) {
  if (playback.isEmpty) return 0;
  var low = 0;
  var high = playback.length;
  while (low < high) {
    final mid = (low + high) >> 1;
    if (playback[mid].timestamp.isBefore(time)) {
      low = mid + 1;
    } else {
      high = mid;
    }
  }
  return low.clamp(0, playback.length - 1);
}

String headingToCardinal(double? degrees) {
  if (degrees == null) return '—';
  final d = (degrees % 360 + 360) % 360;
  const dirs = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW'];
  final idx = ((d + 22.5) / 45).floor() % 8;
  return dirs[idx];
}

bool _isOverspeedType(String type) {
  final t = type.toLowerCase();
  return t.contains('overspeed') ||
      t.contains('speed_exceeded') ||
      t.contains('speed');
}

bool _isFuelType(String type) {
  final t = type.toLowerCase();
  return t.contains('fuel');
}

bool _isSosType(String type) {
  final t = type.toLowerCase();
  return t.contains('sos') || t.contains('panic');
}

bool _isIgnitionType(String type) {
  final t = type.toLowerCase();
  return t.contains('ignition');
}

bool _isGeofenceType(String type) {
  final t = type.toLowerCase();
  return t.contains('geofence');
}

List<TripEvent> filterEvents(List<TripEvent> events, TripEventFilter filter) {
  if (filter == TripEventFilter.all) return events;
  return events.where((e) {
    final t = e.type;
    switch (filter) {
      case TripEventFilter.stops:
        return t.toLowerCase().contains('stop');
      case TripEventFilter.overspeed:
        return _isOverspeedType(t);
      case TripEventFilter.fuel:
        return _isFuelType(t);
      case TripEventFilter.sos:
        return _isSosType(t);
      case TripEventFilter.ignition:
        return _isIgnitionType(t);
      case TripEventFilter.geofence:
        return _isGeofenceType(t);
      case TripEventFilter.all:
        return true;
    }
  }).toList();
}

List<int> timelineMarkerIndices({
  required List<HistoryReplayPoint> playback,
  required List<TripStop> stops,
  required List<TripEvent> events,
}) {
  final indices = <int>{};
  for (final s in stops) {
    indices.add(indexForTimestamp(playback, s.startTime));
  }
  for (final e in events) {
    indices.add(indexForTimestamp(playback, e.time));
  }
  final sorted = indices.toList()..sort();
  return sorted;
}

List<int> eventMarkerIndices(
  List<HistoryReplayPoint> playback,
  List<TripEvent> events,
) {
  return events
      .map((e) => indexForTimestamp(playback, e.time))
      .toSet()
      .toList()
    ..sort();
}

PlaybackSegmentKind classifyPoint(
  HistoryReplayPoint point, {
  double overspeedKmh = defaultOverspeedKmh,
  bool nearStop = false,
}) {
  if (nearStop) return PlaybackSegmentKind.stop;
  if (point.ignition == false) return PlaybackSegmentKind.ignitionOff;
  if (point.speedKmh >= overspeedKmh) return PlaybackSegmentKind.overspeed;
  return PlaybackSegmentKind.normal;
}

List<PlaybackPolylineSegment> buildSpeedSegments(
  List<HistoryReplayPoint> points, {
  double overspeedKmh = defaultOverspeedKmh,
  List<TripStop> stops = const [],
}) {
  if (points.length < 2) return const [];

  bool nearStop(HistoryReplayPoint p) {
    for (final s in stops) {
      if (p.timestamp.isAfter(s.startTime.subtract(const Duration(minutes: 2))) &&
          p.timestamp.isBefore(s.endTime.add(const Duration(minutes: 2)))) {
        return true;
      }
    }
    return false;
  }

  final segments = <PlaybackPolylineSegment>[];
  var currentKind = classifyPoint(
    points.first,
    overspeedKmh: overspeedKmh,
    nearStop: nearStop(points.first),
  );
  var bucket = <HistoryReplayPoint>[points.first];

  for (var i = 1; i < points.length; i++) {
    final p = points[i];
    final kind = classifyPoint(
      p,
      overspeedKmh: overspeedKmh,
      nearStop: nearStop(p),
    );
    if (kind == currentKind) {
      bucket.add(p);
    } else {
      if (bucket.length >= 2) {
        segments.add(PlaybackPolylineSegment(points: List.of(bucket), kind: currentKind));
      }
      currentKind = kind;
      bucket = [points[i - 1], p];
    }
  }
  if (bucket.length >= 2) {
    segments.add(PlaybackPolylineSegment(points: bucket, kind: currentKind));
  }
  return segments;
}

/// Map playback scrubber index to trail index when route is denser than playback.
int trailIndexForPlaybackIndex(
  List<HistoryReplayPoint> trail,
  List<HistoryReplayPoint> playback,
  int playbackIndex,
) {
  if (trail.isEmpty || playback.isEmpty) return 0;
  if (trail == playback || trail.length == playback.length) {
    return playbackIndex.clamp(0, trail.length - 1);
  }
  final t = playback[playbackIndex.clamp(0, playback.length - 1)].timestamp;
  return indexForTimestamp(trail, t);
}

double distanceAlongTrailKm(List<HistoryReplayPoint> trail, int trailIndex) {
  if (trail.isEmpty || trailIndex <= 0) return 0;
  final end = trailIndex.clamp(0, trail.length - 1);
  double sum = 0;
  for (var i = 1; i <= end; i++) {
    sum += segmentDistanceKm(trail[i - 1], trail[i]);
  }
  return sum;
}

/// Single-segment distance for incremental `_distSoFar` during playback.
double segmentDistanceKm(HistoryReplayPoint a, HistoryReplayPoint b) {
  return _haversineKm(a.latitude, a.longitude, b.latitude, b.longitude);
}

double _haversineKm(double lat1, double lon1, double lat2, double lon2) {
  const r = 6371;
  final dLat = _deg2rad(lat2 - lat1);
  final dLon = _deg2rad(lon2 - lon1);
  final a = math.sin(dLat / 2) * math.sin(dLat / 2) +
      math.cos(_deg2rad(lat1)) *
          math.cos(_deg2rad(lat2)) *
          math.sin(dLon / 2) *
          math.sin(dLon / 2);
  final c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a));
  return r * c;
}

double _deg2rad(double deg) => deg * (math.pi / 180);

/// Human-readable duration from whole minutes (e.g. `14 min`, `2 hr 14 min`).
String formatDurationMinutes(int minutes) {
  if (minutes <= 0) return '0 min';
  if (minutes < 60) return '$minutes min';
  final h = minutes ~/ 60;
  final m = minutes % 60;
  if (m == 0) return '$h hr';
  return '$h hr $m min';
}

String formatDurationMinutesCompact(int minutes) {
  if (minutes <= 0) return '0m';
  if (minutes < 60) return '${minutes}m';
  final h = minutes ~/ 60;
  final m = minutes % 60;
  if (m == 0) return '${h}h';
  return '${h}h ${m}m';
}

String formatStopWindow({
  required DateTime startTime,
  required DateTime endTime,
}) {
  final start = startTime.toLocal();
  final end = endTime.toLocal();
  final dayFmt = DateFormat('MMM d');
  final timeFmt = DateFormat('h:mm a');
  final startDay = dayFmt.format(start);
  final startClock = timeFmt.format(start);
  if (!end.isAfter(start)) return '$startDay · $startClock';
  final endClock = timeFmt.format(end);
  if (start.year == end.year &&
      start.month == end.month &&
      start.day == end.day) {
    return '$startDay · $startClock – $endClock';
  }
  final endDay = dayFmt.format(end);
  return '$startDay $startClock – $endDay $endClock';
}

/// Human-readable duration from seconds (e.g. `14 min 20 sec`, `2 hr 14 min`).
String formatDurationSeconds(int totalSeconds) {
  if (totalSeconds <= 0) return '0 sec';
  final h = totalSeconds ~/ 3600;
  final m = (totalSeconds % 3600) ~/ 60;
  final s = totalSeconds % 60;
  if (h > 0) {
    if (m == 0) return '$h hr';
    return '$h hr $m min';
  }
  if (m > 0) {
    if (s == 0) return '$m min';
    return '$m min $s sec';
  }
  return '$s sec';
}

String formatDistanceKm(double km) {
  if (km <= 0) return '0 km';
  if (km < 10) return '${km.toStringAsFixed(2)} km';
  return '${km.toStringAsFixed(1)} km';
}

String buildGpx(List<HistoryReplayPoint> route, {String? name}) {
  final title = name ?? 'SheikhGo replay';
  final lines = <String>[
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<gpx version="1.1" creator="SheikhGo">',
    '<trk>',
    '<name>$title</name>',
    '<trkseg>',
  ];
  for (final p in route) {
    final ts = p.timestamp.toUtc().toIso8601String();
    lines.add(
      '<trkpt lat="${p.latitude}" lon="${p.longitude}">'
      '<time>$ts</time>'
      '<extensions><speed>${p.speedKmh}</speed></extensions>'
      '</trkpt>',
    );
  }
  lines.addAll(['</trkseg>', '</trk>', '</gpx>']);
  return lines.join('\n');
}

String buildCsv(List<HistoryReplayPoint> route) {
  final lines = <String>[
    'timestamp,latitude,longitude,speed_kmh,heading,ignition,address',
  ];
  for (final p in route) {
    final safeAddress =
        (p.address ?? '').replaceAll('"', '""').replaceAll('\n', ' ');
    lines.add(
      '"${p.timestamp.toUtc().toIso8601String()}",'
      '${p.latitude},'
      '${p.longitude},'
      '${p.speedKmh.toStringAsFixed(2)},'
      '${p.heading?.toStringAsFixed(1) ?? ''},'
      '${p.ignition == null ? '' : (p.ignition! ? 'on' : 'off')},'
      '"$safeAddress"',
    );
  }
  return lines.join('\n');
}

String buildKml(List<HistoryReplayPoint> route, {String? name}) {
  final title = (name ?? 'SheikhGo replay')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;');
  final coords = route
      .map((p) => '${p.longitude},${p.latitude},0')
      .join(' ');
  return '''<?xml version="1.0" encoding="UTF-8"?>
<kml xmlns="http://www.opengis.net/kml/2.2">
  <Document>
    <name>$title</name>
    <Placemark>
      <name>$title</name>
      <Style>
        <LineStyle><color>ffed4d1d</color><width>4</width></LineStyle>
      </Style>
      <LineString>
        <tessellate>1</tessellate>
        <coordinates>$coords</coordinates>
      </LineString>
    </Placemark>
  </Document>
</kml>''';
}

String buildTripNarrative(HistoryReplayBundle bundle) {
  final stats = PlaybackStats.fromBundle(bundle);
  final dist = stats.distanceKm;
  final driveMin = stats.drivingMinutes;
  final idle = stats.idleMinutes;
  final overs = stats.overspeedCount;
  final stops = bundle.stops.length;
  final geofence = bundle.events
      .where((e) => e.type.toLowerCase().contains('geofence'))
      .length;
  return 'Vehicle traveled ${dist.toStringAsFixed(1)} km in '
      '${driveMin ~/ 60}h ${driveMin % 60}m, '
      '${overs > 0 ? "exceeded speed threshold $overs time(s), " : ""}'
      'idled $idle min, '
      '$stops stop(s)'
      '${geofence > 0 ? ", $geofence geofence event(s)" : ""}.';
}

/// Points used for ticker animation. Prefers dense [HistoryReplayBundle.route]
/// when playback is empty/sparse so the marker follows the drawn polyline.
List<HistoryReplayPoint> effectivePlaybackPoints(HistoryReplayBundle bundle) {
  final route = bundle.route;
  final playback = bundle.playback;
  if (route.length >= 2 &&
      (playback.length < 2 || route.length >= playback.length * 2)) {
    return route;
  }
  return bundle.points;
}

/// Prefers API mileage when present; otherwise integrates the trail polyline.
double effectiveDistanceKm(HistoryReplayBundle bundle) {
  final fromApi = bundle.mileageKm ??
      bundle.statistics?.distanceKm ??
      bundle.summary?.distanceKm;
  if (fromApi != null && fromApi > 0) return fromApi;
  final trail =
      bundle.trailPoints.isNotEmpty ? bundle.trailPoints : bundle.points;
  if (trail.isEmpty) return 0;
  return distanceAlongTrailKm(trail, trail.length - 1);
}

/// Uses backend driving minutes when plausible, otherwise derives a motion-based
/// driving duration from replay points to avoid inflated 24h-style values.
int effectiveDrivingMinutes(HistoryReplayBundle bundle) {
  final stats = bundle.statistics;
  final summary = bundle.summary;
  final serverMinutes = stats?.drivingMinutes ?? summary?.drivingMinutes ?? 0;
  final points = bundle.trailPoints.isNotEmpty ? bundle.trailPoints : bundle.points;
  final derivedMinutes = _deriveDrivingMinutesFromPoints(points);
  if (derivedMinutes <= 0) return serverMinutes;
  if (serverMinutes <= 0) return derivedMinutes;

  final distanceKm = bundle.mileageKm ??
      stats?.distanceKm ??
      summary?.distanceKm ??
      0;

  // Guardrail: short-distance trips should not report near-day driving windows.
  if (distanceKm <= 10 && serverMinutes > (derivedMinutes * 3)) {
    return derivedMinutes;
  }
  // General sanity: if backend value is dramatically larger than point-derived
  // movement time, prefer point-derived.
  if (serverMinutes > (derivedMinutes * 4)) {
    return derivedMinutes;
  }
  return serverMinutes;
}

int _deriveDrivingMinutesFromPoints(List<HistoryReplayPoint> points) {
  if (points.length < 2) return 0;
  var movingMinutes = 0.0;

  for (var i = 1; i < points.length; i++) {
    final a = points[i - 1];
    final b = points[i];
    final delta = b.timestamp.difference(a.timestamp).inSeconds;
    if (delta <= 0) continue;
    if (delta > 20 * 60) continue;

    final segKm = segmentDistanceKm(a, b);
    final movingBySpeed = a.speedKmh >= 3 || b.speedKmh >= 3;
    final movingByIgnition =
        (a.ignition == true || b.ignition == true) &&
        (a.speedKmh > 0 || b.speedKmh > 0);
    final movingByDistance = segKm >= 0.03;

    if (movingBySpeed || movingByIgnition || movingByDistance) {
      movingMinutes += delta / 60.0;
    }
  }

  return movingMinutes.round();
}

class PlaybackStats {
  const PlaybackStats({
    required this.distanceKm,
    required this.drivingMinutes,
    required this.idleMinutes,
    required this.stopCount,
    required this.avgSpeedKmh,
    required this.maxSpeedKmh,
    required this.overspeedCount,
    required this.sosCount,
    required this.fuelEvents,
    required this.ignitionEvents,
  });

  final double distanceKm;
  final int drivingMinutes;
  final int idleMinutes;
  final int stopCount;
  final double avgSpeedKmh;
  final double maxSpeedKmh;
  final int overspeedCount;
  final int sosCount;
  final int fuelEvents;
  final int ignitionEvents;

  factory PlaybackStats.fromBundle(HistoryReplayBundle bundle) {
    final summary = bundle.summary;
    final stats = bundle.statistics;
    final events = bundle.events;
    final stopCount = stats?.stopCount ?? bundle.stops.length;
    final overspeed = stats?.overspeedCount ??
        events.where((e) => e.type.toLowerCase().contains('speed')).length;
    final sos = events
        .where((e) => e.type.toLowerCase().contains('sos'))
        .length;
    final fuel = events
        .where((e) => e.type.toLowerCase().contains('fuel'))
        .length;
    final ignition = events
        .where((e) => e.type.toLowerCase().contains('ignition'))
        .length;
    return PlaybackStats(
      distanceKm: bundle.mileageKm ?? stats?.distanceKm ?? summary?.distanceKm ?? 0,
      drivingMinutes: effectiveDrivingMinutes(bundle),
      idleMinutes: stats?.idleMinutes ?? 0,
      stopCount: stopCount,
      avgSpeedKmh: stats?.avgSpeedKmh ?? summary?.avgSpeedKmh ?? 0,
      maxSpeedKmh: stats?.maxSpeedKmh ?? summary?.maxSpeedKmh ?? 0,
      overspeedCount: overspeed,
      sosCount: sos,
      fuelEvents: fuel,
      ignitionEvents: ignition,
    );
  }
}
