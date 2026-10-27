class DriverEvStatus {
  const DriverEvStatus({
    required this.vehicleId,
    required this.vehicleName,
    this.batteryPercent,
    this.electricRangeKm,
    this.estimatedRangeKm,
    required this.rangeAlert,
    required this.nearbyStations,
  });

  final int vehicleId;
  final String vehicleName;
  final double? batteryPercent;
  final double? electricRangeKm;
  final double? estimatedRangeKm;
  final bool rangeAlert;
  final List<ChargingStationPin> nearbyStations;

  factory DriverEvStatus.fromJson(Map<String, dynamic> json) {
    final stations = (json['nearbyStations'] as List<dynamic>? ?? [])
        .whereType<Map<String, dynamic>>()
        .map(ChargingStationPin.fromJson)
        .toList();
    return DriverEvStatus(
      vehicleId: json['vehicleId'] as int? ?? 0,
      vehicleName: json['vehicleName'] as String? ?? '',
      batteryPercent: (json['batteryPercent'] as num?)?.toDouble(),
      electricRangeKm: (json['electricRangeKm'] as num?)?.toDouble(),
      estimatedRangeKm: (json['estimatedRangeKm'] as num?)?.toDouble(),
      rangeAlert: json['rangeAlert'] as bool? ?? false,
      nearbyStations: stations,
    );
  }
}

class ChargingStationPin {
  const ChargingStationPin({
    required this.id,
    required this.name,
    required this.latitude,
    required this.longitude,
    this.address,
    this.connectorType,
  });

  final int id;
  final String name;
  final double latitude;
  final double longitude;
  final String? address;
  final String? connectorType;

  factory ChargingStationPin.fromJson(Map<String, dynamic> json) {
    return ChargingStationPin(
      id: json['id'] as int? ?? 0,
      name: json['name'] as String? ?? '',
      latitude: (json['latitude'] as num?)?.toDouble() ?? 0,
      longitude: (json['longitude'] as num?)?.toDouble() ?? 0,
      address: json['address'] as String?,
      connectorType: json['connectorType'] as String?,
    );
  }
}

class VehicleDocExpiry {
  const VehicleDocExpiry({
    required this.documentType,
    this.expiryDate,
    required this.daysRemaining,
    required this.status,
  });

  final String documentType;
  final DateTime? expiryDate;
  final int daysRemaining;
  final String status;

  factory VehicleDocExpiry.fromJson(Map<String, dynamic> json) {
    final raw = json['expiryDate'] as String?;
    return VehicleDocExpiry(
      documentType: json['documentType'] as String? ?? '',
      expiryDate: raw != null ? DateTime.tryParse(raw) : null,
      daysRemaining: json['daysRemaining'] as int? ?? 0,
      status: json['status'] as String? ?? '',
    );
  }
}
