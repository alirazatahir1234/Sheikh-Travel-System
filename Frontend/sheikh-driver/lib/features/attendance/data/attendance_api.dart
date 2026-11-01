import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/errors/app_exception.dart';
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
    try {
      await _ref.read(offlineSyncProvider).runOrQueue(
        online: () async {
          final res = await _dio.post<Map<String, dynamic>>(
            ApiEndpoints.attendanceCheckIn,
            data: {
              'latitude': lat,
              'longitude': lng,
            },
          );
          ApiResponseParser.ensureSuccess(res.data);
        },
        type: OfflineOpType.attendanceCheckIn,
        payload: {'latitude': lat, 'longitude': lng},
      );
    } on OfflineQueuedException {
      rethrow;
    } catch (e) {
      throw toAppException(e);
    }
  }

  Future<void> checkOut({double? lat, double? lng}) async {
    try {
      await _ref.read(offlineSyncProvider).runOrQueue(
        online: () async {
          final res = await _dio.post<Map<String, dynamic>>(
            ApiEndpoints.attendanceCheckOut,
            data: {
              'latitude': lat,
              'longitude': lng,
            },
          );
          ApiResponseParser.ensureSuccess(res.data);
        },
        type: OfflineOpType.attendanceCheckOut,
        payload: {'latitude': lat, 'longitude': lng},
      );
    } on OfflineQueuedException {
      rethrow;
    } catch (e) {
      throw toAppException(e);
    }
  }

  Future<List<AttendanceRecord>> getHistory({
    DateTime? from,
    DateTime? to,
    int page = 1,
    int pageSize = 30,
  }) async {
    try {
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
      // Fail closed on success:false (e.g. "Driver identity required").
      ApiResponseParser.ensureSuccess(body);
      try {
        final list = ApiResponseParser.dataList(body);
        return list.map(AttendanceRecord.fromJson).toList();
      } on AppException {
        rethrow;
      } catch (_) {
        throw const UnknownException(
          'Could not load attendance history. Please try again.',
        );
      }
    } catch (e) {
      throw toAppException(e);
    }
  }

  Future<void> requestCorrection({
    required DateTime attendanceDate,
    required String notes,
    String? requestedStatus,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.attendanceCorrectionRequest,
        data: {
          'attendanceDate': attendanceDate.toIso8601String(),
          'notes': notes,
          if (requestedStatus != null) 'requestedStatus': requestedStatus,
        },
      );
      ApiResponseParser.ensureSuccess(res.data);
    } catch (e) {
      throw toAppException(e);
    }
  }

  Future<List<TeamAttendanceRow>> getTeamAttendance({
    DateTime? date,
    String? q,
    int? branchId,
  }) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.teamAttendance,
        queryParameters: {
          if (date != null) 'date': date.toIso8601String(),
          if (q != null && q.trim().isNotEmpty) 'q': q.trim(),
          if (branchId != null) 'branchId': branchId,
        },
      );
      ApiResponseParser.ensureSuccess(res.data);
      try {
        return ApiResponseParser.dataList(res.data)
            .map(TeamAttendanceRow.fromJson)
            .toList();
      } on AppException {
        rethrow;
      } catch (_) {
        throw const UnknownException(
          'Could not load team attendance. Please try again.',
        );
      }
    } catch (e) {
      throw toAppException(e);
    }
  }

  Future<void> createManagerCorrection({
    required int driverId,
    required DateTime attendanceDate,
    required String status,
    DateTime? checkInAt,
    DateTime? checkOutAt,
    String? notes,
  }) async {
    try {
      final res = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.driverAttendance(driverId),
        data: {
          'attendanceDate': attendanceDate.toIso8601String(),
          'status': status,
          if (checkInAt != null) 'checkInAt': checkInAt.toIso8601String(),
          if (checkOutAt != null) 'checkOutAt': checkOutAt.toIso8601String(),
          if (notes != null) 'notes': notes,
        },
      );
      ApiResponseParser.ensureSuccess(res.data);
    } catch (e) {
      throw toAppException(e);
    }
  }
}
