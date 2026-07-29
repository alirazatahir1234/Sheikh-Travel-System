import 'dart:math' as math;

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
  var best = 0;
  var bestDiff = const Duration(days: 9999);
  for (var i = 0; i < playback.length; i++) {
    final diff = playback[i].timestamp.difference(time).abs();
    if (diff < bestDiff) {
      bestDiff = diff;
      best = i;
    }
  }
  return best;
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

String buildTripNarrative(HistoryReplayBundle bundle) {
  final stats = bundle.statistics;
  final summary = bundle.summary;
  final dist = bundle.mileageKm ??
      stats?.distanceKm ??
      summary?.distanceKm ??
      0;
  final driveMin = stats?.drivingMinutes ?? summary?.drivingMinutes ?? 0;
  final idle = stats?.idleMinutes ?? 0;
  final overs = stats?.overspeedCount ?? 0;
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
