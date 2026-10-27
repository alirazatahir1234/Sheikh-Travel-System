/// Driving route geometry from the backend planned-route proxy
/// (or a one-shot Routes API fallback). Numbers come only from that response.
class TripDrivingRoute {
  const TripDrivingRoute({
    required this.encodedPolyline,
    required this.distanceMeters,
    required this.durationSeconds,
    required this.points,
    this.staticDurationSeconds,
    this.trafficLevel,
    this.routingSource,
  });

  final String encodedPolyline;
  final int distanceMeters;
  final int durationSeconds;
  final List<RoutePoint> points;
  final int? staticDurationSeconds;
  final String? trafficLevel;
  final String? routingSource;

  String? get summary => formatDrivingRouteSummary(
        distanceMeters: distanceMeters,
        durationSeconds: durationSeconds,
        trafficLevel: trafficLevel,
      );
}

class RoutePoint {
  const RoutePoint(this.latitude, this.longitude);

  final double latitude;
  final double longitude;
}

/// ~11 m buckets so a GPS jitter on the same pickup/drop does not refetch.
String tripRouteCacheKey(
  double originLat,
  double originLng,
  double destLat,
  double destLng,
) {
  String round(double value) => value.toStringAsFixed(4);
  return '${round(originLat)},${round(originLng)}|${round(destLat)},${round(destLng)}';
}

/// Google encoded polyline, precision 1e5.
List<RoutePoint> decodeEncodedPolyline(String encoded) {
  final points = <RoutePoint>[];
  var index = 0;
  var lat = 0;
  var lng = 0;

  while (index < encoded.length) {
    final latDelta = _decodeChunk(encoded, index);
    index = latDelta.next;
    lat += latDelta.value;

    if (index >= encoded.length) break;
    final lngDelta = _decodeChunk(encoded, index);
    index = lngDelta.next;
    lng += lngDelta.value;

    points.add(RoutePoint(lat / 1e5, lng / 1e5));
  }
  return points;
}

class _Chunk {
  const _Chunk(this.value, this.next);
  final int value;
  final int next;
}

_Chunk _decodeChunk(String encoded, int index) {
  var shift = 0;
  var result = 0;
  int byte;
  do {
    if (index >= encoded.length) {
      return _Chunk(0, index);
    }
    byte = encoded.codeUnitAt(index++) - 63;
    result |= (byte & 0x1f) << shift;
    shift += 5;
  } while (byte >= 0x20);
  final value = (result & 1) != 0 ? ~(result >> 1) : (result >> 1);
  return _Chunk(value, index);
}

/// "2 h 10 min · 178 km". Null when the API sent no duration and no distance.
String? formatDrivingRouteSummary({
  required int distanceMeters,
  required int durationSeconds,
  String? trafficLevel,
}) {
  final parts = <String>[];
  if (durationSeconds > 0) {
    final totalMinutes = (durationSeconds / 60).round();
    if (totalMinutes > 0) {
      final hours = totalMinutes ~/ 60;
      final minutes = totalMinutes % 60;
      if (hours > 0 && minutes > 0) {
        parts.add('$hours h $minutes min');
      } else if (hours > 0) {
        parts.add('$hours h');
      } else {
        parts.add('$minutes min');
      }
    }
  }
  if (distanceMeters > 0) {
    final km = distanceMeters / 1000.0;
    if (km >= 10) {
      parts.add('${km.round()} km');
    } else if (km >= 0.1) {
      parts.add('${km.toStringAsFixed(1)} km');
    } else {
      parts.add('$distanceMeters m');
    }
  }
  if (trafficLevel != null && trafficLevel.isNotEmpty) {
    parts.add('$trafficLevel traffic');
  }
  if (parts.isEmpty) return null;
  return parts.join(' · ');
}

String? deriveTrafficLevel(int durationSeconds, int? staticDurationSeconds) {
  if (staticDurationSeconds == null ||
      staticDurationSeconds <= 0 ||
      durationSeconds <= 0) {
    return null;
  }
  final ratio = durationSeconds / staticDurationSeconds;
  if (ratio < 1.08) return 'Light';
  if (ratio < 1.25) return 'Moderate';
  return 'Heavy';
}

int parseGoogleDurationSeconds(String? duration) {
  if (duration == null) return 0;
  final match = RegExp(r'^(\d+(?:\.\d+)?)s$').firstMatch(duration.trim());
  if (match == null) return 0;
  final seconds = double.tryParse(match.group(1)!);
  if (seconds == null || seconds <= 0) return 0;
  return seconds.ceil();
}

/// Backend `GET /gps/maps/planned-route` data object.
TripDrivingRoute? parsePlannedRouteData(Map<String, dynamic> data) {
  final encoded = data['encodedPolyline'] as String?;
  final duration = (data['durationSeconds'] as num?)?.toInt() ?? 0;
  final staticDuration =
      (data['staticDurationSeconds'] as num?)?.toInt();
  return _routeFromEncoded(
    encoded,
    distanceMeters: (data['distanceMeters'] as num?)?.toInt() ?? 0,
    durationSeconds: duration,
    staticDurationSeconds: staticDuration,
    trafficLevel: data['trafficLevel'] as String? ??
        deriveTrafficLevel(duration, staticDuration),
    routingSource: data['routingSource'] as String?,
  );
}

/// Routes API `computeRoutes` JSON (client fallback).
TripDrivingRoute? parseRoutesComputePayload(Map<String, dynamic> payload) {
  final routes = payload['routes'];
  if (routes is! List || routes.isEmpty || routes.first is! Map) return null;
  final route = Map<String, dynamic>.from(routes.first as Map);
  final poly = route['polyline'];
  final encoded = poly is Map ? poly['encodedPolyline'] as String? : null;
  final duration = parseGoogleDurationSeconds(route['duration'] as String?);
  final staticDuration =
      parseGoogleDurationSeconds(route['staticDuration'] as String?);
  return _routeFromEncoded(
    encoded,
    distanceMeters: (route['distanceMeters'] as num?)?.toInt() ?? 0,
    durationSeconds: duration,
    staticDurationSeconds: staticDuration > 0 ? staticDuration : null,
    trafficLevel: deriveTrafficLevel(
      duration,
      staticDuration > 0 ? staticDuration : null,
    ),
    routingSource: 'GoogleRoutes',
  );
}

TripDrivingRoute? _routeFromEncoded(
  String? encoded, {
  required int distanceMeters,
  required int durationSeconds,
  int? staticDurationSeconds,
  String? trafficLevel,
  String? routingSource,
}) {
  if (encoded == null || encoded.isEmpty) return null;
  final points = decodeEncodedPolyline(encoded);
  if (points.length < 2) return null;
  return TripDrivingRoute(
    encodedPolyline: encoded,
    distanceMeters: distanceMeters,
    durationSeconds: durationSeconds,
    points: points,
    staticDurationSeconds: staticDurationSeconds,
    trafficLevel: trafficLevel,
    routingSource: routingSource,
  );
}
