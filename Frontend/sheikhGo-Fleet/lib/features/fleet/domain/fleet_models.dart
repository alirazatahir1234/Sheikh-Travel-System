/// Fleet track status — mirrors ERP `resolveFleetStatus` / backend calculator.
enum FleetTrackStatus {
  moving,
  idle,
  parked,
  offline,
  neverSeen,
  sos,
}

extension FleetTrackStatusX on FleetTrackStatus {
  String get label => switch (this) {
        FleetTrackStatus.moving => 'Moving',
        FleetTrackStatus.idle => 'Idle',
        FleetTrackStatus.parked => 'Parked',
        FleetTrackStatus.offline => 'Offline',
        FleetTrackStatus.neverSeen => 'Never seen',
        FleetTrackStatus.sos => 'SOS',
      };

  String get filterKey => switch (this) {
        FleetTrackStatus.moving => 'moving',
        FleetTrackStatus.idle => 'idle',
        FleetTrackStatus.parked => 'parked',
        FleetTrackStatus.offline => 'offline',
        FleetTrackStatus.neverSeen => 'never_seen',
        FleetTrackStatus.sos => 'sos',
      };
}

class GpsFleetStatusKpis {
  const GpsFleetStatusKpis({
    required this.totalVehicles,
    required this.online,
    required this.offline,
    required this.moving,
    required this.idle,
    required this.parked,
    required this.neverSeen,
    required this.sos,
    required this.alertsToday,
  });

  final int totalVehicles;
  final int online;
  final int offline;
  final int moving;
  final int idle;
  final int parked;
  final int neverSeen;
  final int sos;
  final int alertsToday;

  factory GpsFleetStatusKpis.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) =>
        json[a] as int? ?? (b != null ? json[b] as int? : null) ?? 0;
    return GpsFleetStatusKpis(
      totalVehicles: n('totalVehicles', 'TotalVehicles'),
      online: n('online', 'Online'),
      offline: n('offline', 'Offline'),
      moving: n('moving', 'Moving'),
      idle: n('idle', 'Idle'),
      parked: n('parked', 'Parked'),
      neverSeen: n('neverSeen', 'NeverSeen'),
      sos: n('sos', 'Sos'),
      alertsToday: n('alertsToday', 'AlertsToday'),
    );
  }

  static const empty = GpsFleetStatusKpis(
    totalVehicles: 0,
    online: 0,
    offline: 0,
    moving: 0,
    idle: 0,
    parked: 0,
    neverSeen: 0,
    sos: 0,
    alertsToday: 0,
  );
}

class FleetOpsDashboard {
  const FleetOpsDashboard({
    required this.totalVehicles,
    required this.activeVehicles,
    required this.driversOnDuty,
    required this.maintenanceDue,
    required this.monthlyFuelCost,
    required this.complianceAlerts,
  });

  final int totalVehicles;
  final int activeVehicles;
  final int driversOnDuty;
  final int maintenanceDue;
  final double monthlyFuelCost;
  final int complianceAlerts;

  factory FleetOpsDashboard.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) =>
        json[a] as int? ?? (b != null ? json[b] as int? : null) ?? 0;
    num cost = json['monthlyFuelCost'] as num? ??
        json['MonthlyFuelCost'] as num? ??
        0;
    return FleetOpsDashboard(
      totalVehicles: n('totalVehicles', 'TotalVehicles'),
      activeVehicles: n('activeVehicles', 'ActiveVehicles'),
      driversOnDuty: n('driversOnDuty', 'DriversOnDuty'),
      maintenanceDue: n('maintenanceDue', 'MaintenanceDue'),
      monthlyFuelCost: cost.toDouble(),
      complianceAlerts: n('complianceAlerts', 'ComplianceAlerts'),
    );
  }

  static const empty = FleetOpsDashboard(
    totalVehicles: 0,
    activeVehicles: 0,
    driversOnDuty: 0,
    maintenanceDue: 0,
    monthlyFuelCost: 0,
    complianceAlerts: 0,
  );
}

class GpsPosition {
  const GpsPosition({
    required this.vehicleId,
    required this.latitude,
    required this.longitude,
    required this.speed,
    required this.timestamp,
    this.driverId,
    this.bookingId,
    this.heading,
    this.ignition,
    this.fuelLevel,
    this.batteryLevel,
    this.gsmSignal,
    this.totalDistanceKm,
    this.address,
    this.alarmType,
    this.driverPhone,
  });

  final int vehicleId;
  final double latitude;
  final double longitude;
  final double speed;
  final DateTime timestamp;
  final int? driverId;
  final int? bookingId;
  final double? heading;
  final bool? ignition;
  final double? fuelLevel;
  final double? batteryLevel;
  final int? gsmSignal;
  final double? totalDistanceKm;
  final String? address;
  final String? alarmType;
  final String? driverPhone;

  factory GpsPosition.fromJson(Map<String, dynamic> json) {
    return GpsPosition(
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      latitude: (json['latitude'] as num? ?? json['Latitude'] as num? ?? 0)
          .toDouble(),
      longitude: (json['longitude'] as num? ?? json['Longitude'] as num? ?? 0)
          .toDouble(),
      speed: (json['speed'] as num? ?? json['Speed'] as num? ?? 0).toDouble(),
      timestamp: _parseDate(json['timestamp'] ?? json['Timestamp']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      driverId: json['driverId'] as int? ?? json['DriverId'] as int?,
      bookingId: json['bookingId'] as int? ?? json['BookingId'] as int?,
      heading: (json['heading'] as num? ?? json['Heading'] as num?)?.toDouble(),
      ignition: json['ignition'] as bool? ?? json['Ignition'] as bool?,
      fuelLevel:
          (json['fuelLevel'] as num? ?? json['FuelLevel'] as num?)?.toDouble(),
      batteryLevel: (json['batteryLevel'] as num? ?? json['BatteryLevel'] as num?)
          ?.toDouble(),
      gsmSignal: json['gsmSignal'] as int? ?? json['GsmSignal'] as int?,
      totalDistanceKm:
          (json['totalDistanceKm'] as num? ?? json['TotalDistanceKm'] as num?)
              ?.toDouble(),
      address: json['address'] as String? ?? json['Address'] as String?,
      alarmType: json['alarmType'] as String? ?? json['AlarmType'] as String?,
      driverPhone:
          json['driverPhone'] as String? ?? json['DriverPhone'] as String?,
    );
  }
}

class VehicleListItem {
  const VehicleListItem({
    required this.id,
    required this.name,
    required this.registrationNumber,
    required this.status,
    this.make,
    this.model,
    this.vehicleType,
    this.driverName,
    this.driverId,
    this.engineIgnition,
    this.gpsLastSeenAt,
    this.gpsOnline = false,
    this.hasGpsDevice = false,
    this.locationLatitude,
    this.locationLongitude,
    this.locationLastUpdate,
    this.serviceAlert,
    this.imageUrl,
  });

  final int id;
  final String name;
  final String registrationNumber;
  final String status;
  final String? make;
  final String? model;
  final String? vehicleType;
  final String? driverName;
  final int? driverId;
  final bool? engineIgnition;
  final DateTime? gpsLastSeenAt;
  final bool gpsOnline;
  final bool hasGpsDevice;
  final double? locationLatitude;
  final double? locationLongitude;
  final DateTime? locationLastUpdate;
  final String? serviceAlert;
  final String? imageUrl;

  bool get isRetired {
    final s = status.toLowerCase();
    return s == 'retired' || s == '4';
  }

  factory VehicleListItem.fromJson(Map<String, dynamic> json) {
    final statusRaw = json['status'] ?? json['Status'];
    return VehicleListItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      name: json['name'] as String? ?? json['Name'] as String? ?? '',
      registrationNumber: json['registrationNumber'] as String? ??
          json['RegistrationNumber'] as String? ??
          '',
      status: statusRaw?.toString() ?? '',
      make: json['make'] as String? ?? json['Make'] as String?,
      model: json['model'] as String? ?? json['Model'] as String?,
      vehicleType:
          json['vehicleType'] as String? ?? json['VehicleType'] as String?,
      driverName:
          json['driverName'] as String? ?? json['DriverName'] as String?,
      driverId: json['driverId'] as int? ?? json['DriverId'] as int?,
      engineIgnition:
          json['engineIgnition'] as bool? ?? json['EngineIgnition'] as bool?,
      gpsLastSeenAt:
          _parseDate(json['gpsLastSeenAt'] ?? json['GpsLastSeenAt']),
      gpsOnline: json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
      hasGpsDevice:
          json['hasGpsDevice'] as bool? ?? json['HasGpsDevice'] as bool? ?? false,
      locationLatitude: (json['locationLatitude'] as num? ??
              json['LocationLatitude'] as num?)
          ?.toDouble(),
      locationLongitude: (json['locationLongitude'] as num? ??
              json['LocationLongitude'] as num?)
          ?.toDouble(),
      locationLastUpdate:
          _parseDate(json['locationLastUpdate'] ?? json['LocationLastUpdate']),
      serviceAlert:
          json['serviceAlert'] as String? ?? json['ServiceAlert'] as String?,
      imageUrl: json['imageUrl'] as String? ?? json['ImageUrl'] as String?,
    );
  }
}

class VehicleDetail {
  const VehicleDetail({
    required this.id,
    required this.name,
    required this.registrationNumber,
    required this.status,
    this.make,
    this.model,
    this.year,
    this.color,
    this.vehicleType,
    this.seatingCapacity,
    this.currentMileage,
    this.driverName,
    this.driverId,
    this.driverPhone,
    this.gpsImei,
    this.gpsOnline = false,
    this.hasGpsDevice = false,
    this.engineIgnition,
    this.locationLatitude,
    this.locationLongitude,
    this.locationLastUpdate,
    this.locationSpeed,
    this.batteryLevel,
    this.gsmSignal,
    this.address,
    this.serviceAlert,
    this.nextServiceDue,
    this.imageUrl,
  });

  final int id;
  final String name;
  final String registrationNumber;
  final String status;
  final String? make;
  final String? model;
  final int? year;
  final String? color;
  final String? vehicleType;
  final int? seatingCapacity;
  final double? currentMileage;
  final String? driverName;
  final int? driverId;
  final String? driverPhone;
  final String? gpsImei;
  final bool gpsOnline;
  final bool hasGpsDevice;
  final bool? engineIgnition;
  final double? locationLatitude;
  final double? locationLongitude;
  final DateTime? locationLastUpdate;
  final double? locationSpeed;
  final double? batteryLevel;
  final int? gsmSignal;
  final String? address;
  final String? serviceAlert;
  final DateTime? nextServiceDue;
  final String? imageUrl;

  factory VehicleDetail.fromJson(Map<String, dynamic> json) {
    final statusRaw = json['status'] ?? json['Status'];
    return VehicleDetail(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      name: json['name'] as String? ?? json['Name'] as String? ?? '',
      registrationNumber: json['registrationNumber'] as String? ??
          json['RegistrationNumber'] as String? ??
          '',
      status: statusRaw?.toString() ?? '',
      make: json['make'] as String? ?? json['Make'] as String?,
      model: json['model'] as String? ?? json['Model'] as String?,
      year: json['year'] as int? ?? json['Year'] as int?,
      color: json['color'] as String? ?? json['Color'] as String?,
      vehicleType:
          json['vehicleType'] as String? ?? json['VehicleType'] as String?,
      seatingCapacity:
          json['seatingCapacity'] as int? ?? json['SeatingCapacity'] as int?,
      currentMileage: (json['currentMileage'] as num? ??
              json['CurrentMileage'] as num?)
          ?.toDouble(),
      driverName:
          json['driverName'] as String? ?? json['DriverName'] as String?,
      driverId: json['driverId'] as int? ?? json['DriverId'] as int?,
      driverPhone:
          json['driverPhone'] as String? ?? json['DriverPhone'] as String?,
      gpsImei: json['gpsImei'] as String? ?? json['GpsImei'] as String?,
      gpsOnline: json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
      hasGpsDevice:
          json['hasGpsDevice'] as bool? ?? json['HasGpsDevice'] as bool? ?? false,
      engineIgnition:
          json['engineIgnition'] as bool? ?? json['EngineIgnition'] as bool?,
      locationLatitude: (json['locationLatitude'] as num? ??
              json['LocationLatitude'] as num?)
          ?.toDouble(),
      locationLongitude: (json['locationLongitude'] as num? ??
              json['LocationLongitude'] as num?)
          ?.toDouble(),
      locationLastUpdate:
          _parseDate(json['locationLastUpdate'] ?? json['LocationLastUpdate']),
      locationSpeed: (json['locationSpeed'] as num? ??
              json['LocationSpeed'] as num?)
          ?.toDouble(),
      batteryLevel: (json['batteryLevel'] as num? ??
              json['BatteryLevel'] as num?)
          ?.toDouble(),
      gsmSignal: json['gsmSignal'] as int? ?? json['GsmSignal'] as int?,
      address: json['address'] as String? ?? json['Address'] as String?,
      serviceAlert:
          json['serviceAlert'] as String? ?? json['ServiceAlert'] as String?,
      nextServiceDue:
          _parseDate(json['nextServiceDue'] ?? json['NextServiceDue']),
      imageUrl: json['imageUrl'] as String? ?? json['ImageUrl'] as String?,
    );
  }
}

/// Merged vehicle + live GPS row for list/map.
class FleetVehicleLocation {
  const FleetVehicleLocation({
    required this.vehicleId,
    required this.vehicleName,
    required this.registrationNumber,
    required this.status,
    this.latitude,
    this.longitude,
    this.lastUpdated,
    this.speed = 0,
    this.driverName,
    this.hasGps = false,
    this.ignition,
    this.heading,
    this.batteryLevel,
    this.gsmSignal,
    this.address,
    this.alarmType,
    this.vehicleType,
    this.serviceAlert,
  });

  final int vehicleId;
  final String vehicleName;
  final String registrationNumber;
  final FleetTrackStatus status;
  final double? latitude;
  final double? longitude;
  final DateTime? lastUpdated;
  final double speed;
  final String? driverName;
  final bool hasGps;
  final bool? ignition;
  final double? heading;
  final double? batteryLevel;
  final int? gsmSignal;
  final String? address;
  final String? alarmType;
  final String? vehicleType;
  final String? serviceAlert;

  bool get hasMapCoords =>
      latitude != null &&
      longitude != null &&
      latitude != 0 &&
      longitude != 0 &&
      status != FleetTrackStatus.neverSeen;

  FleetVehicleLocation copyWith({
    double? latitude,
    double? longitude,
    DateTime? lastUpdated,
    double? speed,
    FleetTrackStatus? status,
    String? driverName,
    bool? ignition,
    double? heading,
    double? batteryLevel,
    int? gsmSignal,
    String? address,
    String? alarmType,
  }) {
    return FleetVehicleLocation(
      vehicleId: vehicleId,
      vehicleName: vehicleName,
      registrationNumber: registrationNumber,
      status: status ?? this.status,
      latitude: latitude ?? this.latitude,
      longitude: longitude ?? this.longitude,
      lastUpdated: lastUpdated ?? this.lastUpdated,
      speed: speed ?? this.speed,
      driverName: driverName ?? this.driverName,
      hasGps: hasGps,
      ignition: ignition ?? this.ignition,
      heading: heading ?? this.heading,
      batteryLevel: batteryLevel ?? this.batteryLevel,
      gsmSignal: gsmSignal ?? this.gsmSignal,
      address: address ?? this.address,
      alarmType: alarmType ?? this.alarmType,
      vehicleType: vehicleType,
      serviceAlert: serviceAlert,
    );
  }
}

DateTime? _parseDate(Object? raw) {
  if (raw == null) return null;
  if (raw is DateTime) return raw;
  return DateTime.tryParse(raw.toString());
}

class VehicleDocumentItem {
  const VehicleDocumentItem({
    required this.id,
    required this.vehicleId,
    required this.documentType,
    this.fileUrl,
    this.expiryDate,
    this.notes,
  });

  final int id;
  final int vehicleId;
  final String documentType;
  final String? fileUrl;
  final DateTime? expiryDate;
  final String? notes;

  factory VehicleDocumentItem.fromJson(Map<String, dynamic> json) {
    return VehicleDocumentItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      documentType: json['documentType'] as String? ??
          json['DocumentType'] as String? ??
          '',
      fileUrl: json['fileUrl'] as String? ?? json['FileUrl'] as String?,
      expiryDate: _parseDate(json['expiryDate'] ?? json['ExpiryDate']),
      notes: json['notes'] as String? ?? json['Notes'] as String?,
    );
  }
}

class VehicleMaintenanceItem {
  const VehicleMaintenanceItem({
    required this.id,
    required this.vehicleId,
    required this.description,
    required this.cost,
    required this.maintenanceDate,
    this.nextDueDate,
    required this.status,
    this.serviceProvider,
  });

  final int id;
  final int vehicleId;
  final String description;
  final double cost;
  final DateTime maintenanceDate;
  final DateTime? nextDueDate;
  final String status;
  final String? serviceProvider;

  factory VehicleMaintenanceItem.fromJson(Map<String, dynamic> json) {
    return VehicleMaintenanceItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      description: json['description'] as String? ??
          json['Description'] as String? ??
          '',
      cost: (json['cost'] as num? ?? json['Cost'] as num? ?? 0).toDouble(),
      maintenanceDate: _parseDate(
            json['maintenanceDate'] ?? json['MaintenanceDate'],
          ) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      nextDueDate: _parseDate(json['nextDueDate'] ?? json['NextDueDate']),
      status: '${json['status'] ?? json['Status'] ?? ''}',
      serviceProvider: json['serviceProvider'] as String? ??
          json['ServiceProvider'] as String?,
    );
  }
}

class VehicleFuelItem {
  const VehicleFuelItem({
    required this.id,
    required this.vehicleId,
    required this.liters,
    required this.totalCost,
    required this.fuelDate,
    this.station,
    this.odometerReading,
    this.fuelType,
  });

  final int id;
  final int vehicleId;
  final double liters;
  final double totalCost;
  final DateTime fuelDate;
  final String? station;
  final double? odometerReading;
  final String? fuelType;

  factory VehicleFuelItem.fromJson(Map<String, dynamic> json) {
    return VehicleFuelItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      liters: (json['liters'] as num? ?? json['Liters'] as num? ?? 0).toDouble(),
      totalCost:
          (json['totalCost'] as num? ?? json['TotalCost'] as num? ?? 0)
              .toDouble(),
      fuelDate: _parseDate(json['fuelDate'] ?? json['FuelDate']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      station: json['station'] as String? ?? json['Station'] as String?,
      odometerReading: (json['odometerReading'] as num? ??
              json['OdometerReading'] as num?)
          ?.toDouble(),
      fuelType: '${json['fuelType'] ?? json['FuelType'] ?? ''}',
    );
  }
}

class VehicleFuelSummary {
  const VehicleFuelSummary({
    required this.items,
    required this.totalLiters,
    required this.totalCost,
    required this.totalCount,
  });

  final List<VehicleFuelItem> items;
  final double totalLiters;
  final double totalCost;
  final int totalCount;

  factory VehicleFuelSummary.fromJson(Map<String, dynamic> json) {
    final raw = json['items'] ?? json['Items'];
    final items = raw is List
        ? raw
            .whereType<Map>()
            .map((e) => VehicleFuelItem.fromJson(Map<String, dynamic>.from(e)))
            .toList()
        : <VehicleFuelItem>[];
    return VehicleFuelSummary(
      items: items,
      totalLiters:
          (json['totalLiters'] as num? ?? json['TotalLiters'] as num? ?? 0)
              .toDouble(),
      totalCost:
          (json['totalCost'] as num? ?? json['TotalCost'] as num? ?? 0)
              .toDouble(),
      totalCount:
          json['totalCount'] as int? ?? json['TotalCount'] as int? ?? items.length,
    );
  }
}

class VehicleGpsInfo {
  const VehicleGpsInfo({
    this.gpsDeviceId,
    this.deviceName,
    this.uniqueId,
    this.isActive,
    this.lastSeenAt,
    this.lastIgnition,
    this.latitude,
    this.longitude,
    this.speed,
    this.lastUpdate,
    this.simNumber,
    this.modelName,
    this.brandName,
    this.totalDistanceKm,
    this.batteryLevel,
    this.gsmSignal,
    this.address,
    this.gpsOnline = false,
    this.heading,
    this.fuelLevel,
  });

  final int? gpsDeviceId;
  final String? deviceName;
  final String? uniqueId;
  final bool? isActive;
  final DateTime? lastSeenAt;
  final bool? lastIgnition;
  final double? latitude;
  final double? longitude;
  final double? speed;
  final DateTime? lastUpdate;
  final String? simNumber;
  final String? modelName;
  final String? brandName;
  final double? totalDistanceKm;
  final double? batteryLevel;
  final int? gsmSignal;
  final String? address;
  final bool gpsOnline;
  final double? heading;
  final double? fuelLevel;

  factory VehicleGpsInfo.fromJson(Map<String, dynamic> json) {
    return VehicleGpsInfo(
      gpsDeviceId:
          json['gpsDeviceId'] as int? ?? json['GpsDeviceId'] as int?,
      deviceName:
          json['deviceName'] as String? ?? json['DeviceName'] as String?,
      uniqueId: json['uniqueId'] as String? ?? json['UniqueId'] as String?,
      isActive: json['isActive'] as bool? ?? json['IsActive'] as bool?,
      lastSeenAt: _parseDate(json['lastSeenAt'] ?? json['LastSeenAt']),
      lastIgnition:
          json['lastIgnition'] as bool? ?? json['LastIgnition'] as bool?,
      latitude:
          (json['latitude'] as num? ?? json['Latitude'] as num?)?.toDouble(),
      longitude:
          (json['longitude'] as num? ?? json['Longitude'] as num?)?.toDouble(),
      speed: (json['speed'] as num? ?? json['Speed'] as num?)?.toDouble(),
      lastUpdate: _parseDate(json['lastUpdate'] ?? json['LastUpdate']),
      simNumber: json['simNumber'] as String? ?? json['SimNumber'] as String?,
      modelName: json['modelName'] as String? ?? json['ModelName'] as String?,
      brandName: json['brandName'] as String? ?? json['BrandName'] as String?,
      totalDistanceKm: (json['totalDistanceKm'] as num? ??
              json['TotalDistanceKm'] as num?)
          ?.toDouble(),
      batteryLevel: (json['batteryLevel'] as num? ??
              json['BatteryLevel'] as num?)
          ?.toDouble(),
      gsmSignal: json['gsmSignal'] as int? ?? json['GsmSignal'] as int?,
      address: json['address'] as String? ?? json['Address'] as String?,
      gpsOnline: json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
      heading: (json['heading'] as num? ?? json['Heading'] as num?)?.toDouble(),
      fuelLevel:
          (json['fuelLevel'] as num? ?? json['FuelLevel'] as num?)?.toDouble(),
    );
  }
}

class HistoryReplayPoint {
  const HistoryReplayPoint({
    required this.timestamp,
    required this.latitude,
    required this.longitude,
    required this.speedKmh,
    this.heading,
    this.ignition,
    this.address,
  });

  final DateTime timestamp;
  final double latitude;
  final double longitude;
  final double speedKmh;
  final double? heading;
  final bool? ignition;
  final String? address;

  factory HistoryReplayPoint.fromJson(Map<String, dynamic> json) {
    return HistoryReplayPoint(
      timestamp: _parseDate(json['timestamp'] ?? json['Timestamp']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      latitude:
          (json['latitude'] as num? ?? json['Latitude'] as num? ?? 0).toDouble(),
      longitude: (json['longitude'] as num? ?? json['Longitude'] as num? ?? 0)
          .toDouble(),
      speedKmh:
          (json['speedKmh'] as num? ?? json['SpeedKmh'] as num? ?? 0).toDouble(),
      heading: (json['heading'] as num? ?? json['Heading'] as num?)?.toDouble(),
      ignition: json['ignition'] as bool? ?? json['Ignition'] as bool?,
      address: json['address'] as String? ?? json['Address'] as String?,
    );
  }
}

class HistoryReplayBundle {
  const HistoryReplayBundle({
    required this.route,
    required this.playback,
    this.mileageKm,
    this.gpsDeviceId,
  });

  final List<HistoryReplayPoint> route;
  final List<HistoryReplayPoint> playback;
  final double? mileageKm;
  final int? gpsDeviceId;

  List<HistoryReplayPoint> get points =>
      playback.isNotEmpty ? playback : route;

  factory HistoryReplayBundle.fromJson(Map<String, dynamic> json) {
    List<HistoryReplayPoint> parsePoints(Object? raw) {
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) => HistoryReplayPoint.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }

    final vehicle = json['vehicle'] ?? json['Vehicle'];
    int? deviceId;
    if (vehicle is Map) {
      deviceId = vehicle['gpsDeviceId'] as int? ?? vehicle['GpsDeviceId'] as int?;
    }

    return HistoryReplayBundle(
      route: parsePoints(json['route'] ?? json['Route']),
      playback: parsePoints(json['playback'] ?? json['Playback']),
      mileageKm:
          (json['mileageKm'] as num? ?? json['MileageKm'] as num?)?.toDouble(),
      gpsDeviceId: deviceId,
    );
  }
}

class SupportedGpsCommand {
  const SupportedGpsCommand({
    required this.type,
    required this.label,
    required this.available,
    this.reason,
  });

  final String type;
  final String label;
  final bool available;
  final String? reason;

  factory SupportedGpsCommand.fromJson(Map<String, dynamic> json) {
    return SupportedGpsCommand(
      type: json['type'] as String? ?? json['Type'] as String? ?? '',
      label: json['label'] as String? ?? json['Label'] as String? ?? '',
      available:
          json['available'] as bool? ?? json['Available'] as bool? ?? false,
      reason: json['reason'] as String? ?? json['Reason'] as String?,
    );
  }
}

class GpsDeviceCommandItem {
  const GpsDeviceCommandItem({
    required this.id,
    required this.commandType,
    required this.status,
    required this.requestedAt,
    this.reason,
    this.errorMessage,
  });

  final int id;
  final String commandType;
  final String status;
  final DateTime requestedAt;
  final String? reason;
  final String? errorMessage;

  factory GpsDeviceCommandItem.fromJson(Map<String, dynamic> json) {
    return GpsDeviceCommandItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      commandType: json['commandType'] as String? ??
          json['CommandType'] as String? ??
          '',
      status: json['status'] as String? ?? json['Status'] as String? ?? '',
      requestedAt: _parseDate(json['requestedAt'] ?? json['RequestedAt']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      reason: json['reason'] as String? ?? json['Reason'] as String?,
      errorMessage:
          json['errorMessage'] as String? ?? json['ErrorMessage'] as String?,
    );
  }
}

class GpsGeofenceItem {
  const GpsGeofenceItem({
    required this.id,
    required this.name,
    required this.areaType,
    required this.centerLat,
    required this.centerLng,
    required this.radiusMeters,
    this.geoJson,
    this.isActive = true,
    this.color,
  });

  final int id;
  final String name;
  final String areaType;
  final double centerLat;
  final double centerLng;
  final double radiusMeters;
  final String? geoJson;
  final bool isActive;
  final String? color;

  bool get isCircle =>
      areaType.toLowerCase().contains('circle') || radiusMeters > 0;

  factory GpsGeofenceItem.fromJson(Map<String, dynamic> json) {
    return GpsGeofenceItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      name: json['name'] as String? ?? json['Name'] as String? ?? '',
      areaType: json['areaType'] as String? ??
          json['AreaType'] as String? ??
          json['type'] as String? ??
          '',
      centerLat: (json['centerLat'] as num? ?? json['CenterLat'] as num? ?? 0)
          .toDouble(),
      centerLng: (json['centerLng'] as num? ?? json['CenterLng'] as num? ?? 0)
          .toDouble(),
      radiusMeters:
          (json['radiusMeters'] as num? ?? json['RadiusMeters'] as num? ?? 0)
              .toDouble(),
      geoJson: json['geoJson'] as String? ?? json['GeoJson'] as String?,
      isActive: json['isActive'] as bool? ?? json['IsActive'] as bool? ?? true,
      color: json['color'] as String? ?? json['Color'] as String?,
    );
  }
}
