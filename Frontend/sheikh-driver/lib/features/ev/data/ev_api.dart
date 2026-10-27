import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/ev_models.dart';

final evApiProvider = Provider<EvApi>((ref) => EvApi(ref.read(dioProvider)));

class EvApi {
  EvApi(this._dio);
  final Dio _dio;

  Future<DriverEvStatus?> fetchStatus() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.driverEvStatus);
      if (res.data == null) return null;
      final data = ApiResponseParser.dataMap(res.data);
      if (data.isEmpty) return null;
      return DriverEvStatus.fromJson(data);
    } on DioException catch (e) {
      if (e.response?.statusCode == 400) return null;
      rethrow;
    }
  }

  Future<List<VehicleDocExpiry>> fetchDocumentExpiries() async {
    final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.driverVehicleDocExpiries);
    return ApiResponseParser.dataList(res.data)
        .map(VehicleDocExpiry.fromJson)
        .toList();
  }
}
