class FuelLog {
  const FuelLog({
    required this.id,
    required this.vehicleId,
    required this.liters,
    required this.pricePerLiter,
    required this.totalCost,
    required this.fuelDate,
    this.odometerReading,
    this.station,
    this.fuelType,
  });

  final int id;
  final int vehicleId;
  final double liters;
  final double pricePerLiter;
  final double totalCost;
  final DateTime fuelDate;
  final double? odometerReading;
  final String? station;
  final String? fuelType;

  factory FuelLog.fromJson(Map<String, dynamic> json) => FuelLog(
        id: json['id'] as int? ?? 0,
        vehicleId: json['vehicleId'] as int? ?? 0,
        liters: (json['liters'] as num?)?.toDouble() ?? 0,
        pricePerLiter: (json['pricePerLiter'] as num?)?.toDouble() ?? 0,
        totalCost: (json['totalCost'] as num?)?.toDouble() ?? 0,
        fuelDate: DateTime.tryParse(json['fuelDate']?.toString() ?? '') ?? DateTime.now(),
        odometerReading: (json['odometerReading'] as num?)?.toDouble(),
        station: json['station'] as String?,
        fuelType: json['fuelType'] as String?,
      );
}
