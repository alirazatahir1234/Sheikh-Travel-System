import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../api/api_endpoints.dart';
import '../api/dio_client.dart';
import 'device_integrity_service.dart';

final deviceRegistrationProvider = Provider<DeviceRegistrationService>(
  (ref) => DeviceRegistrationService(ref.read(dioProvider)),
);

class DeviceRegistrationService {
  DeviceRegistrationService(this._dio);
  final Dio _dio;

  Future<void> registerCurrentDevice() async {
    final report = await DeviceIntegrityService.instance.evaluate();
    try {
      await _dio.post(ApiEndpoints.deviceRegister, data: {
        ...report.toRegistrationPayload(),
        'fingerprintHash': DeviceIntegrityService.fingerprintHash(report),
      });
      debugPrint('[Security] Device registered (${report.deviceId})');
    } catch (e) {
      debugPrint('[Security] Device registration failed: $e');
    }
  }
}
