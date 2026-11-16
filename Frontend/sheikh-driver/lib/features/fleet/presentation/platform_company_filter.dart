import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:hive_flutter/hive_flutter.dart';
import '../../auth/data/auth_repository.dart';
import '../data/admin_cross_tenant_api.dart';

const platformCompanyFilterStorageKey = 'platform_company_filter_id';
const platformCompanyFilterHiveKey = 'platformCompanyFilterId';

/// Selected company for Super Admin cross-tenant views.
/// `null` = All Companies.
class PlatformCompanyFilterNotifier extends StateNotifier<int?> {
  PlatformCompanyFilterNotifier(this._ref) : super(null) {
    _restore();
  }

  final Ref _ref;
  final _secure = const FlutterSecureStorage();

  Future<void> _restore() async {
    try {
      final box = Hive.box('prefs');
      final raw = box.get(platformCompanyFilterHiveKey);
      if (raw is int && raw > 0) {
        state = raw;
        await _secure.write(
          key: platformCompanyFilterStorageKey,
          value: '$raw',
        );
        return;
      }
    } catch (_) {}
    try {
      final s = await _secure.read(key: platformCompanyFilterStorageKey);
      final id = int.tryParse(s ?? '');
      if (id != null && id > 0) state = id;
    } catch (_) {}
  }

  Future<void> select(int? companyId) async {
    final session = _ref.read(fleetSessionProvider);
    if (session == null || !session.canCrossTenantVehicles) return;

    state = companyId;
    try {
      final box = Hive.box('prefs');
      if (companyId == null) {
        await box.delete(platformCompanyFilterHiveKey);
        await _secure.delete(key: platformCompanyFilterStorageKey);
      } else {
        await box.put(platformCompanyFilterHiveKey, companyId);
        await _secure.write(
          key: platformCompanyFilterStorageKey,
          value: '$companyId',
        );
      }
    } catch (_) {}
  }
}

final platformCompanyFilterProvider =
    StateNotifierProvider<PlatformCompanyFilterNotifier, int?>((ref) {
  return PlatformCompanyFilterNotifier(ref);
});

final adminCompaniesProvider =
    FutureProvider.autoDispose<List<AdminCompanyOption>>((ref) async {
  final session = ref.watch(fleetSessionProvider);
  if (session == null || !session.canCrossTenantVehicles) {
    return const [];
  }
  return ref.read(adminCrossTenantApiProvider).getCompanies();
});
