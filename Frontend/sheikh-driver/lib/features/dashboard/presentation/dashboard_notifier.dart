import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/dashboard_api.dart';
import '../domain/dashboard_models.dart';

final dashboardProvider =
    AsyncNotifierProvider<DashboardNotifier, DashboardSummary>(DashboardNotifier.new);

class DashboardNotifier extends AsyncNotifier<DashboardSummary> {
  @override
  Future<DashboardSummary> build() => _fetch();

  Future<DashboardSummary> _fetch() =>
      ref.read(dashboardApiProvider).getSummary();

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_fetch);
  }
}
