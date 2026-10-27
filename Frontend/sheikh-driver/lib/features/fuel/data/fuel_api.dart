import 'dart:io';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/offline/offline_file_store.dart';
import '../../../core/offline/offline_models.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../domain/fuel_model.dart';

final fuelApiProvider =
    Provider<FuelApi>((ref) => FuelApi(ref.read(dioProvider), ref));

class FuelApi {
  FuelApi(this._dio, this._ref);
  final Dio _dio;
  final Ref _ref;

  Future<List<FuelLog>> getHistory({int page = 1}) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.fuelReceipts,
      queryParameters: {'page': page, 'pageSize': 30},
    );
    final list = (res.data?['data'] as List?) ?? [];
    return list.cast<Map<String, dynamic>>().map(FuelLog.fromJson).toList();
  }

  Future<void> submitReceipt({
    required int vehicleId,
    required double liters,
    required double pricePerLiter,
    required double odometerReading,
    required String station,
    required String fuelType,
    File? receipt,
  }) async {
    // Persist receipt early so offline queue does not depend on cache/temp paths.
    final durablePaths = receipt != null
        ? await OfflineFileStore.persistForOffline([receipt.path])
        : const <String>[];
    final receiptPath =
        durablePaths.isNotEmpty ? durablePaths.first : receipt?.path;
    final receiptFile =
        receiptPath != null ? File(receiptPath) : null;

    await _ref.read(offlineSyncProvider).runOrQueue(
          online: () async {
            final form = FormData.fromMap({
              'vehicleId': vehicleId,
              'liters': liters,
              'pricePerLiter': pricePerLiter,
              'odometerReading': odometerReading,
              'station': station,
              'fuelType': fuelType,
              'fuelDate': DateTime.now().toUtc().toIso8601String(),
            });
            if (receiptFile != null && await receiptFile.exists()) {
              form.files.add(MapEntry(
                'receipt',
                await MultipartFile.fromFile(
                  receiptFile.path,
                  filename: receiptFile.uri.pathSegments.last,
                ),
              ));
            }
            await _dio.post(ApiEndpoints.fuelReceipts, data: form);
          },
          type: OfflineOpType.fuelSubmit,
          payload: {
            'vehicleId': vehicleId,
            'liters': liters,
            'pricePerLiter': pricePerLiter,
            'odometerReading': odometerReading,
            'station': station,
            'fuelType': fuelType,
            'fuelDate': DateTime.now().toUtc().toIso8601String(),
            if (receiptPath != null) 'receiptPath': receiptPath,
          },
          filePaths: receiptPath != null ? [receiptPath] : const [],
        );
  }

  Future<String> submitPumpOrder({
    required int vehicleId,
    required double orderedLiters,
    String? stationName,
    double? latitude,
    double? longitude,
  }) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.fuelPumpOrder,
      data: {
        'vehicleId': vehicleId,
        'orderedLiters': orderedLiters,
        if (stationName != null && stationName.isNotEmpty)
          'stationName': stationName,
        if (latitude != null) 'latitude': latitude,
        if (longitude != null) 'longitude': longitude,
      },
    );
    final data = res.data?['data'] as Map<String, dynamic>? ?? {};
    return data['message'] as String? ?? 'Pump order logged';
  }

  Future<FuelOcrSuggestion> scanReceipt(File receipt) async {
    final form = FormData.fromMap({
      'receipt': await MultipartFile.fromFile(
        receipt.path,
        filename: receipt.uri.pathSegments.last,
      ),
    });
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.fuelReceiptsScan,
      data: form,
    );
    final data = res.data?['data'] as Map<String, dynamic>? ?? {};
    return FuelOcrSuggestion.fromJson(data);
  }
}
