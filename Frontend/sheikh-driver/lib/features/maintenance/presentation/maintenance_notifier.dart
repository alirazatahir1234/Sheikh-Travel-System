import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/errors/error_handler.dart';
import '../data/maintenance_api.dart';
import '../domain/maintenance_models.dart';

enum MaintenanceTab { dashboard, requests, workOrders, schedules }

class MaintenanceHubState {
  const MaintenanceHubState({
    this.dashboard = MaintenanceDashboard.empty,
    this.requestStats = MaintenanceRequestStats.empty,
    this.workOrderStats = WorkOrderStats.empty,
    this.requests = const [],
    this.requestsTotal = 0,
    this.requestsPage = 1,
    this.workOrders = const [],
    this.workOrdersTotal = 0,
    this.workOrdersPage = 1,
    this.schedules = const [],
    this.tab = MaintenanceTab.dashboard,
    this.requestStatusFilter,
    this.workOrderStatusFilter,
    this.scheduleStatusFilter,
    this.search = '',
    this.dashboardError,
    this.requestsError,
    this.workOrdersError,
    this.schedulesError,
    this.loadingMoreRequests = false,
    this.loadingMoreWorkOrders = false,
  });

  final MaintenanceDashboard dashboard;
  final MaintenanceRequestStats requestStats;
  final WorkOrderStats workOrderStats;
  final List<MaintenanceRequestItem> requests;
  final int requestsTotal;
  final int requestsPage;
  final List<WorkOrderItem> workOrders;
  final int workOrdersTotal;
  final int workOrdersPage;
  final List<MaintenanceScheduleItem> schedules;
  final MaintenanceTab tab;
  final String? requestStatusFilter;
  final String? workOrderStatusFilter;
  final String? scheduleStatusFilter;
  final String search;
  final String? dashboardError;
  final String? requestsError;
  final String? workOrdersError;
  final String? schedulesError;
  final bool loadingMoreRequests;
  final bool loadingMoreWorkOrders;

  MaintenanceKpis get kpis => dashboard.kpis;

  bool get hasMoreRequests => requests.length < requestsTotal;
  bool get hasMoreWorkOrders => workOrders.length < workOrdersTotal;

  MaintenanceHubState copyWith({
    MaintenanceDashboard? dashboard,
    MaintenanceRequestStats? requestStats,
    WorkOrderStats? workOrderStats,
    List<MaintenanceRequestItem>? requests,
    int? requestsTotal,
    int? requestsPage,
    List<WorkOrderItem>? workOrders,
    int? workOrdersTotal,
    int? workOrdersPage,
    List<MaintenanceScheduleItem>? schedules,
    MaintenanceTab? tab,
    String? requestStatusFilter,
    bool clearRequestStatus = false,
    String? workOrderStatusFilter,
    bool clearWorkOrderStatus = false,
    String? scheduleStatusFilter,
    bool clearScheduleStatus = false,
    String? search,
    String? dashboardError,
    bool clearDashboardError = false,
    String? requestsError,
    bool clearRequestsError = false,
    String? workOrdersError,
    bool clearWorkOrdersError = false,
    String? schedulesError,
    bool clearSchedulesError = false,
    bool? loadingMoreRequests,
    bool? loadingMoreWorkOrders,
  }) {
    return MaintenanceHubState(
      dashboard: dashboard ?? this.dashboard,
      requestStats: requestStats ?? this.requestStats,
      workOrderStats: workOrderStats ?? this.workOrderStats,
      requests: requests ?? this.requests,
      requestsTotal: requestsTotal ?? this.requestsTotal,
      requestsPage: requestsPage ?? this.requestsPage,
      workOrders: workOrders ?? this.workOrders,
      workOrdersTotal: workOrdersTotal ?? this.workOrdersTotal,
      workOrdersPage: workOrdersPage ?? this.workOrdersPage,
      schedules: schedules ?? this.schedules,
      tab: tab ?? this.tab,
      requestStatusFilter: clearRequestStatus
          ? null
          : (requestStatusFilter ?? this.requestStatusFilter),
      workOrderStatusFilter: clearWorkOrderStatus
          ? null
          : (workOrderStatusFilter ?? this.workOrderStatusFilter),
      scheduleStatusFilter: clearScheduleStatus
          ? null
          : (scheduleStatusFilter ?? this.scheduleStatusFilter),
      search: search ?? this.search,
      dashboardError:
          clearDashboardError ? null : (dashboardError ?? this.dashboardError),
      requestsError:
          clearRequestsError ? null : (requestsError ?? this.requestsError),
      workOrdersError: clearWorkOrdersError
          ? null
          : (workOrdersError ?? this.workOrdersError),
      schedulesError:
          clearSchedulesError ? null : (schedulesError ?? this.schedulesError),
      loadingMoreRequests: loadingMoreRequests ?? this.loadingMoreRequests,
      loadingMoreWorkOrders:
          loadingMoreWorkOrders ?? this.loadingMoreWorkOrders,
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

  Future<void> _softReload(Future<MaintenanceHubState> Function() loader) async {
    final previous = state.valueOrNull;
    if (previous != null) {
      state = const AsyncLoading<MaintenanceHubState>().copyWithPrevious(
        AsyncData(previous),
      );
    } else {
      state = const AsyncLoading();
    }
    state = await AsyncValue.guard(loader);
  }

  Future<MaintenanceHubState> _load({
    String? requestStatus,
    bool clearRequestStatus = false,
    String? workOrderStatus,
    bool clearWorkOrderStatus = false,
    String? scheduleStatus,
    bool clearScheduleStatus = false,
    String? search,
  }) async {
    final api = ref.read(maintenanceApiProvider);
    final prev = state.valueOrNull;
    final useRequestStatus = clearRequestStatus
        ? null
        : (requestStatus ?? prev?.requestStatusFilter);
    final useWorkOrderStatus = clearWorkOrderStatus
        ? null
        : (workOrderStatus ?? prev?.workOrderStatusFilter);
    final useScheduleStatus = clearScheduleStatus
        ? null
        : (scheduleStatus ?? prev?.scheduleStatusFilter);
    final useSearch = search ?? prev?.search ?? '';

    MaintenanceDashboard dashboard = prev?.dashboard ?? MaintenanceDashboard.empty;
    MaintenanceRequestStats requestStats =
        prev?.requestStats ?? MaintenanceRequestStats.empty;
    WorkOrderStats workOrderStats =
        prev?.workOrderStats ?? WorkOrderStats.empty;
    List<MaintenanceRequestItem> requests = prev?.requests ?? const [];
    int requestsTotal = prev?.requestsTotal ?? 0;
    List<WorkOrderItem> workOrders = prev?.workOrders ?? const [];
    int workOrdersTotal = prev?.workOrdersTotal ?? 0;
    List<MaintenanceScheduleItem> schedules = prev?.schedules ?? const [];

    String? dashboardError;
    String? requestsError;
    String? workOrdersError;
    String? schedulesError;

    await Future.wait([
      () async {
        try {
          dashboard = await api.getDashboard();
        } catch (e) {
          dashboardError = ErrorHandler.message(e);
        }
      }(),
      () async {
        try {
          requestStats = await api.requestStats();
        } catch (_) {
          // Non-fatal; KPIs still come from dashboard.
        }
      }(),
      () async {
        try {
          workOrderStats = await api.workOrderStats();
        } catch (_) {
          // Non-fatal.
        }
      }(),
      () async {
        try {
          final page = await api.listRequests(
            status: useRequestStatus,
            search: useSearch.isEmpty ? null : useSearch,
          );
          requests = page.items;
          requestsTotal = page.totalCount;
        } catch (e) {
          requestsError = ErrorHandler.message(e);
          if (prev == null) {
            requests = const [];
            requestsTotal = 0;
          }
        }
      }(),
      () async {
        try {
          final page = await api.listWorkOrders(
            status: useWorkOrderStatus,
            search: useSearch.isEmpty ? null : useSearch,
          );
          workOrders = page.items;
          workOrdersTotal = page.totalCount;
        } catch (e) {
          workOrdersError = ErrorHandler.message(e);
          if (prev == null) {
            workOrders = const [];
            workOrdersTotal = 0;
          }
        }
      }(),
      () async {
        try {
          schedules = await api.listSchedules(
            status: useScheduleStatus,
            search: useSearch.isEmpty ? null : useSearch,
          );
        } catch (e) {
          schedulesError = ErrorHandler.message(e);
          if (prev == null) schedules = const [];
        }
      }(),
    ]);

    final allFailed = dashboardError != null &&
        requestsError != null &&
        workOrdersError != null &&
        schedulesError != null;
    if (allFailed && prev == null) {
      throw Exception(requestsError ?? dashboardError ?? 'Failed to load');
    }

    return MaintenanceHubState(
      dashboard: dashboard,
      requestStats: requestStats,
      workOrderStats: workOrderStats,
      requests: requests,
      requestsTotal: requestsTotal,
      requestsPage: 1,
      workOrders: workOrders,
      workOrdersTotal: workOrdersTotal,
      workOrdersPage: 1,
      schedules: schedules,
      tab: prev?.tab ?? MaintenanceTab.dashboard,
      requestStatusFilter: useRequestStatus,
      workOrderStatusFilter: useWorkOrderStatus,
      scheduleStatusFilter: useScheduleStatus,
      search: useSearch,
      dashboardError: dashboardError,
      requestsError: requestsError,
      workOrdersError: workOrdersError,
      schedulesError: schedulesError,
    );
  }

  Future<void> refresh() async {
    final cur = state.valueOrNull;
    await _softReload(
      () => _load(
        requestStatus: cur?.requestStatusFilter,
        workOrderStatus: cur?.workOrderStatusFilter,
        scheduleStatus: cur?.scheduleStatusFilter,
        search: cur?.search,
      ),
    );
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

  Future<void> applySearch() async {
    final cur = state.valueOrNull;
    if (cur == null) return;
    await _softReload(() => _load(search: cur.search));
  }

  Future<void> setRequestStatusFilter(String? status) async {
    final cur = state.valueOrNull;
    final next =
        (status == null || cur?.requestStatusFilter == status) ? null : status;
    await _softReload(
      () => _load(
        requestStatus: next,
        clearRequestStatus: next == null,
      ),
    );
  }

  Future<void> setWorkOrderStatusFilter(String? status) async {
    final cur = state.valueOrNull;
    final next =
        (status == null || cur?.workOrderStatusFilter == status) ? null : status;
    await _softReload(
      () => _load(
        workOrderStatus: next,
        clearWorkOrderStatus: next == null,
      ),
    );
  }

  Future<void> setScheduleStatusFilter(String? status) async {
    final cur = state.valueOrNull;
    final next =
        (status == null || cur?.scheduleStatusFilter == status) ? null : status;
    await _softReload(
      () => _load(
        scheduleStatus: next,
        clearScheduleStatus: next == null,
      ),
    );
  }

  Future<void> loadMoreRequests() async {
    final cur = state.valueOrNull;
    if (cur == null || !cur.hasMoreRequests || cur.loadingMoreRequests) return;
    state = AsyncData(cur.copyWith(loadingMoreRequests: true));
    try {
      final nextPage = cur.requestsPage + 1;
      final page = await ref.read(maintenanceApiProvider).listRequests(
            status: cur.requestStatusFilter,
            search: cur.search.isEmpty ? null : cur.search,
            page: nextPage,
          );
      final merged = [...cur.requests, ...page.items];
      state = AsyncData(
        cur.copyWith(
          requests: merged,
          requestsTotal: page.totalCount,
          requestsPage: nextPage,
          loadingMoreRequests: false,
          clearRequestsError: true,
        ),
      );
    } catch (e) {
      state = AsyncData(
        cur.copyWith(
          loadingMoreRequests: false,
          requestsError: ErrorHandler.message(e),
        ),
      );
    }
  }

  Future<void> loadMoreWorkOrders() async {
    final cur = state.valueOrNull;
    if (cur == null || !cur.hasMoreWorkOrders || cur.loadingMoreWorkOrders) {
      return;
    }
    state = AsyncData(cur.copyWith(loadingMoreWorkOrders: true));
    try {
      final nextPage = cur.workOrdersPage + 1;
      final page = await ref.read(maintenanceApiProvider).listWorkOrders(
            status: cur.workOrderStatusFilter,
            search: cur.search.isEmpty ? null : cur.search,
            page: nextPage,
          );
      final merged = [...cur.workOrders, ...page.items];
      state = AsyncData(
        cur.copyWith(
          workOrders: merged,
          workOrdersTotal: page.totalCount,
          workOrdersPage: nextPage,
          loadingMoreWorkOrders: false,
          clearWorkOrdersError: true,
        ),
      );
    } catch (e) {
      state = AsyncData(
        cur.copyWith(
          loadingMoreWorkOrders: false,
          workOrdersError: ErrorHandler.message(e),
        ),
      );
    }
  }
}

final maintenanceRequestProvider =
    FutureProvider.autoDispose.family<MaintenanceRequestItem, int>((ref, id) {
  return ref.read(maintenanceApiProvider).getRequest(id);
});

final workOrderProvider =
    FutureProvider.autoDispose.family<WorkOrderItem, int>((ref, id) {
  return ref.read(maintenanceApiProvider).getWorkOrder(id);
});
