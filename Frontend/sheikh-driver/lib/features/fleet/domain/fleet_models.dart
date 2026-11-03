/// Fleet track status — mirrors ERP `resolveFleetStatus` / backend calculator.
enum FleetTrackStatus {
  moving,
  idle,
  parked,
  offline,
  neverSeen,
  sos,
  unknown,
}

extension FleetTrackStatusX on FleetTrackStatus {
  String get label => switch (this) {
        FleetTrackStatus.moving => 'Moving',
        FleetTrackStatus.idle => 'Idle',
        FleetTrackStatus.parked => 'Parked',
        FleetTrackStatus.offline => 'Offline',
        FleetTrackStatus.neverSeen => 'Never seen',
        FleetTrackStatus.sos => 'SOS',
        FleetTrackStatus.unknown => 'Unknown',
      };

  String get filterKey => switch (this) {
        FleetTrackStatus.moving => 'moving',
        FleetTrackStatus.idle => 'idle',
        FleetTrackStatus.parked => 'parked',
        FleetTrackStatus.offline => 'offline',
        FleetTrackStatus.neverSeen => 'never_seen',
        FleetTrackStatus.sos => 'sos',
        FleetTrackStatus.unknown => 'unknown',
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
    this.unknown = 0,
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
  final int unknown;

  factory GpsFleetStatusKpis.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) => _readInt(json, a, b);
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
      unknown: n('unknown', 'Unknown'),
    );
  }

  static int _readInt(Map<String, dynamic> json, String a, [String? b]) {
    final raw = json[a] ?? (b != null ? json[b] : null);
    if (raw == null) return 0;
    if (raw is int) return raw;
    if (raw is num) return raw.round();
    return int.tryParse(raw.toString()) ?? 0;
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
    int n(String a, [String? b]) {
      final raw = json[a] ?? (b != null ? json[b] : null);
      if (raw == null) return 0;
      if (raw is int) return raw;
      if (raw is num) return raw.round();
      return int.tryParse(raw.toString()) ?? 0;
    }

    num cost =
        json['monthlyFuelCost'] as num? ?? json['MonthlyFuelCost'] as num? ?? 0;
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
    this.isGpsValid = true,
    this.gpsQualityCode,
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
  final bool isGpsValid;
  final String? gpsQualityCode;

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
      batteryLevel:
          (json['batteryLevel'] as num? ?? json['BatteryLevel'] as num?)
              ?.toDouble(),
      gsmSignal: json['gsmSignal'] as int? ?? json['GsmSignal'] as int?,
      totalDistanceKm:
          (json['totalDistanceKm'] as num? ?? json['TotalDistanceKm'] as num?)
              ?.toDouble(),
      address: json['address'] as String? ?? json['Address'] as String?,
      alarmType: json['alarmType'] as String? ?? json['AlarmType'] as String?,
      driverPhone:
          json['driverPhone'] as String? ?? json['DriverPhone'] as String?,
      isGpsValid:
          json['isGpsValid'] as bool? ?? json['IsGpsValid'] as bool? ?? true,
      gpsQualityCode:
          json['gpsQualityCode'] as String? ?? json['GpsQualityCode'] as String?,
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
    this.driverPhone,
    this.engineIgnition,
    this.gpsLastSeenAt,
    this.gpsOnline = false,
    this.hasGpsDevice = false,
    this.locationLatitude,
    this.locationLongitude,
    this.locationLastUpdate,
    this.serviceAlert,
    this.imageUrl,
    this.insuranceExpiryDate,
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
  final String? driverPhone;
  final bool? engineIgnition;
  final DateTime? gpsLastSeenAt;
  final bool gpsOnline;
  final bool hasGpsDevice;
  final double? locationLatitude;
  final double? locationLongitude;
  final DateTime? locationLastUpdate;
  final String? serviceAlert;
  final String? imageUrl;
  final DateTime? insuranceExpiryDate;

  bool get isRetired {
    final s = status.toLowerCase();
    return s == 'retired' || s == '4';
  }

  /// Matches backend [DriverAssignmentOps.EnsureVehicleAssignableAsync].
  bool get isAssignableForDriver {
    final s = status.trim().toLowerCase();
    if (s == 'draft' ||
        s == '5' ||
        s == 'retired' ||
        s == '4' ||
        s == 'maintenance' ||
        s == '3') {
      return false;
    }
    final expiry = insuranceExpiryDate;
    if (expiry != null) {
      final today = DateTime.now().toUtc();
      final day = DateTime.utc(today.year, today.month, today.day);
      final expDay = DateTime.utc(expiry.year, expiry.month, expiry.day);
      if (expDay.isBefore(day)) return false;
    }
    return true;
  }

  String? get assignBlockReason {
    final s = status.trim().toLowerCase();
    if (s == 'draft' || s == '5') return 'Draft — not assignable';
    if (s == 'retired' || s == '4') return 'Retired — not assignable';
    if (s == 'maintenance' || s == '3') return 'Under maintenance';
    final expiry = insuranceExpiryDate;
    if (expiry != null) {
      final today = DateTime.now().toUtc();
      final day = DateTime.utc(today.year, today.month, today.day);
      final expDay = DateTime.utc(expiry.year, expiry.month, expiry.day);
      if (expDay.isBefore(day)) return 'Insurance expired';
    }
    return null;
  }

  bool get hasAssignee =>
      driverId != null || (driverName != null && driverName!.trim().isNotEmpty);

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
      driverPhone:
          json['driverPhone'] as String? ?? json['DriverPhone'] as String?,
      engineIgnition:
          json['engineIgnition'] as bool? ?? json['EngineIgnition'] as bool?,
      gpsLastSeenAt: _parseDate(json['gpsLastSeenAt'] ?? json['GpsLastSeenAt']),
      gpsOnline:
          json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
      hasGpsDevice: json['hasGpsDevice'] as bool? ??
          json['HasGpsDevice'] as bool? ??
          false,
      locationLatitude:
          (json['locationLatitude'] as num? ?? json['LocationLatitude'] as num?)
              ?.toDouble(),
      locationLongitude: (json['locationLongitude'] as num? ??
              json['LocationLongitude'] as num?)
          ?.toDouble(),
      locationLastUpdate:
          _parseDate(json['locationLastUpdate'] ?? json['LocationLastUpdate']),
      serviceAlert:
          json['serviceAlert'] as String? ?? json['ServiceAlert'] as String?,
      imageUrl: json['imageUrl'] as String? ?? json['ImageUrl'] as String?,
      insuranceExpiryDate: _parseDate(
        json['insuranceExpiryDate'] ?? json['InsuranceExpiryDate'],
      ),
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
      currentMileage:
          (json['currentMileage'] as num? ?? json['CurrentMileage'] as num?)
              ?.toDouble(),
      driverName:
          json['driverName'] as String? ?? json['DriverName'] as String?,
      driverId: json['driverId'] as int? ?? json['DriverId'] as int?,
      driverPhone:
          json['driverPhone'] as String? ?? json['DriverPhone'] as String?,
      gpsImei: json['gpsImei'] as String? ?? json['GpsImei'] as String?,
      gpsOnline:
          json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
      hasGpsDevice: json['hasGpsDevice'] as bool? ??
          json['HasGpsDevice'] as bool? ??
          false,
      engineIgnition:
          json['engineIgnition'] as bool? ?? json['EngineIgnition'] as bool?,
      locationLatitude:
          (json['locationLatitude'] as num? ?? json['LocationLatitude'] as num?)
              ?.toDouble(),
      locationLongitude: (json['locationLongitude'] as num? ??
              json['LocationLongitude'] as num?)
          ?.toDouble(),
      locationLastUpdate:
          _parseDate(json['locationLastUpdate'] ?? json['LocationLastUpdate']),
      locationSpeed:
          (json['locationSpeed'] as num? ?? json['LocationSpeed'] as num?)
              ?.toDouble(),
      batteryLevel:
          (json['batteryLevel'] as num? ?? json['BatteryLevel'] as num?)
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
    this.driverPhone,
    this.hasGps = false,
    this.ignition,
    this.heading,
    this.batteryLevel,
    this.gsmSignal,
    this.address,
    this.alarmType,
    this.vehicleType,
    this.serviceAlert,
    this.isGpsValid = true,
    this.gpsQualityCode,
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
  final String? driverPhone;
  final bool hasGps;
  final bool? ignition;
  final double? heading;
  final double? batteryLevel;
  final int? gsmSignal;
  final String? address;
  final String? alarmType;
  final String? vehicleType;
  final String? serviceAlert;
  final bool isGpsValid;
  final String? gpsQualityCode;

  /// True when the vehicle has a last-known map position.
  /// Online/offline/never-seen status must not hide pins — style differs by status.
  bool get hasMapCoords =>
      latitude != null &&
      longitude != null &&
      latitude != 0 &&
      longitude != 0;

  FleetVehicleLocation copyWith({
    double? latitude,
    double? longitude,
    DateTime? lastUpdated,
    double? speed,
    FleetTrackStatus? status,
    String? driverName,
    String? driverPhone,
    bool? hasGps,
    bool? ignition,
    bool clearIgnition = false,
    double? heading,
    double? batteryLevel,
    int? gsmSignal,
    String? address,
    String? alarmType,
    bool? isGpsValid,
    String? gpsQualityCode,
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
      driverPhone: driverPhone ?? this.driverPhone,
      hasGps: hasGps ?? this.hasGps,
      ignition: clearIgnition ? ignition : (ignition ?? this.ignition),
      heading: heading ?? this.heading,
      batteryLevel: batteryLevel ?? this.batteryLevel,
      gsmSignal: gsmSignal ?? this.gsmSignal,
      address: address ?? this.address,
      alarmType: alarmType ?? this.alarmType,
      vehicleType: vehicleType,
      serviceAlert: serviceAlert,
      isGpsValid: isGpsValid ?? this.isGpsValid,
      gpsQualityCode: gpsQualityCode ?? this.gpsQualityCode,
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
      liters:
          (json['liters'] as num? ?? json['Liters'] as num? ?? 0).toDouble(),
      totalCost: (json['totalCost'] as num? ?? json['TotalCost'] as num? ?? 0)
          .toDouble(),
      fuelDate: _parseDate(json['fuelDate'] ?? json['FuelDate']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      station: json['station'] as String? ?? json['Station'] as String?,
      odometerReading:
          (json['odometerReading'] as num? ?? json['OdometerReading'] as num?)
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
      totalCost: (json['totalCost'] as num? ?? json['TotalCost'] as num? ?? 0)
          .toDouble(),
      totalCount: json['totalCount'] as int? ??
          json['TotalCount'] as int? ??
          items.length,
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
      gpsDeviceId: json['gpsDeviceId'] as int? ?? json['GpsDeviceId'] as int?,
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
      totalDistanceKm:
          (json['totalDistanceKm'] as num? ?? json['TotalDistanceKm'] as num?)
              ?.toDouble(),
      batteryLevel:
          (json['batteryLevel'] as num? ?? json['BatteryLevel'] as num?)
              ?.toDouble(),
      gsmSignal: json['gsmSignal'] as int? ?? json['GsmSignal'] as int?,
      address: json['address'] as String? ?? json['Address'] as String?,
      gpsOnline:
          json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
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
    this.altitude,
    this.batteryLevel,
    this.satellites,
    this.totalDistanceKm,
  });

  final DateTime timestamp;
  final double latitude;
  final double longitude;
  final double speedKmh;
  final double? heading;
  final bool? ignition;
  final String? address;
  final double? altitude;
  final double? batteryLevel;
  final int? satellites;
  final double? totalDistanceKm;

  factory HistoryReplayPoint.fromJson(Map<String, dynamic> json) {
    return HistoryReplayPoint(
      timestamp: _parseDate(json['timestamp'] ?? json['Timestamp']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      latitude: (json['latitude'] as num? ?? json['Latitude'] as num? ?? 0)
          .toDouble(),
      longitude: (json['longitude'] as num? ?? json['Longitude'] as num? ?? 0)
          .toDouble(),
      speedKmh: (json['speedKmh'] as num? ?? json['SpeedKmh'] as num? ?? 0)
          .toDouble(),
      heading: (json['heading'] as num? ?? json['Heading'] as num?)?.toDouble(),
      ignition: json['ignition'] as bool? ?? json['Ignition'] as bool?,
      address: json['address'] as String? ?? json['Address'] as String?,
      altitude:
          (json['altitude'] as num? ?? json['Altitude'] as num?)?.toDouble(),
      batteryLevel:
          (json['batteryLevel'] as num? ?? json['BatteryLevel'] as num?)
              ?.toDouble(),
      satellites: json['satellites'] as int? ?? json['Satellites'] as int?,
      totalDistanceKm:
          (json['totalDistanceKm'] as num? ?? json['TotalDistanceKm'] as num?)
              ?.toDouble(),
    );
  }
}

class TripReplaySummary {
  const TripReplaySummary({
    required this.distanceKm,
    required this.drivingMinutes,
    required this.avgSpeedKmh,
    required this.maxSpeedKmh,
    this.fuelLiters,
    this.engineHours,
  });

  final double distanceKm;
  final int drivingMinutes;
  final double avgSpeedKmh;
  final double maxSpeedKmh;
  final double? fuelLiters;
  final double? engineHours;

  factory TripReplaySummary.fromJson(Map<String, dynamic> json) {
    return TripReplaySummary(
      distanceKm:
          (json['distanceKm'] as num? ?? json['DistanceKm'] as num? ?? 0)
              .toDouble(),
      drivingMinutes:
          json['drivingMinutes'] as int? ?? json['DrivingMinutes'] as int? ?? 0,
      avgSpeedKmh:
          (json['avgSpeedKmh'] as num? ?? json['AvgSpeedKmh'] as num? ?? 0)
              .toDouble(),
      maxSpeedKmh:
          (json['maxSpeedKmh'] as num? ?? json['MaxSpeedKmh'] as num? ?? 0)
              .toDouble(),
      fuelLiters: (json['fuelLiters'] as num? ?? json['FuelLiters'] as num?)
          ?.toDouble(),
      engineHours: (json['engineHours'] as num? ?? json['EngineHours'] as num?)
          ?.toDouble(),
    );
  }
}

class TripAnalyticsSummary {
  const TripAnalyticsSummary({
    required this.tripCount,
    required this.distanceKm,
    required this.drivingMinutes,
    required this.idleMinutes,
    required this.avgSpeedKmh,
    required this.maxSpeedKmh,
    required this.stopCount,
    required this.overspeedCount,
    required this.harshBrakeCount,
    required this.harshAccelCount,
    this.fuelLiters,
    this.engineHours,
  });

  final int tripCount;
  final double distanceKm;
  final int drivingMinutes;
  final int idleMinutes;
  final double avgSpeedKmh;
  final double maxSpeedKmh;
  final int stopCount;
  final int overspeedCount;
  final int harshBrakeCount;
  final int harshAccelCount;
  final double? fuelLiters;
  final double? engineHours;

  factory TripAnalyticsSummary.fromJson(Map<String, dynamic> json) {
    return TripAnalyticsSummary(
      tripCount: json['tripCount'] as int? ?? json['TripCount'] as int? ?? 0,
      distanceKm:
          (json['distanceKm'] as num? ?? json['DistanceKm'] as num? ?? 0)
              .toDouble(),
      drivingMinutes:
          json['drivingMinutes'] as int? ?? json['DrivingMinutes'] as int? ?? 0,
      idleMinutes:
          json['idleMinutes'] as int? ?? json['IdleMinutes'] as int? ?? 0,
      avgSpeedKmh:
          (json['avgSpeedKmh'] as num? ?? json['AvgSpeedKmh'] as num? ?? 0)
              .toDouble(),
      maxSpeedKmh:
          (json['maxSpeedKmh'] as num? ?? json['MaxSpeedKmh'] as num? ?? 0)
              .toDouble(),
      stopCount: json['stopCount'] as int? ?? json['StopCount'] as int? ?? 0,
      overspeedCount:
          json['overspeedCount'] as int? ?? json['OverspeedCount'] as int? ?? 0,
      harshBrakeCount: json['harshBrakeCount'] as int? ??
          json['HarshBrakeCount'] as int? ??
          0,
      harshAccelCount: json['harshAccelCount'] as int? ??
          json['HarshAccelCount'] as int? ??
          0,
      fuelLiters: (json['fuelLiters'] as num? ?? json['FuelLiters'] as num?)
          ?.toDouble(),
      engineHours: (json['engineHours'] as num? ?? json['EngineHours'] as num?)
          ?.toDouble(),
    );
  }
}

class TripAnalyticsBundle {
  const TripAnalyticsBundle({
    required this.summary,
    this.events = const [],
    this.stops = const [],
  });

  final TripAnalyticsSummary summary;
  final List<TripEvent> events;
  final List<TripStop> stops;

  factory TripAnalyticsBundle.fromJson(Map<String, dynamic> json) {
    final summaryRaw = json['summary'] ?? json['Summary'];
    final summaryMap = summaryRaw is Map
        ? Map<String, dynamic>.from(summaryRaw)
        : <String, dynamic>{};
    final eventsRaw = json['events'] ?? json['Events'];
    final stopsRaw = json['stops'] ?? json['Stops'];
    return TripAnalyticsBundle(
      summary: TripAnalyticsSummary.fromJson(summaryMap),
      events: eventsRaw is List
          ? eventsRaw
              .whereType<Map>()
              .map((e) => TripEvent.fromJson(Map<String, dynamic>.from(e)))
              .toList()
          : const [],
      stops: stopsRaw is List
          ? stopsRaw
              .whereType<Map>()
              .map((e) => TripStop.fromJson(Map<String, dynamic>.from(e)))
              .toList()
          : const [],
    );
  }
}

class TripStop {
  const TripStop({
    required this.startTime,
    required this.endTime,
    required this.latitude,
    required this.longitude,
    this.address,
    required this.durationMinutes,
  });

  final DateTime startTime;
  final DateTime endTime;
  final double latitude;
  final double longitude;
  final String? address;
  final int durationMinutes;

  factory TripStop.fromJson(Map<String, dynamic> json) {
    // Prefer latitude/longitude (API TripStopDto / ResolvedLat|Lon); fall back
    // to legacy Traccar lat/lon aliases if a raw payload ever leaks through.
    final lat = (json['latitude'] as num? ??
            json['Latitude'] as num? ??
            json['lat'] as num? ??
            json['Lat'] as num? ??
            0)
        .toDouble();
    final lon = (json['longitude'] as num? ??
            json['Longitude'] as num? ??
            json['lon'] as num? ??
            json['Lon'] as num? ??
            0)
        .toDouble();
    return TripStop(
      startTime: _parseDate(json['startTime'] ?? json['StartTime']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      endTime: _parseDate(json['endTime'] ?? json['EndTime']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      latitude: lat,
      longitude: lon,
      address: json['address'] as String? ?? json['Address'] as String?,
      durationMinutes: json['durationMinutes'] as int? ??
          json['DurationMinutes'] as int? ??
          0,
    );
  }
}

class TripEvent {
  const TripEvent({
    required this.time,
    required this.type,
    this.latitude,
    this.longitude,
    this.address,
    this.speedKmh,
    this.geofenceId,
    this.geofenceName,
    this.label,
  });

  final DateTime time;
  final String type;
  final double? latitude;
  final double? longitude;
  final String? address;
  final double? speedKmh;
  final int? geofenceId;
  final String? geofenceName;
  final String? label;

  factory TripEvent.fromJson(Map<String, dynamic> json) {
    return TripEvent(
      time: _parseDate(json['time'] ?? json['Time']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      type: json['type'] as String? ?? json['Type'] as String? ?? '',
      latitude:
          (json['latitude'] as num? ?? json['Latitude'] as num?)?.toDouble(),
      longitude:
          (json['longitude'] as num? ?? json['Longitude'] as num?)?.toDouble(),
      address: json['address'] as String? ?? json['Address'] as String?,
      speedKmh:
          (json['speedKmh'] as num? ?? json['SpeedKmh'] as num?)?.toDouble(),
      geofenceId: json['geofenceId'] as int? ?? json['GeofenceId'] as int?,
      geofenceName:
          json['geofenceName'] as String? ?? json['GeofenceName'] as String?,
      label: json['label'] as String? ?? json['Label'] as String?,
    );
  }
}

class TripDeviceContext {
  const TripDeviceContext({
    required this.vehicleId,
    this.vehicleName,
    this.plateNumber,
    this.gpsDeviceId,
    this.deviceName,
    this.uniqueId,
    this.hasTraccarLink = false,
    this.isOnline = false,
    this.lastPositionAt,
    this.lastLatitude,
    this.lastLongitude,
    this.lastAddress,
    this.lastSpeedKmh,
    this.lastIgnition,
  });

  final int vehicleId;
  final String? vehicleName;
  final String? plateNumber;
  final int? gpsDeviceId;
  final String? deviceName;
  final String? uniqueId;
  final bool hasTraccarLink;
  final bool isOnline;
  final DateTime? lastPositionAt;
  final double? lastLatitude;
  final double? lastLongitude;
  final String? lastAddress;
  final double? lastSpeedKmh;
  final bool? lastIgnition;

  factory TripDeviceContext.fromJson(Map<String, dynamic> json) {
    return TripDeviceContext(
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      plateNumber:
          json['plateNumber'] as String? ?? json['PlateNumber'] as String?,
      gpsDeviceId: json['gpsDeviceId'] as int? ?? json['GpsDeviceId'] as int?,
      deviceName:
          json['deviceName'] as String? ?? json['DeviceName'] as String?,
      uniqueId: json['uniqueId'] as String? ?? json['UniqueId'] as String?,
      hasTraccarLink: json['hasTraccarLink'] as bool? ??
          json['HasTraccarLink'] as bool? ??
          false,
      isOnline: json['isOnline'] as bool? ?? json['IsOnline'] as bool? ?? false,
      lastPositionAt: _parseDate(
        json['lastPositionAt'] ?? json['LastPositionAt'],
      ),
      lastLatitude:
          (json['lastLatitude'] as num? ?? json['LastLatitude'] as num?)
              ?.toDouble(),
      lastLongitude:
          (json['lastLongitude'] as num? ?? json['LastLongitude'] as num?)
              ?.toDouble(),
      lastAddress:
          json['lastAddress'] as String? ?? json['LastAddress'] as String?,
      lastSpeedKmh:
          (json['lastSpeedKmh'] as num? ?? json['LastSpeedKmh'] as num?)
              ?.toDouble(),
      lastIgnition:
          json['lastIgnition'] as bool? ?? json['LastIgnition'] as bool?,
    );
  }
}

class HistoryReplayBundle {
  const HistoryReplayBundle({
    required this.route,
    required this.playback,
    this.stops = const [],
    this.events = const [],
    this.summary,
    this.statistics,
    this.mileageKm,
    this.vehicle,
    this.gpsDeviceId,
  });

  final List<HistoryReplayPoint> route;
  final List<HistoryReplayPoint> playback;
  final List<TripStop> stops;
  final List<TripEvent> events;
  final TripReplaySummary? summary;
  final TripAnalyticsSummary? statistics;
  final double? mileageKm;
  final TripDeviceContext? vehicle;
  final int? gpsDeviceId;

  List<HistoryReplayPoint> get points => playback.isNotEmpty ? playback : route;

  /// Full trail for map coloring (prefer dense route).
  List<HistoryReplayPoint> get trailPoints => route.isNotEmpty ? route : points;

  factory HistoryReplayBundle.fromJson(Map<String, dynamic> json) {
    List<HistoryReplayPoint> parsePoints(Object? raw) {
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) => HistoryReplayPoint.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }

    List<TripStop> parseStops(Object? raw) {
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) => TripStop.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }

    List<TripEvent> parseEvents(Object? raw) {
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) => TripEvent.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }

    final vehicleRaw = json['vehicle'] ?? json['Vehicle'];
    TripDeviceContext? vehicleCtx;
    int? deviceId;
    if (vehicleRaw is Map) {
      final vm = Map<String, dynamic>.from(vehicleRaw);
      vehicleCtx = TripDeviceContext.fromJson(vm);
      deviceId = vehicleCtx.gpsDeviceId;
    }

    final summaryRaw = json['summary'] ?? json['Summary'];
    final statsRaw = json['statistics'] ?? json['Statistics'];

    return HistoryReplayBundle(
      route: parsePoints(json['route'] ?? json['Route']),
      playback: parsePoints(json['playback'] ?? json['Playback']),
      stops: parseStops(json['stops'] ?? json['Stops']),
      events: parseEvents(json['events'] ?? json['Events']),
      summary: summaryRaw is Map
          ? TripReplaySummary.fromJson(Map<String, dynamic>.from(summaryRaw))
          : null,
      statistics: statsRaw is Map
          ? TripAnalyticsSummary.fromJson(Map<String, dynamic>.from(statsRaw))
          : null,
      mileageKm:
          (json['mileageKm'] as num? ?? json['MileageKm'] as num?)?.toDouble(),
      vehicle: vehicleCtx,
      gpsDeviceId: deviceId,
    );
  }
}

/// Motion trip from `GET /gps/trips` (Traccar / GpsTrips / detector).
class GpsTrip {
  const GpsTrip({
    required this.vehicleId,
    required this.startTime,
    required this.endTime,
    required this.distanceKm,
    required this.avgSpeedKmh,
    required this.maxSpeedKmh,
    required this.durationMinutes,
    this.vehicleName,
    this.gpsDeviceId,
    this.deviceName,
    this.startAddress,
    this.endAddress,
    this.driverName,
    this.fuelLiters,
    this.plateNumber,
    this.tripKey,
    this.status,
    this.startLatitude,
    this.startLongitude,
    this.endLatitude,
    this.endLongitude,
  });

  final int vehicleId;
  final String? vehicleName;
  final int? gpsDeviceId;
  final DateTime startTime;
  final DateTime endTime;
  final double distanceKm;
  final double avgSpeedKmh;
  final double maxSpeedKmh;
  final int durationMinutes;
  final String? deviceName;
  final String? startAddress;
  final String? endAddress;
  final String? driverName;
  final double? fuelLiters;
  final String? plateNumber;
  final String? tripKey;
  final String? status;
  final double? startLatitude;
  final double? startLongitude;
  final double? endLatitude;
  final double? endLongitude;

  bool get hasStartCoords =>
      startLatitude != null &&
      startLongitude != null &&
      startLatitude != 0 &&
      startLongitude != 0;

  bool get hasEndCoords =>
      endLatitude != null &&
      endLongitude != null &&
      endLatitude != 0 &&
      endLongitude != 0;

  factory GpsTrip.fromJson(Map<String, dynamic> json) {
    return GpsTrip(
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      gpsDeviceId: json['gpsDeviceId'] as int? ?? json['GpsDeviceId'] as int?,
      startTime: _parseDate(json['startTime'] ?? json['StartTime']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      endTime: _parseDate(json['endTime'] ?? json['EndTime']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      distanceKm:
          (json['distanceKm'] as num? ?? json['DistanceKm'] as num? ?? 0)
              .toDouble(),
      avgSpeedKmh:
          (json['avgSpeedKmh'] as num? ?? json['AvgSpeedKmh'] as num? ?? 0)
              .toDouble(),
      maxSpeedKmh:
          (json['maxSpeedKmh'] as num? ?? json['MaxSpeedKmh'] as num? ?? 0)
              .toDouble(),
      durationMinutes: json['durationMinutes'] as int? ??
          json['DurationMinutes'] as int? ??
          0,
      deviceName:
          json['deviceName'] as String? ?? json['DeviceName'] as String?,
      startAddress:
          json['startAddress'] as String? ?? json['StartAddress'] as String?,
      endAddress:
          json['endAddress'] as String? ?? json['EndAddress'] as String?,
      driverName:
          json['driverName'] as String? ?? json['DriverName'] as String?,
      fuelLiters: (json['fuelLiters'] as num? ?? json['FuelLiters'] as num?)
          ?.toDouble(),
      plateNumber:
          json['plateNumber'] as String? ?? json['PlateNumber'] as String?,
      tripKey: json['tripKey'] as String? ?? json['TripKey'] as String?,
      status: json['status'] as String? ?? json['Status'] as String?,
      startLatitude:
          (json['startLatitude'] as num? ?? json['StartLatitude'] as num?)
              ?.toDouble(),
      startLongitude:
          (json['startLongitude'] as num? ?? json['StartLongitude'] as num?)
              ?.toDouble(),
      endLatitude: (json['endLatitude'] as num? ?? json['EndLatitude'] as num?)
          ?.toDouble(),
      endLongitude:
          (json['endLongitude'] as num? ?? json['EndLongitude'] as num?)
              ?.toDouble(),
    );
  }
}

/// Per-trip replay payload from `GET /gps/trips/{tripKey}`.
class TripDetailBundle {
  const TripDetailBundle({
    required this.trip,
    required this.route,
    required this.playback,
    this.stops = const [],
    this.events = const [],
    this.summary,
  });

  final GpsTrip trip;
  final List<HistoryReplayPoint> route;
  final List<HistoryReplayPoint> playback;
  final List<TripStop> stops;
  final List<TripEvent> events;
  final TripReplaySummary? summary;

  HistoryReplayBundle toHistoryReplayBundle({bool includeStops = true}) {
    return HistoryReplayBundle(
      route: route,
      playback: playback,
      stops: includeStops ? stops : const [],
      events: events,
      summary: summary,
      mileageKm: trip.distanceKm,
      vehicle: TripDeviceContext(
        vehicleId: trip.vehicleId,
        vehicleName: trip.vehicleName,
        plateNumber: trip.plateNumber,
        gpsDeviceId: trip.gpsDeviceId,
        deviceName: trip.deviceName,
      ),
      gpsDeviceId: trip.gpsDeviceId,
    );
  }

  factory TripDetailBundle.fromJson(Map<String, dynamic> json) {
    List<HistoryReplayPoint> parsePoints(Object? raw) {
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) => HistoryReplayPoint.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }

    List<TripStop> parseStops(Object? raw) {
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) => TripStop.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }

    List<TripEvent> parseEvents(Object? raw) {
      if (raw is! List) return const [];
      return raw
          .whereType<Map>()
          .map((e) => TripEvent.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }

    final tripRaw = json['trip'] ?? json['Trip'];
    final tripMap = tripRaw is Map
        ? Map<String, dynamic>.from(tripRaw)
        : <String, dynamic>{};
    final summaryRaw = json['summary'] ?? json['Summary'];

    return TripDetailBundle(
      trip: GpsTrip.fromJson(tripMap),
      route: parsePoints(json['route'] ?? json['Route']),
      playback: parsePoints(json['playback'] ?? json['Playback']),
      stops: parseStops(json['stops'] ?? json['Stops']),
      events: parseEvents(json['events'] ?? json['Events']),
      summary: summaryRaw is Map
          ? TripReplaySummary.fromJson(Map<String, dynamic>.from(summaryRaw))
          : null,
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

class SendDeviceCommandResult {
  const SendDeviceCommandResult({
    required this.id,
    required this.message,
  });

  final int id;
  final String message;
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

  bool get isPendingApproval => status == 'PendingApproval';
  bool get isCancellable => status == 'pending' || status == 'PendingApproval';

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

class GpsCommandResponseItem {
  const GpsCommandResponseItem({
    required this.id,
    required this.source,
    required this.receivedAt,
    this.responseCode,
    this.responseText,
  });

  final int id;
  final String source;
  final DateTime receivedAt;
  final String? responseCode;
  final String? responseText;

  factory GpsCommandResponseItem.fromJson(Map<String, dynamic> json) {
    return GpsCommandResponseItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      source: json['source'] as String? ?? json['Source'] as String? ?? '',
      responseCode:
          json['responseCode'] as String? ?? json['ResponseCode'] as String?,
      responseText:
          json['responseText'] as String? ?? json['ResponseText'] as String?,
      receivedAt: _parseDate(json['receivedAt'] ?? json['ReceivedAt']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
    );
  }
}

class GpsDeviceCommandDetail {
  const GpsDeviceCommandDetail({
    required this.command,
    required this.responses,
    this.attributesJson,
  });

  final GpsDeviceCommandItem command;
  final List<GpsCommandResponseItem> responses;
  final String? attributesJson;

  String get responseBodyText {
    final parts = responses
        .map((r) => (r.responseText ?? '').trim())
        .where((t) => t.isNotEmpty);
    return parts.join('\n\n');
  }

  factory GpsDeviceCommandDetail.fromJson(Map<String, dynamic> json) {
    final commandRaw = json['command'] ?? json['Command'];
    final responsesRaw = json['responses'] ?? json['Responses'];
    final commandMap = commandRaw is Map
        ? Map<String, dynamic>.from(commandRaw)
        : <String, dynamic>{};
    final responses = responsesRaw is List
        ? responsesRaw
            .whereType<Map>()
            .map(
              (e) => GpsCommandResponseItem.fromJson(
                Map<String, dynamic>.from(e),
              ),
            )
            .toList()
        : const <GpsCommandResponseItem>[];
    return GpsDeviceCommandDetail(
      command: GpsDeviceCommandItem.fromJson(commandMap),
      attributesJson: json['attributesJson'] as String? ??
          json['AttributesJson'] as String?,
      responses: responses,
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
    this.category,
    this.description,
    this.assignedVehicleCount = 0,
    this.alertOnEnter = false,
    this.alertOnExit = false,
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
  final String? category;
  final String? description;
  final int assignedVehicleCount;

  /// Fleet-wide Entry/Exit alert rule flags for this zone.
  final bool alertOnEnter;
  final bool alertOnExit;

  /// ERP geofences tagged as parking zones (category or name).
  bool get isParkingZone {
    final c = (category ?? '').trim().toLowerCase();
    if (c.contains('parking')) return true;
    return name.toLowerCase().contains('parking');
  }

  bool get isCircle {
    final t = areaType.toLowerCase();
    if (t.contains('polygon') || t.contains('rectangle')) return false;
    if (t.contains('circle')) return true;
    return geoJson == null || geoJson!.trim().isEmpty;
  }

  bool get isPolygon {
    final t = areaType.toLowerCase();
    return t.contains('polygon') ||
        t.contains('rectangle') ||
        (geoJson != null && geoJson!.trim().isNotEmpty && !isCircle);
  }

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
      category: json['category'] as String? ?? json['Category'] as String?,
      description:
          json['description'] as String? ?? json['Description'] as String?,
      assignedVehicleCount: json['assignedVehicleCount'] as int? ??
          json['AssignedVehicleCount'] as int? ??
          0,
      alertOnEnter: json['alertOnEnter'] as bool? ??
          json['AlertOnEnter'] as bool? ??
          false,
      alertOnExit:
          json['alertOnExit'] as bool? ?? json['AlertOnExit'] as bool? ?? false,
    );
  }
}
