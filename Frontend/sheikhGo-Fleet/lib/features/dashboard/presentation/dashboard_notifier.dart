import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/dashboard_api.dart';
import '../domain/dashboard_models.dart';

final dashboardProvider =
    AsyncNotifierProvider<DashboardNotifier, RoleDashboardData>(
        DashboardNotifier.new);

class DashboardNotifier extends AsyncNotifier<RoleDashboardData> {
  @override
  Future<RoleDashboardData> build() => _fetch();

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
