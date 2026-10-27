import 'package:dio/dio.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../domain/driver_score_models.dart';

class DriverScoreApi {
  DriverScoreApi(this._dio);
  final Dio _dio;

  Future<DriverGpsScore> myScore({DateTime? from, DateTime? to}) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.driverAppPerformanceScore,
      queryParameters: {
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
      },
    );
    return DriverGpsScore.fromJson(ApiResponseParser.dataMap(res.data));
  }
}
