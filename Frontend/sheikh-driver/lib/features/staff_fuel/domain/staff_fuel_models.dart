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
    this.vehicleName,
    this.vehicleRegistration,
    this.driverName,
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
  final String? vehicleName;
  final String? vehicleRegistration;
  final String? driverName;

  /// Registration / name label used across fleet & maintenance screens.
  String get vehicleLabel {
    final name = vehicleName?.trim();
    final reg = vehicleRegistration?.trim();
    if (name != null && name.isNotEmpty && reg != null && reg.isNotEmpty) {
      return '$name · $reg';
    }
    if (name != null && name.isNotEmpty) return name;
    if (reg != null && reg.isNotEmpty) return reg;
    return 'Vehicle #$vehicleId';
  }

  String get driverLabel {
    final name = driverName?.trim();
    if (name != null && name.isNotEmpty) return name;
    if (driverId != null) return 'Driver #$driverId';
    return '—';
  }

  factory StaffFuelLog.fromJson(Map<String, dynamic> json) {
    return StaffFuelLog(
      id: _asInt(json['id'] ?? json['Id']),
      vehicleId: _asInt(json['vehicleId'] ?? json['VehicleId']),
      driverId: _asIntOrNull(json['driverId'] ?? json['DriverId']),
      liters: _asDouble(json['liters'] ?? json['Liters']),
      pricePerLiter: _asDouble(json['pricePerLiter'] ?? json['PricePerLiter']),
      totalCost: _asDouble(json['totalCost'] ?? json['TotalCost']),
      odometerReading:
          _asDouble(json['odometerReading'] ?? json['OdometerReading']),
      fuelType: (json['fuelType'] ?? json['FuelType'] ?? '').toString(),
      fuelDate: DateTime.tryParse(
            (json['fuelDate'] ?? json['FuelDate'] ?? '').toString(),
          ) ??
          DateTime.now(),
      station: _asStringOrNull(json['station'] ?? json['Station']),
      createdAt: DateTime.tryParse(
            (json['createdAt'] ?? json['CreatedAt'] ?? '').toString(),
          ) ??
          DateTime.now(),
      receiptUrl: _asStringOrNull(json['receiptUrl'] ?? json['ReceiptUrl']),
      vehicleName: _asStringOrNull(json['vehicleName'] ?? json['VehicleName']),
      vehicleRegistration: _asStringOrNull(
        json['vehicleRegistration'] ?? json['VehicleRegistration'],
      ),
      driverName: _asStringOrNull(json['driverName'] ?? json['DriverName']),
    );
  }

  static int _asInt(Object? value) {
    if (value == null) return 0;
    if (value is int) return value;
    if (value is num) return value.toInt();
    return int.tryParse(value.toString()) ?? 0;
  }

  static int? _asIntOrNull(Object? value) {
    if (value == null) return null;
    if (value is int) return value;
    if (value is num) return value.toInt();
    return int.tryParse(value.toString());
  }

  static double _asDouble(Object? value) {
    if (value == null) return 0;
    if (value is num) return value.toDouble();
    return double.tryParse(value.toString()) ?? 0;
  }

  static String? _asStringOrNull(Object? value) {
    if (value == null) return null;
    final s = value.toString().trim();
    return s.isEmpty ? null : s;
  }
}
