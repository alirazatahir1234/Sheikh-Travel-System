import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/driver_models.dart';

final driversApiProvider =
    Provider<DriversApi>((ref) => DriversApi(ref.read(dioProvider)));

class DriversApi {
  DriversApi(this._dio);
  final Dio _dio;

  Future<List<DriverListItem>> list({
    String? q,
    String? status,
    int page = 1,
    int pageSize = 100,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.drivers,
      queryParameters: {
        'page': page,
        'pageSize': pageSize,
        if (q != null && q.isNotEmpty) 'q': q,
        if (status != null && status.isNotEmpty) 'status': status,
      },
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(DriverListItem.fromJson)
        .toList();
  }

  Future<DriverStats> stats() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.driversStats);
    return DriverStats.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<DriverDetail> getById(int id) async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.driverById(id));
    return DriverDetail.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<void> updateStatus(int id, String status) async {
    final res = await _dio.patch<Map<String, dynamic>>(
      ApiEndpoints.driverPatchStatus(id),
      data: {'status': status},
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<void> assignVehicle(int id, int vehicleId) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.driverAssignVehicle(id),
      data: {'vehicleId': vehicleId},
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<void> unassignVehicle(int id) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.driverUnassignVehicle(id),
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<DriverPerformanceSummary> performance(int id) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.driverPerformance(id),
    );
    return DriverPerformanceSummary.fromJson(
      ApiResponseParser.dataMap(res.data),
    );
  }

  Future<List<DriverViolation>> violations(int id) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.driverViolations(id),
      queryParameters: {'page': 1, 'pageSize': 30},
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(DriverViolation.fromJson)
        .toList();
  }

  Future<List<DriverAttendanceRow>> attendance(int id) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.driverAttendance(id),
    );
    return ApiResponseParser.dataList(res.data)
        .map(DriverAttendanceRow.fromJson)
        .toList();
  }

  Future<List<DriverDocumentItem>> documents(int id) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.driverDocuments(id),
    );
    return ApiResponseParser.dataList(res.data)
        .map(DriverDocumentItem.fromJson)
        .toList();
  }

  Future<List<DriverRankItem>> ranking({int limit = 5}) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.driverRanking,
    );
    final list = ApiResponseParser.dataList(res.data)
        .map(DriverRankItem.fromJson)
        .toList();
    if (list.length <= limit) return list;
    return list.take(limit).toList();
  }
}
