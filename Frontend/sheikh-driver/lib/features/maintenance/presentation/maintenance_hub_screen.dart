import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../auth/data/auth_repository.dart';
import '../../auth/domain/auth_models.dart';
import '../data/maintenance_api.dart';
import '../domain/maintenance_models.dart';
import 'maintenance_forms.dart';
import 'maintenance_notifier.dart';

String _pkr(num value) {
  final v = value.toDouble();
  if (v >= 1000000) return 'PKR ${(v / 1000000).toStringAsFixed(1)}M';
  if (v >= 1000) {
    return 'PKR ${NumberFormat('#,##0').format(v.round())}';
  }
  return 'PKR ${v.toStringAsFixed(0)}';
}

class MaintenanceHubScreen extends ConsumerStatefulWidget {
  const MaintenanceHubScreen({super.key});

  @override
  ConsumerState<MaintenanceHubScreen> createState() =>
      _MaintenanceHubScreenState();
}

class _MaintenanceHubScreenState extends ConsumerState<MaintenanceHubScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 4, vsync: this);
    _tabs.addListener(_onTabChanged);
  }

  void _onTabChanged() {
    if (_tabs.indexIsChanging) return;
    final tab = MaintenanceTab.values[_tabs.index];
    ref.read(maintenanceHubProvider.notifier).setTab(tab);
    setState(() {}); // refresh FAB for active tab
  }

  @override
  void dispose() {
    _tabs.removeListener(_onTabChanged);
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(maintenanceHubProvider);
    final session = ref.watch(fleetSessionProvider);
    final canCreateRequest = session?.hasPermission(
          FleetPermissions.maintenanceRequestCreate,
        ) ??
        false;
    final canManageWo = session?.hasPermission(
          FleetPermissions.maintenanceWorkOrderManage,
        ) ??
        false;

    // Keep TabController in sync if notifier tab changes programmatically.
    final hubTab = async.valueOrNull?.tab;
    if (hubTab != null &&
        _tabs.index != hubTab.index &&
        !_tabs.indexIsChanging) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted && _tabs.index != hubTab.index) {
          _tabs.animateTo(hubTab.index);
        }
      });
    }

    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(
        title: const Text('Maintenance'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () =>
                ref.read(maintenanceHubProvider.notifier).refresh(),
          ),
        ],
        bottom: TabBar(
          controller: _tabs,
          isScrollable: true,
          labelColor: AppColors.primary,
          unselectedLabelColor: context.sg.textSecondary,
          indicatorColor: AppColors.primary,
          tabs: const [
            Tab(text: 'Dashboard'),
            Tab(text: 'Service request'),
            Tab(text: 'Work order'),
            Tab(text: 'Service schedule'),
          ],
        ),
      ),
      floatingActionButton: _fab(canCreateRequest, canManageWo),
      body: async.when(
        skipLoadingOnReload: true,
        loading: () => const Center(child: SgBrandLoader()),
        error: (e, _) => SgErrorView(
          message: ErrorHandler.message(e),
          onRetry: () => ref.read(maintenanceHubProvider.notifier).refresh(),
        ),
        data: (state) => TabBarView(
          controller: _tabs,
          children: [
            _DashboardTab(state: state),
            _RequestsTab(state: state),
            _WorkOrdersTab(state: state),
            _SchedulesTab(state: state),
          ],
        ),
      ),
    );
  }

  Widget? _fab(bool canCreateRequest, bool canManageWo) {
    final tab = MaintenanceTab.values[_tabs.index];
    if (tab == MaintenanceTab.requests && canCreateRequest) {
      return FloatingActionButton.extended(
        onPressed: () => showCreateMaintenanceRequestSheet(context, ref),
        icon: const Icon(Icons.add),
        label: const Text('Request'),
      );
    }
    if (tab == MaintenanceTab.workOrders && canManageWo) {
      return FloatingActionButton.extended(
        onPressed: () => showCreateWorkOrderSheet(context, ref),
        icon: const Icon(Icons.add),
        label: const Text('Work order'),
      );
    }
    return null;
  }
}

class _DashboardTab extends ConsumerWidget {
  const _DashboardTab({required this.state});
  final MaintenanceHubState state;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final df = DateFormat('dd MMM');
    final kpis = state.kpis;
    final dash = state.dashboard;

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: () => ref.read(maintenanceHubProvider.notifier).refresh(),
      child: CustomScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: [
          if (state.dashboardError != null)
            SliverToBoxAdapter(
              child: _SectionError(
                message: state.dashboardError!,
                onRetry: () =>
                    ref.read(maintenanceHubProvider.notifier).refresh(),
              ),
            ),
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      _Kpi('Due', '${kpis.dueForService}'),
                      _Kpi('In shop', '${kpis.underMaintenance}'),
                      _Kpi('Overdue', '${kpis.overdueServices}'),
                      _Kpi('Pending', '${kpis.pendingRequests}'),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      _Kpi('Active WO', '${kpis.activeWorkOrders}'),
                      _Kpi('Vehicles', '${kpis.totalVehicles}'),
                      Expanded(
                        child: Column(
                          children: [
                            Text(
                              _pkr(kpis.monthlyMaintenanceCost),
                              style: const TextStyle(
                                fontWeight: FontWeight.w800,
                                fontSize: 14,
                              ),
                              textAlign: TextAlign.center,
                            ),
                            Text(
                              'Month cost',
                              style: TextStyle(
                                fontSize: 11,
                                color: context.sg.textMuted,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 14),
                  Text(
                    'Requests · Open ${state.requestStats.open} · '
                    'Pending ${state.requestStats.pendingApproval} · '
                    'Approved ${state.requestStats.approved}',
                    style: TextStyle(
                      fontSize: 12,
                      color: context.sg.textMuted,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Work orders · Open ${state.workOrderStats.open} · '
                    'In progress ${state.workOrderStats.inProgress} · '
                    'Done ${state.workOrderStats.completed}',
                    style: TextStyle(
                      fontSize: 12,
                      color: context.sg.textMuted,
                    ),
                  ),
                ],
              ),
            ),
          ),
          if (dash.predictiveAlerts.isNotEmpty) ...[
            const SliverToBoxAdapter(
              child: Padding(
                padding: EdgeInsets.fromLTRB(16, 14, 16, 6),
                child: Text(
                  'Predictive alerts',
                  style: TextStyle(fontWeight: FontWeight.w800),
                ),
              ),
            ),
            ...dash.predictiveAlerts.take(12).map(
                  (a) => SliverToBoxAdapter(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 4,
                      ),
                      child: SgCard(
                        onTap: a.vehicleId > 0
                            ? () =>
                                context.push('/fleet/vehicles/${a.vehicleId}')
                            : null,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                _PredictiveCategoryChip(a.categoryLabel),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: Text(
                                    a.title,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                ),
                                StatusBadge(a.severity),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Text(
                              a.message,
                              style: TextStyle(
                                color: context.sg.textSecondary,
                                fontSize: 13,
                              ),
                            ),
                            if (a.predictedDueDate != null ||
                                a.remainingKm != null ||
                                a.progressPercent != null) ...[
                              const SizedBox(height: 6),
                              Text(
                                [
                                  if (a.predictedDueDate != null)
                                    'Due ${df.format(a.predictedDueDate!.toLocal())}',
                                  if (a.remainingKm != null)
                                    '${a.remainingKm!.round()} km left',
                                  if (a.progressPercent != null)
                                    '${a.progressPercent}%',
                                ].join(' · '),
                                style: TextStyle(
                                  fontSize: 12,
                                  color: context.sg.textMuted,
                                ),
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
          ],
          if (dash.criticalAlerts.isNotEmpty) ...[
            const SliverToBoxAdapter(
              child: Padding(
                padding: EdgeInsets.fromLTRB(16, 12, 16, 6),
                child: Text(
                  'Critical alerts',
                  style: TextStyle(fontWeight: FontWeight.w800),
                ),
              ),
            ),
            ...dash.criticalAlerts.take(5).map(
                  (a) => SliverToBoxAdapter(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 4,
                      ),
                      child: SgCard(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    a.title,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                ),
                                StatusBadge(a.severity),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Text(
                              a.message,
                              style: TextStyle(
                                color: context.sg.textSecondary,
                                fontSize: 13,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
          ],
          if (dash.upcomingServices.isNotEmpty) ...[
            const SliverToBoxAdapter(
              child: Padding(
                padding: EdgeInsets.fromLTRB(16, 12, 16, 6),
                child: Text(
                  'Upcoming services',
                  style: TextStyle(fontWeight: FontWeight.w800),
                ),
              ),
            ),
            ...dash.upcomingServices.take(8).map(
                  (s) => SliverToBoxAdapter(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 4,
                      ),
                      child: SgCard(
                        onTap: s.vehicleId > 0
                            ? () =>
                                context.push('/fleet/vehicles/${s.vehicleId}')
                            : null,
                        child: Row(
                          children: [
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    s.serviceType,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                  Text(
                                    s.vehicleName.isNotEmpty
                                        ? s.vehicleName
                                        : (s.vehicleRegistration ??
                                            '#${s.vehicleId}'),
                                    style: TextStyle(
                                      color: context.sg.textSecondary,
                                      fontSize: 13,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            Text(
                              s.dueDate != null
                                  ? df.format(s.dueDate!.toLocal())
                                  : s.priority,
                              style: TextStyle(
                                fontSize: 12,
                                color: context.sg.textMuted,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
          ],
          if (dash.recentWorkOrders.isNotEmpty) ...[
            const SliverToBoxAdapter(
              child: Padding(
                padding: EdgeInsets.fromLTRB(16, 12, 16, 6),
                child: Text(
                  'Recent work orders',
                  style: TextStyle(fontWeight: FontWeight.w800),
                ),
              ),
            ),
            ...dash.recentWorkOrders.take(8).map(
                  (w) => SliverToBoxAdapter(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 4,
                      ),
                      child: SgCard(
                        onTap: () => context
                            .push('/fleet/maintenance/work-orders/${w.id}'),
                        child: Row(
                          children: [
                            Expanded(
                              child: Text(
                                w.workOrderNumber,
                                style: const TextStyle(
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                            StatusBadge(w.status),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
          ],
          const SliverToBoxAdapter(child: SizedBox(height: 32)),
        ],
      ),
    );
  }
}

class _RequestsTab extends ConsumerWidget {
  const _RequestsTab({required this.state});
  final MaintenanceHubState state;

  static const _filters = [
    'Open',
    'PendingApproval',
    'Approved',
    'Rejected',
    'Converted',
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final df = DateFormat('dd MMM');
    final q = state.search.trim().toLowerCase();
    final items = q.isEmpty
        ? state.requests
        : state.requests.where((r) {
            return r.requestNumber.toLowerCase().contains(q) ||
                (r.vehicleName?.toLowerCase().contains(q) ?? false) ||
                (r.vehicleRegistration?.toLowerCase().contains(q) ?? false) ||
                r.description.toLowerCase().contains(q);
          }).toList();

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: () => ref.read(maintenanceHubProvider.notifier).refresh(),
      child: CustomScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: [
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
              child: SgSearchField(
                hintText: 'Search requests…',
                onChanged: (v) {
                  ref.read(maintenanceHubProvider.notifier).setSearch(v);
                  if (v.trim().isEmpty) {
                    ref.read(maintenanceHubProvider.notifier).applySearch();
                  }
                },
              ),
            ),
          ),
          SliverToBoxAdapter(
            child: SizedBox(
              height: 40,
              child: ListView(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: 12),
                children: [
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4),
                    child: FilterChip(
                      label: const Text('All'),
                      selected: state.requestStatusFilter == null,
                      onSelected: (_) => ref
                          .read(maintenanceHubProvider.notifier)
                          .setRequestStatusFilter(null),
                    ),
                  ),
                  ..._filters.map(
                    (s) => Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 4),
                      child: FilterChip(
                        label: Text(s),
                        selected: state.requestStatusFilter == s,
                        onSelected: (_) => ref
                            .read(maintenanceHubProvider.notifier)
                            .setRequestStatusFilter(s),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          if (state.requestsError != null)
            SliverToBoxAdapter(
              child: _SectionError(
                message: state.requestsError!,
                onRetry: () =>
                    ref.read(maintenanceHubProvider.notifier).refresh(),
              ),
            ),
          if (items.isEmpty && state.requestsError == null)
            const SliverFillRemaining(
              hasScrollBody: false,
              child: Center(child: Text('No requests')),
            )
          else
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
              sliver: SliverList.builder(
                itemCount: items.length + (state.hasMoreRequests ? 1 : 0),
                itemBuilder: (_, i) {
                  if (i >= items.length) {
                    return Padding(
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      child: Center(
                        child: state.loadingMoreRequests
                            ? const CircularProgressIndicator()
                            : TextButton(
                                onPressed: () => ref
                                    .read(maintenanceHubProvider.notifier)
                                    .loadMoreRequests(),
                                child: Text(
                                  'Load more (${items.length}/${state.requestsTotal})',
                                ),
                              ),
                      ),
                    );
                  }
                  final r = items[i];
                  return SgCard(
                    margin: const EdgeInsets.only(bottom: 10),
                    onTap: () =>
                        context.push('/fleet/maintenance/requests/${r.id}'),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Text(
                                r.requestNumber,
                                style: const TextStyle(
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                            ),
                            StatusBadge(r.status),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          r.vehicleName ??
                              r.vehicleRegistration ??
                              'Vehicle #${r.vehicleId}',
                          style: TextStyle(
                            color: context.sg.textSecondary,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '${r.priority} · ${r.issueCategory} · ${df.format(r.requestDate.toLocal())}',
                          style: TextStyle(
                            fontSize: 12,
                            color: context.sg.textMuted,
                          ),
                        ),
                      ],
                    ),
                  );
                },
              ),
            ),
        ],
      ),
    );
  }
}

class _WorkOrdersTab extends ConsumerWidget {
  const _WorkOrdersTab({required this.state});
  final MaintenanceHubState state;

  static const _filters = [
    'Draft',
    'Open',
    'Assigned',
    'InProgress',
    'WaitingParts',
    'Completed',
    'Closed',
    'Cancelled',
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final df = DateFormat('dd MMM');
    final q = state.search.trim().toLowerCase();
    final items = q.isEmpty
        ? state.workOrders
        : state.workOrders.where((w) {
            return w.workOrderNumber.toLowerCase().contains(q) ||
                (w.vehicleName?.toLowerCase().contains(q) ?? false) ||
                (w.vehicleRegistration?.toLowerCase().contains(q) ?? false) ||
                (w.serviceTypeName?.toLowerCase().contains(q) ?? false);
          }).toList();

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: () => ref.read(maintenanceHubProvider.notifier).refresh(),
      child: CustomScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: [
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
              child: SgSearchField(
                hintText: 'Search work orders…',
                onChanged: (v) {
                  ref.read(maintenanceHubProvider.notifier).setSearch(v);
                  if (v.trim().isEmpty) {
                    ref.read(maintenanceHubProvider.notifier).applySearch();
                  }
                },
              ),
            ),
          ),
          SliverToBoxAdapter(
            child: SizedBox(
              height: 40,
              child: ListView(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: 12),
                children: [
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4),
                    child: FilterChip(
                      label: const Text('All'),
                      selected: state.workOrderStatusFilter == null,
                      onSelected: (_) => ref
                          .read(maintenanceHubProvider.notifier)
                          .setWorkOrderStatusFilter(null),
                    ),
                  ),
                  ..._filters.map(
                    (s) => Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 4),
                      child: FilterChip(
                        label: Text(s),
                        selected: state.workOrderStatusFilter == s,
                        onSelected: (_) => ref
                            .read(maintenanceHubProvider.notifier)
                            .setWorkOrderStatusFilter(s),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          if (state.workOrdersError != null)
            SliverToBoxAdapter(
              child: _SectionError(
                message: state.workOrdersError!,
                onRetry: () =>
                    ref.read(maintenanceHubProvider.notifier).refresh(),
              ),
            ),
          if (items.isEmpty && state.workOrdersError == null)
            const SliverFillRemaining(
              hasScrollBody: false,
              child: Center(child: Text('No work orders')),
            )
          else
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
              sliver: SliverList.builder(
                itemCount: items.length + (state.hasMoreWorkOrders ? 1 : 0),
                itemBuilder: (_, i) {
                  if (i >= items.length) {
                    return Padding(
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      child: Center(
                        child: state.loadingMoreWorkOrders
                            ? const CircularProgressIndicator()
                            : TextButton(
                                onPressed: () => ref
                                    .read(maintenanceHubProvider.notifier)
                                    .loadMoreWorkOrders(),
                                child: Text(
                                  'Load more (${items.length}/${state.workOrdersTotal})',
                                ),
                              ),
                      ),
                    );
                  }
                  final w = items[i];
                  return SgCard(
                    margin: const EdgeInsets.only(bottom: 10),
                    onTap: () =>
                        context.push('/fleet/maintenance/work-orders/${w.id}'),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Text(
                                w.workOrderNumber,
                                style: const TextStyle(
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                            ),
                            StatusBadge(w.status),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          w.vehicleName ??
                              w.vehicleRegistration ??
                              'Vehicle #${w.vehicleId}',
                          style: TextStyle(
                            color: context.sg.textSecondary,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          [
                            if (w.serviceTypeName != null) w.serviceTypeName!,
                            if (w.workshopName != null) w.workshopName!,
                            _pkr(w.totalCost),
                            df.format(w.createdAt.toLocal()),
                          ].join(' · '),
                          style: TextStyle(
                            fontSize: 12,
                            color: context.sg.textMuted,
                          ),
                        ),
                      ],
                    ),
                  );
                },
              ),
            ),
        ],
      ),
    );
  }
}

class _SchedulesTab extends ConsumerWidget {
  const _SchedulesTab({required this.state});
  final MaintenanceHubState state;

  static const _filters = ['Due', 'Overdue', 'Upcoming', 'Active'];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final df = DateFormat('dd MMM');
    final session = ref.watch(fleetSessionProvider);
    final canManageWo = session?.hasPermission(
          FleetPermissions.maintenanceWorkOrderManage,
        ) ??
        false;
    final q = state.search.trim().toLowerCase();
    final items = q.isEmpty
        ? state.schedules
        : state.schedules.where((s) {
            return s.serviceTypeName.toLowerCase().contains(q) ||
                (s.vehicleName?.toLowerCase().contains(q) ?? false) ||
                (s.vehicleRegistration?.toLowerCase().contains(q) ?? false);
          }).toList();

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: () => ref.read(maintenanceHubProvider.notifier).refresh(),
      child: CustomScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: [
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
              child: SgSearchField(
                hintText: 'Search schedules…',
                onChanged: (v) {
                  ref.read(maintenanceHubProvider.notifier).setSearch(v);
                  if (v.trim().isEmpty) {
                    ref.read(maintenanceHubProvider.notifier).applySearch();
                  }
                },
              ),
            ),
          ),
          SliverToBoxAdapter(
            child: SizedBox(
              height: 40,
              child: ListView(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: 12),
                children: [
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4),
                    child: FilterChip(
                      label: const Text('All'),
                      selected: state.scheduleStatusFilter == null,
                      onSelected: (_) => ref
                          .read(maintenanceHubProvider.notifier)
                          .setScheduleStatusFilter(null),
                    ),
                  ),
                  ..._filters.map(
                    (s) => Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 4),
                      child: FilterChip(
                        label: Text(s),
                        selected: state.scheduleStatusFilter == s,
                        onSelected: (_) => ref
                            .read(maintenanceHubProvider.notifier)
                            .setScheduleStatusFilter(s),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          if (state.schedulesError != null)
            SliverToBoxAdapter(
              child: _SectionError(
                message: state.schedulesError!,
                onRetry: () =>
                    ref.read(maintenanceHubProvider.notifier).refresh(),
              ),
            ),
          if (items.isEmpty && state.schedulesError == null)
            const SliverFillRemaining(
              hasScrollBody: false,
              child: Center(child: Text('No schedules')),
            )
          else
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
              sliver: SliverList.builder(
                itemCount: items.length,
                itemBuilder: (_, i) {
                  final s = items[i];
                  return SgCard(
                    margin: const EdgeInsets.only(bottom: 10),
                    onTap: () => context.push('/fleet/vehicles/${s.vehicleId}'),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Text(
                                s.serviceTypeName,
                                style: const TextStyle(
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                            ),
                            StatusBadge(s.status),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          s.vehicleName ??
                              s.vehicleRegistration ??
                              'Vehicle #${s.vehicleId}',
                          style: TextStyle(
                            color: context.sg.textSecondary,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          [
                            '${s.intervalValue} ${s.intervalType}',
                            if (s.dueDate != null)
                              'Due ${df.format(s.dueDate!.toLocal())}',
                            s.priority,
                          ].join(' · '),
                          style: TextStyle(
                            fontSize: 12,
                            color: context.sg.textMuted,
                          ),
                        ),
                        if (canManageWo) ...[
                          const SizedBox(height: 8),
                          Align(
                            alignment: Alignment.centerRight,
                            child: TextButton.icon(
                              onPressed: () => _createWoFromSchedule(
                                context,
                                ref,
                                s,
                              ),
                              icon: const Icon(Icons.build_outlined, size: 16),
                              label: const Text('Create WO'),
                            ),
                          ),
                        ],
                      ],
                    ),
                  );
                },
              ),
            ),
        ],
      ),
    );
  }

  Future<void> _createWoFromSchedule(
    BuildContext context,
    WidgetRef ref,
    MaintenanceScheduleItem s,
  ) async {
    try {
      final id = await ref
          .read(maintenanceApiProvider)
          .createWorkOrderFromSchedule(s.id);
      await ref.read(maintenanceHubProvider.notifier).refresh();
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Work order #$id created')),
        );
        context.push('/fleet/maintenance/work-orders/$id');
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(ErrorHandler.message(e))),
        );
      }
    }
  }
}

class _PredictiveCategoryChip extends StatelessWidget {
  const _PredictiveCategoryChip(this.label);
  final String label;

  @override
  Widget build(BuildContext context) {
    final color = switch (label) {
      'Oil' => const Color(0xFFB45309),
      'Tires' => const Color(0xFF1D4ED8),
      'Engine' => const Color(0xFF9F1239),
      _ => context.sg.textMuted,
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w700,
          color: color,
        ),
      ),
    );
  }
}

class _Kpi extends StatelessWidget {
  const _Kpi(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        children: [
          Text(
            value,
            style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16),
          ),
          Text(
            label,
            style: TextStyle(fontSize: 11, color: context.sg.textMuted),
          ),
        ],
      ),
    );
  }
}

class _SectionError extends StatelessWidget {
  const _SectionError({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: SgCard(
        child: Column(
          children: [
            Text(
              message,
              textAlign: TextAlign.center,
              style: TextStyle(color: context.sg.textSecondary),
            ),
            const SizedBox(height: 8),
            FilledButton(onPressed: onRetry, child: const Text('Retry')),
          ],
        ),
      ),
    );
  }
}
