import 'dart:math' as math;
import '../../domain/fleet_models.dart';

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
