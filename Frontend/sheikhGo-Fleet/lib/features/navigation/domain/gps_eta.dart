class GpsEta {
  const GpsEta({
    required this.bookingId,
    required this.vehicleId,
    this.vehicleName,
    required this.distanceKm,
    this.etaMinutes,
    required this.driverLatitude,
    required this.driverLongitude,
    required this.pickupLatitude,
    required this.pickupLongitude,
  });

  final int bookingId;
  final int vehicleId;
  final String? vehicleName;
  final double distanceKm;
  final int? etaMinutes;
  final double driverLatitude;
  final double driverLongitude;
  final double pickupLatitude;
  final double pickupLongitude;

  factory GpsEta.fromJson(Map<String, dynamic> json) => GpsEta(
        bookingId: json['bookingId'] as int? ?? 0,
        vehicleId: json['vehicleId'] as int? ?? 0,
        vehicleName: json['vehicleName'] as String?,
        distanceKm: (json['distanceKm'] as num?)?.toDouble() ?? 0,
        etaMinutes: json['etaMinutes'] as int?,
        driverLatitude: (json['driverLatitude'] as num?)?.toDouble() ?? 0,
        driverLongitude: (json['driverLongitude'] as num?)?.toDouble() ?? 0,
        pickupLatitude: (json['pickupLatitude'] as num?)?.toDouble() ?? 0,
        pickupLongitude: (json['pickupLongitude'] as num?)?.toDouble() ?? 0,
      );
}
