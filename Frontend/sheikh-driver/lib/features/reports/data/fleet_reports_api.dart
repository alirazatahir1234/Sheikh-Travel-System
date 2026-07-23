import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/report_models.dart';

final fleetReportsApiProvider =
    Provider<FleetReportsApi>((ref) => FleetReportsApi(ref.read(dioProvider)));

class FleetReportsApi {
  FleetReportsApi(this._dio);
  final Dio _dio;

  Future<FleetReport> fetch({
    required String reportType,
    DateTime? from,
    DateTime? to,
    int? vehicleId,
    int? driverId,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.fleetReports,
      queryParameters: {
        'reportType': reportType,
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
        if (vehicleId != null) 'vehicleId': vehicleId,
        if (driverId != null) 'driverId': driverId,
      },
    );
    return FleetReport.fromJson(ApiResponseParser.dataMap(res.data));
  }
}
