class StaffFuelLog {
  const StaffFuelLog({
    required this.id,
    required this.vehicleId,
    required this.liters,
    required this.pricePerLiter,
    required this.totalCost,
    required this.odometerReading,
    required this.fuelType,
    required this.fuelDate,
    required this.createdAt,
    this.driverId,
    this.station,
    this.receiptUrl,
  });

  final int id;
  final int vehicleId;
  final int? driverId;
  final double liters;
  final double pricePerLiter;
  final double totalCost;
  final double odometerReading;
  final String fuelType;
  final DateTime fuelDate;
  final String? station;
  final DateTime createdAt;
  final String? receiptUrl;

  factory StaffFuelLog.fromJson(Map<String, dynamic> json) {
    return StaffFuelLog(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      driverId: json['driverId'] as int? ?? json['DriverId'] as int?,
      liters: (json['liters'] as num? ?? json['Liters'] as num? ?? 0).toDouble(),
      pricePerLiter:
          (json['pricePerLiter'] as num? ?? json['PricePerLiter'] as num? ?? 0)
              .toDouble(),
      totalCost:
          (json['totalCost'] as num? ?? json['TotalCost'] as num? ?? 0).toDouble(),
      odometerReading: (json['odometerReading'] as num? ??
              json['OdometerReading'] as num? ??
              0)
          .toDouble(),
      fuelType: (json['fuelType'] ?? json['FuelType'] ?? '').toString(),
      fuelDate: DateTime.tryParse(
            (json['fuelDate'] ?? json['FuelDate'] ?? '').toString(),
          ) ??
          DateTime.now(),
      station: json['station'] as String? ?? json['Station'] as String?,
      createdAt: DateTime.tryParse(
            (json['createdAt'] ?? json['CreatedAt'] ?? '').toString(),
          ) ??
          DateTime.now(),
      receiptUrl: json['receiptUrl'] as String? ?? json['ReceiptUrl'] as String?,
    );
  }
}
