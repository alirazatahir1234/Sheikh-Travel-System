import 'fleet_models.dart';
import 'fleet_status.dart';

bool hasValidFleetCoords(double? lat, double? lng) {
  if (lat == null || lng == null) return false;
  if (!lat.isFinite || !lng.isFinite) return false;
  if (lat == 0 && lng == 0) return false;
  return lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
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

  var sec = (now ?? DateTime.now()).difference(lastUpdated).inSeconds;
  if (sec < 0) sec = 0;

  final age = sec < 60
      ? '${sec}s ago'
      : sec < 3600
          ? '${sec ~/ 60}m ago'
          : '${sec ~/ 3600}h ago';

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
}) {
  final display = displaySpeedKmh(
    speed: speed,
    ignition: ignition,
    status: status,
  );
  if (display > 0) return '${display.toStringAsFixed(0)} km/h';
  if (status == FleetTrackStatus.idle) return '0 km/h · idle';
  if (status == FleetTrackStatus.parked || ignition == false) {
    return '0 km/h · stationary';
  }
  return 'Stationary';
}
