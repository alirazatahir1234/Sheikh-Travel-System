import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../domain/dashboard_models.dart';

final dashboardApiProvider = Provider<DashboardApi>(
  (ref) => DashboardApi(ref.read(dioProvider)),
);

class DashboardApi {
  DashboardApi(this._dio);
  final Dio _dio;

  Future<DashboardSummary> getSummary() async {
    final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.driverDashboard);
    final body = res.data;
    if (body == null) return DashboardSummary.empty();
    final data = (body['data'] as Map<String, dynamic>?) ?? body;
    return DashboardSummary.fromJson(data);
  }
}
