import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../../auth/data/auth_repository.dart';
import '../../auth/domain/auth_models.dart';
import '../domain/settlement_models.dart';

final settlementsApiProvider =
    Provider<SettlementsApi>((ref) => SettlementsApi(
          ref.read(dioProvider),
          () => ref.read(fleetSessionProvider),
        ));

class SettlementsApi {
  SettlementsApi(this._dio, this._session);
  final Dio _dio;
  final FleetSession? Function() _session;

  Future<List<DriverSettlementItem>> getMySettlements({
    int page = 1,
    int pageSize = 50,
  }) async {
    final session = _session();
    final useStaff = session != null &&
        !session.isDriverSession &&
        session.canSeeOwnerEarnings;

    final res = await _dio.get<Map<String, dynamic>>(
      useStaff ? ApiEndpoints.staffSettlements : ApiEndpoints.settlements,
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
