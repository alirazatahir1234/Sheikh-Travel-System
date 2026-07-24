import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/maintenance_api.dart';
import '../domain/maintenance_models.dart';

enum MaintenanceTab { requests, workOrders }

class MaintenanceHubState {
  const MaintenanceHubState({
    this.kpis = MaintenanceKpis.empty,
    this.requests = const [],
    this.workOrders = const [],
    this.tab = MaintenanceTab.requests,
    this.statusFilter,
    this.search = '',
  });

  final MaintenanceKpis kpis;
  final List<MaintenanceRequestItem> requests;
  final List<WorkOrderItem> workOrders;
  final MaintenanceTab tab;
  final String? statusFilter;
  final String search;

  MaintenanceHubState copyWith({
    MaintenanceKpis? kpis,
    List<MaintenanceRequestItem>? requests,
    List<WorkOrderItem>? workOrders,
    MaintenanceTab? tab,
    String? statusFilter,
    bool clearStatus = false,
    String? search,
  }) {
    return MaintenanceHubState(
      kpis: kpis ?? this.kpis,
      requests: requests ?? this.requests,
      workOrders: workOrders ?? this.workOrders,
      tab: tab ?? this.tab,
      statusFilter: clearStatus ? null : (statusFilter ?? this.statusFilter),
      search: search ?? this.search,
    );
  }
}

final maintenanceHubProvider =
    AsyncNotifierProvider<MaintenanceHubNotifier, MaintenanceHubState>(
  MaintenanceHubNotifier.new,
);

class MaintenanceHubNotifier extends AsyncNotifier<MaintenanceHubState> {
  @override
  Future<MaintenanceHubState> build() => _load();

  Future<MaintenanceHubState> _load({
    String? status,
    bool clearStatus = false,
  }) async {
    final api = ref.read(maintenanceApiProvider);
    final prev = state.valueOrNull;
    final useStatus =
        clearStatus ? null : (status ?? prev?.statusFilter);

    MaintenanceKpis kpis = MaintenanceKpis.empty;
    List<MaintenanceRequestItem> requests = const [];
    List<WorkOrderItem> workOrders = const [];

    await Future.wait([
      () async {
        try {
          kpis = await api.dashboardKpis();
        } catch (_) {}
      }(),
      () async {
        try {
          requests = await api.listRequests(status: useStatus);
        } catch (_) {}
      }(),
      () async {
        try {
          workOrders = await api.listWorkOrders(status: useStatus);
        } catch (_) {}
      }(),
    ]);

    return MaintenanceHubState(
      kpis: kpis,
      requests: requests,
      workOrders: workOrders,
      tab: prev?.tab ?? MaintenanceTab.requests,
      statusFilter: useStatus,
      search: prev?.search ?? '',
    );
  }

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_load);
  }

  void setTab(MaintenanceTab tab) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(tab: tab));
  }

  void setSearch(String q) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(search: q));
  }

  Future<void> setStatusFilter(String? status) async {
    final cur = state.valueOrNull;
    final next = (status == null || cur?.statusFilter == status) ? null : status;
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => _load(status: next, clearStatus: next == null),
    );
  }
}

final maintenanceRequestProvider =
    FutureProvider.family<MaintenanceRequestItem, int>((ref, id) {
  return ref.read(maintenanceApiProvider).getRequest(id);
});

final workOrderProvider =
    FutureProvider.family<WorkOrderItem, int>((ref, id) {
  return ref.read(maintenanceApiProvider).getWorkOrder(id);
});
