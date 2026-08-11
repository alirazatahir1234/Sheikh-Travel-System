import '../domain/fleet_models.dart';

/// Matches backend IsOnline window (LastSeenAt > now - 30min).
const offlineStaleMs = 30 * 60 * 1000;

/// Matches TraccarOptions.MovingSpeedKmh / ERP MOVING_THRESHOLD_KMH.
const movingThresholdKmh = 10.0;

const defaultSosAlarmValues = ['sos', 'panic'];

bool isSosAlarm(String? alarmType, [List<String> sosValues = defaultSosAlarmValues]) {
  if (alarmType == null || alarmType.isEmpty) return false;
  final lower = alarmType.toLowerCase();
  return sosValues.any((v) => v.toLowerCase() == lower);
}

/// Single source of truth for deriving a live-map vehicle status from telemetry.
/// Priority when online: SOS → Parked (ignition off) → Moving (>=10) → Idle.
FleetTrackStatus resolveFleetStatus({
  double? speed,
  bool? ignition,
  DateTime? lastUpdated,
  bool hasGps = true,
  String? alarmType,
  DateTime? now,
}) {
  if (isSosAlarm(alarmType)) return FleetTrackStatus.sos;

  if (!hasGps || lastUpdated == null) return FleetTrackStatus.neverSeen;

  final ageMs = (now ?? DateTime.now()).difference(lastUpdated).inMilliseconds;
  if (ageMs.isNaN || ageMs > offlineStaleMs) return FleetTrackStatus.offline;

  final spd = speed ?? 0;

  // Explicit ACC OFF: always Parked — GPS drift at 1–9 km/h must not become Moving.
  if (ignition == false) return FleetTrackStatus.parked;

  if (spd >= movingThresholdKmh) return FleetTrackStatus.moving;

  return FleetTrackStatus.idle;
}

List<FleetVehicleLocation> mergeVehiclesWithLive({
  required List<VehicleListItem> vehicles,
  required List<GpsPosition> live,
}) {
  final liveByVehicle = {for (final p in live) p.vehicleId: p};

  return vehicles.where((v) => !v.isRetired).map((v) {
    final livePos = liveByVehicle[v.id];
    if (livePos != null && _validCoords(livePos.latitude, livePos.longitude)) {
      final status = resolveFleetStatus(
        speed: livePos.speed,
        ignition: livePos.ignition,
        lastUpdated: livePos.timestamp,
        hasGps: true,
        alarmType: livePos.alarmType,
      );
      return FleetVehicleLocation(
        vehicleId: v.id,
        vehicleName: v.name.isEmpty ? 'Vehicle #${v.id}' : v.name,
        registrationNumber: v.registrationNumber,
        status: status,
        latitude: livePos.latitude,
        longitude: livePos.longitude,
        lastUpdated: livePos.timestamp,
        speed: livePos.speed,
        driverName: v.driverName,
        driverPhone: v.driverPhone,
        hasGps: true,
        ignition: livePos.ignition,
        heading: livePos.heading,
        batteryLevel: livePos.batteryLevel,
        gsmSignal: livePos.gsmSignal,
        address: livePos.address,
        alarmType: livePos.alarmType,
        vehicleType: v.vehicleType,
        serviceAlert: v.serviceAlert,
      );
    }

    final lat = v.locationLatitude;
    final lng = v.locationLongitude;
    if (_validCoords(lat, lng)) {
      final lastUpdated = v.locationLastUpdate;
      final status = lastUpdated != null
          ? resolveFleetStatus(
              speed: 0,
              ignition: v.engineIgnition,
              lastUpdated: lastUpdated,
              hasGps: true,
            )
          : FleetTrackStatus.offline;
      return FleetVehicleLocation(
        vehicleId: v.id,
        vehicleName: v.name.isEmpty ? 'Vehicle #${v.id}' : v.name,
        registrationNumber: v.registrationNumber,
        status: status,
        latitude: lat,
        longitude: lng,
        lastUpdated: lastUpdated,
        speed: 0,
        driverName: v.driverName,
        driverPhone: v.driverPhone,
        hasGps: true,
        ignition: v.engineIgnition,
        vehicleType: v.vehicleType,
        serviceAlert: v.serviceAlert,
      );
    }

    return FleetVehicleLocation(
      vehicleId: v.id,
      vehicleName: v.name.isEmpty ? 'Vehicle #${v.id}' : v.name,
      registrationNumber: v.registrationNumber,
      status: v.hasGpsDevice
          ? FleetTrackStatus.neverSeen
          : FleetTrackStatus.offline,
      driverName: v.driverName,
      driverPhone: v.driverPhone,
      hasGps: v.hasGpsDevice,
      vehicleType: v.vehicleType,
      serviceAlert: v.serviceAlert,
    );
  }).toList();
}

/// Apply a SignalR position update onto an existing list (fresher timestamp wins).
List<FleetVehicleLocation> applyLiveUpdate(
  List<FleetVehicleLocation> current,
  GpsPosition update,
) {
  final idx = current.indexWhere((v) => v.vehicleId == update.vehicleId);
  if (idx < 0) return current;
  final existing = current[idx];
  if (existing.lastUpdated != null &&
      !update.timestamp.isAfter(existing.lastUpdated!)) {
    return current;
  }
  if (!_validCoords(update.latitude, update.longitude)) return current;

  final status = resolveFleetStatus(
    speed: update.speed,
    ignition: update.ignition,
    lastUpdated: update.timestamp,
    hasGps: true,
    alarmType: update.alarmType,
  );
  final next = List<FleetVehicleLocation>.from(current);
  next[idx] = existing.copyWith(
    latitude: update.latitude,
    longitude: update.longitude,
    lastUpdated: update.timestamp,
    speed: update.speed,
    status: status,
    ignition: update.ignition,
    heading: update.heading,
    batteryLevel: update.batteryLevel,
    gsmSignal: update.gsmSignal,
    address: _preferAddress(existing.address, update.address),
    alarmType: update.alarmType,
  );
  return next;
}

/// Keep a good resolved address when a live poll/SignalR payload omits or clears it.
String? _preferAddress(String? previous, String? incoming) {
  final next = incoming?.trim();
  if (next != null && next.isNotEmpty) return next;
  final prev = previous?.trim();
  if (prev != null && prev.isNotEmpty) return prev;
  return null;
}

bool _validCoords(double? lat, double? lng) =>
    lat != null && lng != null && lat != 0 && lng != 0;
