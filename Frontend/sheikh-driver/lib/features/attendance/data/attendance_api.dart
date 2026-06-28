import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../domain/attendance_model.dart';

final attendanceApiProvider = Provider<AttendanceApi>((ref) => AttendanceApi(ref.read(dioProvider)));

class AttendanceApi {
  AttendanceApi(this._dio);
  final Dio _dio;

  Future<void> checkIn({double? lat, double? lng}) async {
    await _dio.post(ApiEndpoints.attendanceCheckIn, data: {
      'latitude': lat,
      'longitude': lng,
    });
  }

  Future<void> checkOut({double? lat, double? lng}) async {
    await _dio.post(ApiEndpoints.attendanceCheckOut, data: {
      'latitude': lat,
      'longitude': lng,
    });
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
