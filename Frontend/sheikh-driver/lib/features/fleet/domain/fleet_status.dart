import '../domain/fleet_models.dart';

/// Matches backend GpsSettings.OfflineStaleMinutes (default 10).
const offlineStaleMs = 10 * 60 * 1000;

/// Matches TraccarOptions.MovingSpeedKmh / ERP MOVING_THRESHOLD_KMH.
const movingThresholdKmh = 10.0;

const defaultSosAlarmValues = ['sos', 'panic'];

bool isSosAlarm(String? alarmType,
    [List<String> sosValues = defaultSosAlarmValues]) {
  if (alarmType == null || alarmType.isEmpty) return false;
  final lower = alarmType.toLowerCase();
  return sosValues.any((v) => v.toLowerCase() == lower);
}

/// Single source of truth for deriving a live-map vehicle status from telemetry.
/// Priority when online: SOS → invalid GPS → Parked (explicit ACC off) → Moving → Idle → Unknown (ACC missing).
FleetTrackStatus resolveFleetStatus({
  double? speed,
  bool? ignition,
  DateTime? lastUpdated,
  bool hasGps = true,
  String? alarmType,
  DateTime? now,
  bool? isGpsValid,
}) {
  // No tracker assigned — never Online (orphan VCL coords must not inflate KPIs).
  if (!hasGps) return FleetTrackStatus.offline;

  if (isSosAlarm(alarmType)) return FleetTrackStatus.sos;

  if (lastUpdated == null) return FleetTrackStatus.neverSeen;

  final ageMs = (now ?? DateTime.now()).difference(lastUpdated).inMilliseconds;
  if (ageMs.isNaN || ageMs > offlineStaleMs) return FleetTrackStatus.offline;

  // Unreliable fix — never claim Parked / Ignition off.
  if (isGpsValid == false) return FleetTrackStatus.unknown;

  final spd = speed ?? 0;

  // Explicit ACC OFF + low speed → Parked. High speed with ACC off → Unknown.
  if (ignition == false) {
    return spd >= movingThresholdKmh
        ? FleetTrackStatus.unknown
        : FleetTrackStatus.parked;
  }

  if (spd >= movingThresholdKmh) return FleetTrackStatus.moving;

  // Engine ON at rest → Idle. Null / unwired ACC at rest → Unknown (not Parked/OFF).
  if (ignition == true) return FleetTrackStatus.idle;
  return FleetTrackStatus.unknown;
}

/// Maps a `GET /gps/live/fleet` row (ERP GpsLiveFleetVehicleDto).
FleetVehicleLocation mapLiveFleetRow(Map<String, dynamic> json) {
  final lat =
      (json['latitude'] as num? ?? json['Latitude'] as num?)?.toDouble();
  final lng =
      (json['longitude'] as num? ?? json['Longitude'] as num?)?.toDouble();
  final lastUpdated =
      _parseOptionalDate(json['lastUpdated'] ?? json['LastUpdated']);
  final speed =
      (json['speed'] as num? ?? json['Speed'] as num? ?? 0).toDouble();
  final hasGpsDevice =
      json['hasGpsDevice'] as bool? ?? json['HasGpsDevice'] as bool? ?? false;
  final hasCoords = _validCoords(lat, lng);
  final alarmType =
      json['alarmType'] as String? ?? json['AlarmType'] as String?;
  final ignition = json['ignition'] as bool? ?? json['Ignition'] as bool?;
  final isGpsValid =
      json['isGpsValid'] as bool? ?? json['IsGpsValid'] as bool? ?? true;
  final gpsQualityCode =
      json['gpsQualityCode'] as String? ?? json['GpsQualityCode'] as String?;
  final vehicleId = json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0;
  final rawName =
      (json['vehicleName'] as String? ?? json['VehicleName'] as String? ?? '')
          .trim();

  // Online requires a live tracker — leftover coords without a device stay Offline.
  final FleetTrackStatus status;
  if (!hasGpsDevice) {
    status = FleetTrackStatus.offline;
  } else if (hasCoords) {
    status = resolveFleetStatus(
      speed: speed,
      ignition: ignition,
      lastUpdated: lastUpdated,
      hasGps: true,
      alarmType: alarmType,
      isGpsValid: isGpsValid,
    );
  } else {
    status = FleetTrackStatus.neverSeen;
  }

  return FleetVehicleLocation(
    vehicleId: vehicleId,
    vehicleName: rawName.isEmpty ? 'Vehicle #$vehicleId' : rawName,
    registrationNumber: json['registrationNumber'] as String? ??
        json['RegistrationNumber'] as String? ??
        '',
    status: status,
    latitude: hasCoords ? lat : null,
    longitude: hasCoords ? lng : null,
    lastUpdated: lastUpdated,
    speed: speed,
    driverName: json['driverName'] as String? ?? json['DriverName'] as String?,
    driverPhone:
        json['driverPhone'] as String? ?? json['DriverPhone'] as String?,
    hasGps: hasGpsDevice && hasCoords,
    ignition: ignition,
    heading: (json['heading'] as num? ?? json['Heading'] as num?)?.toDouble(),
    batteryLevel: (json['batteryLevel'] as num? ?? json['BatteryLevel'] as num?)
        ?.toDouble(),
    gsmSignal: json['gsmSignal'] as int? ?? json['GsmSignal'] as int?,
    address: json['address'] as String? ?? json['Address'] as String?,
    alarmType: alarmType,
    vehicleType:
        json['vehicleType'] as String? ?? json['VehicleType'] as String?,
    isGpsValid: isGpsValid,
    gpsQualityCode: gpsQualityCode,
  );
}

DateTime? _parseOptionalDate(Object? raw) {
  if (raw == null) return null;
  if (raw is DateTime) return raw;
  return DateTime.tryParse(raw.toString());
}

List<FleetVehicleLocation> mergeVehiclesWithLive({
  required List<VehicleListItem> vehicles,
  required List<GpsPosition> live,
}) {
  final liveByVehicle = {for (final p in live) p.vehicleId: p};

  return vehicles.where((v) => !v.isRetired).map((v) {
    final livePos = liveByVehicle[v.id];
    if (v.hasGpsDevice &&
        livePos != null &&
        _validCoords(livePos.latitude, livePos.longitude)) {
      final status = resolveFleetStatus(
        speed: livePos.speed,
        ignition: livePos.ignition,
        lastUpdated: livePos.timestamp,
        hasGps: true,
        alarmType: livePos.alarmType,
        isGpsValid: livePos.isGpsValid,
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
        driverPhone: () {
          final livePhone = livePos.driverPhone?.trim();
          if (livePhone != null && livePhone.isNotEmpty) return livePhone;
          return v.driverPhone;
        }(),
        hasGps: true,
        ignition: livePos.ignition,
        heading: livePos.heading,
        batteryLevel: livePos.batteryLevel,
        gsmSignal: livePos.gsmSignal,
        address: livePos.address,
        alarmType: livePos.alarmType,
        vehicleType: v.vehicleType,
        serviceAlert: v.serviceAlert,
        isGpsValid: livePos.isGpsValid,
        gpsQualityCode: livePos.gpsQualityCode,
      );
    }

    final lat = v.locationLatitude;
    final lng = v.locationLongitude;
    if (v.hasGpsDevice && _validCoords(lat, lng)) {
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

  // Do not promote tracker-less vehicles to Online via SignalR.
  final hasTracker =
      existing.hasGps || existing.status == FleetTrackStatus.neverSeen;
  final status = resolveFleetStatus(
    speed: update.speed,
    ignition: update.ignition,
    lastUpdated: update.timestamp,
    hasGps: hasTracker,
    alarmType: update.alarmType,
    isGpsValid: update.isGpsValid,
  );
  final next = List<FleetVehicleLocation>.from(current);
  final livePhone = update.driverPhone?.trim();
  next[idx] = existing.copyWith(
    latitude: update.latitude,
    longitude: update.longitude,
    lastUpdated: update.timestamp,
    speed: update.speed,
    status: status,
    hasGps: hasTracker,
    ignition: update.ignition,
    clearIgnition: true,
    heading: update.heading,
    batteryLevel: update.batteryLevel,
    gsmSignal: update.gsmSignal,
    address: _preferAddress(existing.address, update.address),
    alarmType: update.alarmType,
    driverPhone: (livePhone != null && livePhone.isNotEmpty)
        ? livePhone
        : existing.driverPhone,
    isGpsValid: update.isGpsValid,
    gpsQualityCode: update.gpsQualityCode,
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

/// Build KPI strip counts from the same vehicle rows the list shows — always
/// reconciles: Total = Online + Offline + NeverSeen.
GpsFleetStatusKpis deriveFleetKpisFromLocations(
  List<FleetVehicleLocation> locations, {
  int alertsToday = 0,
}) {
  var moving = 0;
  var idle = 0;
  var parked = 0;
  var offline = 0;
  var neverSeen = 0;
  var sos = 0;
  var unknown = 0;

  for (final v in locations) {
    switch (v.status) {
      case FleetTrackStatus.moving:
        moving++;
      case FleetTrackStatus.idle:
        idle++;
      case FleetTrackStatus.parked:
        parked++;
      case FleetTrackStatus.offline:
        offline++;
      case FleetTrackStatus.neverSeen:
        neverSeen++;
      case FleetTrackStatus.sos:
        sos++;
      case FleetTrackStatus.unknown:
        unknown++;
    }
  }

  final online = moving + idle + parked + sos + unknown;
  return GpsFleetStatusKpis(
    totalVehicles: locations.length,
    online: online,
    offline: offline,
    moving: moving,
    idle: idle,
    parked: parked,
    neverSeen: neverSeen,
    sos: sos,
    alertsToday: alertsToday,
    unknown: unknown,
  );
}
