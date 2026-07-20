import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../domain/earnings_models.dart';

final earningsApiProvider =
    Provider<EarningsApi>((ref) => EarningsApi(ref.read(dioProvider)));

class EarningsApi {
  EarningsApi(this._dio);
  final Dio _dio;

  Future<EarningsSummary> getEarnings({DateTime? from, DateTime? to}) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.earnings,
      queryParameters: {
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
      },
    );
    final data = res.data?['data'] as Map<String, dynamic>? ?? {};
    return EarningsSummary.fromJson(data);
  }
}
