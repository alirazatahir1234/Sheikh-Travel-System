import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../domain/settlement_models.dart';

final settlementsApiProvider =
    Provider<SettlementsApi>((ref) => SettlementsApi(ref.read(dioProvider)));

class SettlementsApi {
  SettlementsApi(this._dio);
  final Dio _dio;

  Future<List<DriverSettlementItem>> getMySettlements({
    int page = 1,
    int pageSize = 50,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.settlements,
      queryParameters: {'page': page, 'pageSize': pageSize},
    );
    final data = res.data?['data'];
    final items = data is Map
        ? (data['items'] ?? data['Items']) as List? ?? const []
        : data is List
            ? data
            : const [];
    return items
        .whereType<Map>()
        .map((e) => DriverSettlementItem.fromJson(Map<String, dynamic>.from(e)))
        .toList();
  }
}
