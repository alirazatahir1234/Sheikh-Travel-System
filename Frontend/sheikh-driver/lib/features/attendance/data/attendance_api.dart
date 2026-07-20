import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/offline/offline_models.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../domain/attendance_model.dart';

final attendanceApiProvider =
    Provider<AttendanceApi>((ref) => AttendanceApi(ref.read(dioProvider), ref));

class AttendanceApi {
  AttendanceApi(this._dio, this._ref);
  final Dio _dio;
  final Ref _ref;

  Future<void> checkIn({double? lat, double? lng}) async {
    await _ref.read(offlineSyncProvider).runOrQueue(
          online: () => _dio.post(ApiEndpoints.attendanceCheckIn, data: {
            'latitude': lat,
            'longitude': lng,
          }),
          type: OfflineOpType.attendanceCheckIn,
          payload: {'latitude': lat, 'longitude': lng},
        );
  }

  Future<void> checkOut({double? lat, double? lng}) async {
    await _ref.read(offlineSyncProvider).runOrQueue(
          online: () => _dio.post(ApiEndpoints.attendanceCheckOut, data: {
            'latitude': lat,
            'longitude': lng,
          }),
          type: OfflineOpType.attendanceCheckOut,
          payload: {'latitude': lat, 'longitude': lng},
        );
  }

  Future<List<AttendanceRecord>> getHistory({
    DateTime? from,
    DateTime? to,
    int page = 1,
    int pageSize = 30,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.attendanceHistory,
      queryParameters: {
        if (from != null) 'from': from.toIso8601String(),
        if (to != null) 'to': to.toIso8601String(),
        'page': page,
        'pageSize': pageSize,
      },
    );
    final body = res.data;
    final list = (body?['data'] as List?) ?? [];
    return list.cast<Map<String, dynamic>>().map(AttendanceRecord.fromJson).toList();
  }
}
