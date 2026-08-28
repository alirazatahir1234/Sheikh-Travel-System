import 'fleet_models.dart';
import 'fleet_status.dart';

bool hasValidFleetCoords(double? lat, double? lng) {
  if (lat == null || lng == null) return false;
  if (!lat.isFinite || !lng.isFinite) return false;
  if (lat == 0 && lng == 0) return false;
  return lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
}

/// Short relative age for fleet list / overlays (e.g. `12s ago`, `3h 24m ago`).
String formatRelativeAge(DateTime? at, {DateTime? now}) {
  if (at == null) return 'No recent data';
  var sec = (now ?? DateTime.now()).difference(at).inSeconds;
  if (sec < 0) sec = 0;
  if (sec < 60) return '${sec}s ago';
  if (sec < 3600) return '${sec ~/ 60}m ago';
  if (sec < 86400) {
    final h = sec ~/ 3600;
    final m = (sec % 3600) ~/ 60;
    return m > 0 ? '${h}h ${m}m ago' : '${h}h ago';
  }
  return '${sec ~/ 86400}d ago';
}

/// Age-based GPS line for fleet map / list (matches ERP live-map freshness).
/// Live: update within 2 minutes. Available: valid coords but older. No data: no position.
String formatGpsFreshness({
  required double? latitude,
  required double? longitude,
  DateTime? lastUpdated,
  DateTime? now,
}) {
  if (!hasValidFleetCoords(latitude, longitude)) {
    return 'No GPS data';
  }

  if (lastUpdated == null) {
    return 'GPS position available';
  }

  final age = formatRelativeAge(lastUpdated, now: now);

  var sec = (now ?? DateTime.now()).difference(lastUpdated).inSeconds;
  if (sec < 0) sec = 0;

  if (sec <= 120) {
    return 'Live GPS · Last update: $age';
  }
  return 'GPS position available · Last update: $age';
}

/// Ignition OFF / Parked: hide GPS drift (under moving threshold) as movement.
double displaySpeedKmh({
  required double speed,
  bool? ignition,
  FleetTrackStatus? status,
}) {
  if (ignition == false || status == FleetTrackStatus.parked) {
    return speed >= movingThresholdKmh ? speed : 0;
  }
  return speed;
}

String formatDisplaySpeedLabel({
  required double speed,
  bool? ignition,
  FleetTrackStatus? status,
  double? latitude,
  double? longitude,
}) {
  // Without a valid position, never claim "Stationary" / 0 km/h.
  if (!hasValidFleetCoords(latitude, longitude) ||
      status == FleetTrackStatus.neverSeen) {
    return 'No recent data';
  }

  final display = displaySpeedKmh(
    speed: speed,
    ignition: ignition,
    status: status,
  );
  if (display > 0) return '${display.toStringAsFixed(0)} km/h';
  if (status == FleetTrackStatus.offline) {
    return 'Last known: stationary';
  }
  if (status == FleetTrackStatus.idle) return '0 km/h · idle';
  if (status == FleetTrackStatus.parked || ignition == false) {
    return '0 km/h · stationary';
  }
  return 'Stationary';
}
