import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../domain/maintenance_models.dart';
import 'maintenance_notifier.dart';

class MaintenanceHubScreen extends ConsumerWidget {
  const MaintenanceHubScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(maintenanceHubProvider);
    final df = DateFormat('dd MMM');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Maintenance'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () =>
                ref.read(maintenanceHubProvider.notifier).refresh(),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$e'),
              FilledButton(
                onPressed: () =>
                    ref.read(maintenanceHubProvider.notifier).refresh(),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (state) {
          final q = state.search.trim().toLowerCase();
          final requests = state.requests.where((r) {
            if (q.isEmpty) return true;
            return r.requestNumber.toLowerCase().contains(q) ||
                (r.vehicleName?.toLowerCase().contains(q) ?? false) ||
                (r.vehicleRegistration?.toLowerCase().contains(q) ?? false) ||
                r.description.toLowerCase().contains(q);
          }).toList();
          final workOrders = state.workOrders.where((w) {
            if (q.isEmpty) return true;
            return w.workOrderNumber.toLowerCase().contains(q) ||
                (w.vehicleName?.toLowerCase().contains(q) ?? false) ||
                (w.vehicleRegistration?.toLowerCase().contains(q) ?? false);
          }).toList();

          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () =>
                ref.read(maintenanceHubProvider.notifier).refresh(),
            child: CustomScrollView(
              slivers: [
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
                    child: Row(
                      children: [
                        _Kpi('Due', '${state.kpis.dueForService}'),
                        _Kpi('In shop', '${state.kpis.underMaintenance}'),
                        _Kpi('WO', '${state.kpis.activeWorkOrders}'),
                        _Kpi('Pending', '${state.kpis.pendingRequests}'),
                      ],
                    ),
                  ),
                ),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    child: SegmentedButton<MaintenanceTab>(
                      segments: const [
                        ButtonSegment(
                          value: MaintenanceTab.requests,
                          label: Text('Requests'),
                          icon: Icon(Icons.assignment_outlined, size: 16),
                        ),
                        ButtonSegment(
                          value: MaintenanceTab.workOrders,
                          label: Text('Work orders'),
                          icon: Icon(Icons.build_outlined, size: 16),
                        ),
                      ],
                      selected: {state.tab},
                      onSelectionChanged: (s) => ref
                          .read(maintenanceHubProvider.notifier)
                          .setTab(s.first),
                    ),
                  ),
                ),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
                    child: TextField(
                      onChanged: (v) => ref
                          .read(maintenanceHubProvider.notifier)
                          .setSearch(v),
                      decoration: InputDecoration(
                        hintText: 'Search…',
                        prefixIcon: const Icon(Icons.search_rounded),
                        filled: true,
                        fillColor: Colors.white,
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(AppRadii.md),
                          borderSide: BorderSide.none,
                        ),
                      ),
                    ),
                  ),
                ),
                if (state.tab == MaintenanceTab.requests)
                  _requestList(context, requests, df)
                else
                  _workOrderList(context, workOrders, df),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _requestList(
    BuildContext context,
    List<MaintenanceRequestItem> items,
    DateFormat df,
  ) {
    if (items.isEmpty) {
      return const SliverFillRemaining(
        hasScrollBody: false,
        child: Center(child: Text('No requests')),
      );
    }
    return SliverPadding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
      sliver: SliverList.builder(
        itemCount: items.length,
        itemBuilder: (_, i) {
          final r = items[i];
          return SgCard(
            margin: const EdgeInsets.only(bottom: 10),
            onTap: () =>
                context.push('/more/maintenance/requests/${r.id}'),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        r.requestNumber,
                        style: const TextStyle(fontWeight: FontWeight.w800),
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
                  style: const TextStyle(color: AppColors.textSecondary),
                ),
                const SizedBox(height: 4),
                Text(
                  '${r.priority} · ${r.issueCategory} · ${df.format(r.requestDate.toLocal())}',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textMuted,
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _workOrderList(
    BuildContext context,
    List<WorkOrderItem> items,
    DateFormat df,
  ) {
    if (items.isEmpty) {
      return const SliverFillRemaining(
        hasScrollBody: false,
        child: Center(child: Text('No work orders')),
      );
    }
    return SliverPadding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
      sliver: SliverList.builder(
        itemCount: items.length,
        itemBuilder: (_, i) {
          final w = items[i];
          return SgCard(
            margin: const EdgeInsets.only(bottom: 10),
            onTap: () =>
                context.push('/more/maintenance/work-orders/${w.id}'),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        w.workOrderNumber,
                        style: const TextStyle(fontWeight: FontWeight.w800),
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
                  style: const TextStyle(color: AppColors.textSecondary),
                ),
                const SizedBox(height: 4),
                Text(
                  [
                    if (w.serviceTypeName != null) w.serviceTypeName!,
                    if (w.workshopName != null) w.workshopName!,
                    df.format(w.createdAt.toLocal()),
                  ].join(' · '),
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textMuted,
                  ),
                ),
              ],
            ),
          );
        },
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
            style: const TextStyle(fontSize: 11, color: AppColors.textMuted),
          ),
        ],
      ),
    );
  }
}

class MaintenanceRequestDetailScreen extends ConsumerWidget {
  const MaintenanceRequestDetailScreen({super.key, required this.requestId});
  final int requestId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(maintenanceRequestProvider(requestId));
    final df = DateFormat('dd MMM yyyy');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Request')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('$e')),
        data: (r) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            SgCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          r.requestNumber,
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      StatusBadge(r.status),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(r.description),
                ],
              ),
            ),
            const SizedBox(height: 12),
            SgCard(
              child: Column(
                children: [
                  _Row('Vehicle',
                      r.vehicleName ?? r.vehicleRegistration ?? '#${r.vehicleId}'),
                  _Row('Driver', r.driverName ?? '—'),
                  _Row('Type', r.requestType),
                  _Row('Category', r.issueCategory),
                  _Row('Priority', r.priority),
                  _Row('Date', df.format(r.requestDate.toLocal())),
                  if (r.workOrderId != null)
                    _Row('Work order', '#${r.workOrderId}'),
                  if (r.rejectionReason != null)
                    _Row('Rejection', r.rejectionReason!),
                ],
              ),
            ),
            if (r.vehicleId > 0) ...[
              const SizedBox(height: 12),
              TextButton(
                onPressed: () =>
                    context.push('/fleet/vehicles/${r.vehicleId}'),
                child: const Text('Open vehicle'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class WorkOrderDetailScreen extends ConsumerWidget {
  const WorkOrderDetailScreen({super.key, required this.workOrderId});
  final int workOrderId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(workOrderProvider(workOrderId));
    final df = DateFormat('dd MMM yyyy');
    final currency = NumberFormat.currency(symbol: '', decimalDigits: 0);

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Work order')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('$e')),
        data: (w) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            SgCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          w.workOrderNumber,
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      StatusBadge(w.status),
                    ],
                  ),
                  if (w.serviceTypeName != null) ...[
                    const SizedBox(height: 6),
                    Text(w.serviceTypeName!),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 12),
            SgCard(
              child: Column(
                children: [
                  _Row('Vehicle',
                      w.vehicleName ?? w.vehicleRegistration ?? '#${w.vehicleId}'),
                  _Row('Workshop', w.workshopName ?? '—'),
                  _Row('Technician', w.technicianName ?? '—'),
                  _Row('Priority', w.priority ?? '—'),
                  _Row('Type', w.maintenanceType ?? '—'),
                  _Row('Total cost', currency.format(w.totalCost)),
                  _Row(
                    'Created',
                    df.format(w.createdAt.toLocal()),
                  ),
                  if (w.completedAt != null)
                    _Row('Completed', df.format(w.completedAt!.toLocal())),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          SizedBox(
            width: 110,
            child: Text(
              label,
              style: const TextStyle(color: AppColors.textMuted, fontSize: 13),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
            ),
          ),
        ],
      ),
    );
  }
}
