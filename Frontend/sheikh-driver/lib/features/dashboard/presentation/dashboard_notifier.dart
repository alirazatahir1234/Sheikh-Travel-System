import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/dashboard_api.dart';
import '../../auth/data/auth_repository.dart';
import '../domain/dashboard_models.dart';
import 'dart:async';

final dashboardProvider =
    AsyncNotifierProvider<DashboardNotifier, RoleDashboardData>(
        DashboardNotifier.new);

class DashboardNotifier extends AsyncNotifier<RoleDashboardData> {
  @override
  Future<RoleDashboardData> build() {
    final session = ref.read(fleetSessionProvider);
    if (session?.isGpsOperator == true) {
      final timer = Timer.periodic(const Duration(seconds: 25), (_) {
        silentRefresh();
      });
      ref.onDispose(timer.cancel);
    }
    return _fetch();
  }

  Future<void> silentRefresh() async {
    if (!ref.read(authRepositoryProvider).isLoggedIn) return;
    try {
      final next = await _fetch();
      state = AsyncData(next);
    } catch (_) {}
  }

  Future<RoleDashboardData> _fetch() =>
      ref.read(dashboardApiProvider).getRoleDashboard();

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_fetch);
  }

  Future<void> setStatus(String status) async {
    await ref.read(dashboardApiProvider).setStatus(status);
    await refresh();
  }
}
