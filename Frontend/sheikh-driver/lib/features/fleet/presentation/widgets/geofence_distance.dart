import 'dart:convert';
import 'dart:math' as math;
import '../../domain/fleet_models.dart';

/// Outer ring of a GeoJSON Polygon / MultiPolygon / Feature / FeatureCollection
/// as (lat, lng) pairs, without the closing duplicate. Empty when unparseable.
List<({double lat, double lng})> parseGeofenceRing(String? geoJson) {
  final raw = geoJson?.trim();
  if (raw == null || raw.isEmpty) return const [];
  try {
    dynamic node = jsonDecode(raw);
    if (node is Map && node['type'] == 'FeatureCollection') {
      final features = node['features'];
      node = features is List && features.isNotEmpty ? features.first : null;
    }
    if (node is Map && node['type'] == 'Feature') node = node['geometry'];
    if (node is! Map) return const [];
    var coords = node['coordinates'];
    if (node['type'] == 'MultiPolygon' && coords is List && coords.isNotEmpty) {
      coords = coords.first;
    }
    if (coords is! List || coords.isEmpty || coords.first is! List) {
      return const [];
    }
    final out = <({double lat, double lng})>[];
    for (final pt in coords.first as List) {
      if (pt is List && pt.length >= 2 && pt[0] is num && pt[1] is num) {
        out.add((lat: (pt[1] as num).toDouble(), lng: (pt[0] as num).toDouble()));
      }
    }
    if (out.length > 1 &&
        out.first.lat == out.last.lat &&
        out.first.lng == out.last.lng) {
      out.removeLast();
    }
    return out;
  } catch (_) {
    return const [];
  }
}

/// GeoJSON Polygon string (lng, lat order, closed ring) for create/update.
String geofenceRingToGeoJson(List<({double lat, double lng})> ring) {
  final coords = [
    for (final p in ring) [p.lng, p.lat],
    if (ring.isNotEmpty) [ring.first.lng, ring.first.lat],
  ];
  return jsonEncode({
    'type': 'Polygon',
    'coordinates': [coords],
  });
}

bool _insideRing(double lat, double lng, List<({double lat, double lng})> ring) {
  var inside = false;
  for (var i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    final yi = ring[i].lat, xi = ring[i].lng;
    final yj = ring[j].lat, xj = ring[j].lng;
    if ((yi > lat) != (yj > lat) &&
        lng < (xj - xi) * (lat - yi) / ((yj - yi) + 1e-300) + xi) {
      inside = !inside;
    }
  }
  return inside;
}

/// Client-side inside check for every zone shape. Null when the zone's
/// geometry is unusable (circle without radius, polygon < 3 points).
bool? isInsideGeofence(double lat, double lng, GpsGeofenceItem g) {
  if (g.isCircle) {
    if (g.radiusMeters <= 0) return null;
    return _haversineMeters(lat, lng, g.centerLat, g.centerLng) <=
        g.radiusMeters;
  }
  final ring = parseGeofenceRing(g.geoJson);
  if (ring.length < 3) return null;
  return _insideRing(lat, lng, ring);
}

double _haversineMeters(double lat1, double lon1, double lat2, double lon2) {
  const r = 6371000;
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

class NearestGeofenceInfo {
  const NearestGeofenceInfo({
    required this.name,
    required this.distanceMeters,
    required this.inside,
  });

  final String name;
  final double distanceMeters;
  final bool inside;
}

NearestGeofenceInfo? nearestCircleGeofence({
  required double lat,
  required double lng,
  required List<GpsGeofenceItem> fences,
}) {
  NearestGeofenceInfo? best;
  for (final g in fences) {
    if (!g.isCircle || g.radiusMeters <= 0) continue;
    final d = _haversineMeters(lat, lng, g.centerLat, g.centerLng);
    final inside = d <= g.radiusMeters;
    if (best == null || d < best.distanceMeters) {
      best = NearestGeofenceInfo(
        name: g.name,
        distanceMeters: d,
        inside: inside,
      );
    }
  }
  return best;
}
