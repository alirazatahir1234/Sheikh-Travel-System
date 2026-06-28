import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';

final fuelApiProvider = Provider<FuelApi>((ref) => FuelApi(ref.read(dioProvider)));

class FuelApi {
  FuelApi(this._dio);
  final Dio _dio;

  Future<void> submitReceipt({
    required int vehicleId,
    required double liters,
    required double pricePerLiter,
    required double totalCost,
    required double odometerReading,
    required String station,
    required String fuelType,
    int? driverId,
  }) async {
    await _dio.post(ApiEndpoints.fuelReceipts, data: {
      'vehicleId': vehicleId,
      'driverId': driverId,
      'liters': liters,
      'pricePerLiter': pricePerLiter,
      'totalCost': totalCost,
      'odometerReading': odometerReading,
      'station': station,
      'fuelType': fuelType,
      'fuelDate': DateTime.now().toIso8601String(),
    });
  }
}
