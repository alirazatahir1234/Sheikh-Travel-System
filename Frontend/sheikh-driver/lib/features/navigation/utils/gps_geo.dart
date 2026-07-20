import 'dart:math' as math;

/// Port of ERP `gps-status.util.ts` + live-map trail colors.
enum FleetTrackStatus { moving, idle, parked, offline, sos }

class GpsStatusColors {
  /// Matches ERP `TRAIL_COLORS` in live-map.component.ts
  static const moving = 0xFF10B981;
  static const idle = 0xFFF59E0B;
  static const parked = 0xFF3B82F6;
  static const offline = 0xFF64748B;
  static const sos = 0xFFDC2626;
  static const route = 0xFF0D9488;
  static const pickup = 0xFF10B981;
  static const dropoff = 0xFFEF4444;
}

/// Matches backend `GpsPositionIngestionHelper.MovingSpeedKmh` / ERP util.
const movingThresholdKmh = 5.0;

/// Default map center used by ERP GPS maps (Pakistan).
const defaultMapLat = 30.3753;
const defaultMapLng = 69.3451;

FleetTrackStatus resolveFleetStatus({
  required double speedKmh,
  bool? ignition,
  bool sos = false,
  DateTime? lastUpdated,
}) {
  if (sos) return FleetTrackStatus.sos;
  if (lastUpdated != null &&
      DateTime.now().difference(lastUpdated) > const Duration(minutes: 30)) {
    return FleetTrackStatus.offline;
  }
  if (speedKmh > movingThresholdKmh) return FleetTrackStatus.moving;
  if (ignition == false) return FleetTrackStatus.parked;
  return FleetTrackStatus.idle;
}

int colorForStatus(FleetTrackStatus status) => switch (status) {
      FleetTrackStatus.moving => GpsStatusColors.moving,
      FleetTrackStatus.idle => GpsStatusColors.idle,
      FleetTrackStatus.parked => GpsStatusColors.parked,
      FleetTrackStatus.offline => GpsStatusColors.offline,
      FleetTrackStatus.sos => GpsStatusColors.sos,
    };

/// Haversine distance in km — same formula as ERP trip route / GPS ETA.
double haversineKm(double lat1, double lon1, double lat2, double lon2) {
  const r = 6371.0;
  final dLat = _toRad(lat2 - lat1);
  final dLon = _toRad(lon2 - lon1);
  final a = math.sin(dLat / 2) * math.sin(dLat / 2) +
      math.cos(_toRad(lat1)) *
          math.cos(_toRad(lat2)) *
          math.sin(dLon / 2) *
          math.sin(dLon / 2);
  final c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a));
  return (r * c * 100).roundToDouble() / 100;
}

/// ETA minutes at 40 km/h — matches `GetGpsEtaQueryHandler`.
int etaMinutesFromKm(double distanceKm) =>
    distanceKm > 0 ? (distanceKm / 40.0 * 60).ceil() : 0;

double _toRad(double deg) => deg * math.pi / 180;
