import 'dart:io';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
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
            if (receipt != null) {
              form.files.add(MapEntry(
                'receipt',
                await MultipartFile.fromFile(
                  receipt.path,
                  filename: receipt.uri.pathSegments.last,
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
          },
          filePaths: receipt != null ? [receipt.path] : const [],
        );
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
