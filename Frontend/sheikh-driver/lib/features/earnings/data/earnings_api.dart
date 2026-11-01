import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../../auth/data/auth_repository.dart';
import '../../auth/domain/auth_models.dart';
import '../domain/earnings_models.dart';
import '../domain/owner_earnings_models.dart';

final earningsApiProvider = Provider<EarningsApi>((ref) => EarningsApi(
      ref.read(dioProvider),
      () => ref.read(fleetSessionProvider),
    ));

class EarningsApi {
  EarningsApi(this._dio, this._session);
  final Dio _dio;
  final FleetSession? Function() _session;

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

  Future<OwnerEarningsSummary> getOwnerEarnings({
    DateTime? from,
    DateTime? to,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.ownerEarnings,
      queryParameters: {
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
      },
    );
    final data = res.data?['data'] as Map<String, dynamic>? ?? {};
    return OwnerEarningsSummary.fromJson(data);
  }

  bool get isOwnerMode {
    final session = _session();
    if (session == null) return false;
    return !session.isDriverSession && session.canSeeOwnerEarnings;
  }
}
