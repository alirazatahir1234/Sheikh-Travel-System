import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/ops_trip_models.dart';

final opsTripsApiProvider =
    Provider<OpsTripsApi>((ref) => OpsTripsApi(ref.read(dioProvider)));

class OpsTripsApi {
  OpsTripsApi(this._dio);
  final Dio _dio;

  Future<List<OpsTripListItem>> list({
    String? status,
    String? search,
    bool todayOnly = false,
    int page = 1,
    int pageSize = 100,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.opsTrips,
      queryParameters: {
        'page': page,
        'pageSize': pageSize,
        if (status != null && status.isNotEmpty) 'status': status,
        if (search != null && search.isNotEmpty) 'search': search,
        if (todayOnly) 'todayOnly': true,
      },
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(OpsTripListItem.fromJson)
        .toList();
  }

  Future<List<OpsTripListItem>> live({bool todayOnly = true}) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.opsTripsLive,
      queryParameters: {'todayOnly': todayOnly},
    );
    // live may return list or paged
    final body = res.data;
    ApiResponseParser.ensureSuccess(body);
    final data = body?['data'];
    if (data is List) {
      return data
          .whereType<Map>()
          .map((e) => OpsTripListItem.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }
    return ApiResponseParser.pagedItems(body)
        .map(OpsTripListItem.fromJson)
        .toList();
  }

  Future<OpsTripsDashboard> dashboard() async {
    final res = await _dio
        .get<Map<String, dynamic>>(ApiEndpoints.opsTripsDashboard);
    return OpsTripsDashboard.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<OpsTripDetail> getById(int id) async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.opsTripById(id));
    return OpsTripDetail.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<void> updateStatus(int id, String status, {String? note}) async {
    final res = await _dio.put<Map<String, dynamic>>(
      ApiEndpoints.opsTripStatus(id),
      data: {
        'status': status,
        if (note != null) 'note': note,
      },
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<void> assignDriver(int id, int driverId) async {
    final res = await _dio.put<Map<String, dynamic>>(
      ApiEndpoints.opsTripAssignDriver(id),
      data: {'driverId': driverId},
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<void> assignVehicle(int id, int vehicleId) async {
    final res = await _dio.put<Map<String, dynamic>>(
      ApiEndpoints.opsTripAssignVehicle(id),
      data: {'vehicleId': vehicleId},
    );
    ApiResponseParser.ensureSuccess(res.data);
  }
}
