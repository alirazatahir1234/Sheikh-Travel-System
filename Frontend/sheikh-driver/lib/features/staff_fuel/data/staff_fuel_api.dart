import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/staff_fuel_models.dart';

final staffFuelApiProvider =
    Provider<StaffFuelApi>((ref) => StaffFuelApi(ref.read(dioProvider)));

class StaffFuelApi {
  StaffFuelApi(this._dio);
  final Dio _dio;

  Future<List<StaffFuelLog>> list({int page = 1, int pageSize = 100}) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.fuelLogs,
      queryParameters: {'page': page, 'pageSize': pageSize},
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(StaffFuelLog.fromJson)
        .toList();
  }

  Future<StaffFuelLog> getById(int id) async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.fuelLogById(id));
    return StaffFuelLog.fromJson(ApiResponseParser.dataMap(res.data));
  }
}
