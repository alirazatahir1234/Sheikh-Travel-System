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
    this.vehicleName,
    this.vehiclePlate,
    this.receiptUrl,
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
  final String? vehicleName;
  final String? vehiclePlate;
  final String? receiptUrl;

  factory FuelLog.fromJson(Map<String, dynamic> json) => FuelLog(
        id: json['id'] as int? ?? 0,
        vehicleId: json['vehicleId'] as int? ?? 0,
        liters: (json['liters'] as num?)?.toDouble() ?? 0,
        pricePerLiter: (json['pricePerLiter'] as num?)?.toDouble() ?? 0,
        totalCost: (json['totalCost'] as num?)?.toDouble() ?? 0,
        fuelDate:
            DateTime.tryParse(json['fuelDate']?.toString() ?? '') ?? DateTime.now(),
        odometerReading: (json['odometerReading'] as num?)?.toDouble(),
        station: json['station'] as String?,
        fuelType: (json['fuelTypeName'] ?? json['fuelType'])?.toString(),
        vehicleName: json['vehicleName'] as String?,
        vehiclePlate: json['vehiclePlate'] as String?,
        receiptUrl: json['receiptUrl'] as String?,
      );
}

class FuelOcrSuggestion {
  const FuelOcrSuggestion({
    this.liters,
    this.pricePerLiter,
    this.totalCost,
    this.station,
    this.fuelType,
    this.confidence = 0,
  });

  final double? liters;
  final double? pricePerLiter;
  final double? totalCost;
  final String? station;
  final String? fuelType;
  final int confidence;

  factory FuelOcrSuggestion.fromJson(Map<String, dynamic> json) =>
      FuelOcrSuggestion(
        liters: (json['liters'] as num?)?.toDouble(),
        pricePerLiter: (json['pricePerLiter'] as num?)?.toDouble(),
        totalCost: (json['totalCost'] as num?)?.toDouble(),
        station: json['station'] as String?,
        fuelType: json['fuelType'] as String?,
        confidence: (json['confidence'] as num?)?.toInt() ?? 0,
      );

  bool get hasAny =>
      liters != null ||
      pricePerLiter != null ||
      totalCost != null ||
      (station != null && station!.isNotEmpty) ||
      (fuelType != null && fuelType!.isNotEmpty);
}
