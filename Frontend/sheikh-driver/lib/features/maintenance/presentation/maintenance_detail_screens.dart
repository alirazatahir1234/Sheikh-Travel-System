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
import 'maintenance_notifier.dart';

String _pkr(num value) {
  final v = value.toDouble();
  if (v >= 1000000) return 'PKR ${(v / 1000000).toStringAsFixed(1)}M';
  if (v >= 1000) {
    return 'PKR ${NumberFormat('#,##0').format(v.round())}';
  }
  return 'PKR ${v.toStringAsFixed(0)}';
}

class MaintenanceRequestDetailScreen extends ConsumerStatefulWidget {
  const MaintenanceRequestDetailScreen({super.key, required this.requestId});
  final int requestId;

  @override
  ConsumerState<MaintenanceRequestDetailScreen> createState() =>
      _MaintenanceRequestDetailScreenState();
}

class _MaintenanceRequestDetailScreenState
    extends ConsumerState<MaintenanceRequestDetailScreen> {
  bool _busy = false;

  Future<void> _run(Future<void> Function() action, {String? ok}) async {
    setState(() => _busy = true);
    try {
      await action();
      ref.invalidate(maintenanceRequestProvider(widget.requestId));
      await ref.read(maintenanceHubProvider.notifier).refresh();
      if (mounted && ok != null) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ok)));
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(ErrorHandler.message(e))),
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _reject() async {
    final reason = await showSgTextEntrySheet(
      context,
      title: 'Rejection reason',
      hint: 'Why is this request rejected?',
      maxLines: 3,
    );
    if (reason == null || reason.trim().isEmpty) return;
    await _run(
      () => ref.read(maintenanceApiProvider).rejectRequest(
            widget.requestId,
            reason: reason.trim(),
          ),
      ok: 'Request rejected',
    );
  }

  Future<void> _convert() async {
    await _run(() async {
      final woId = await ref
          .read(maintenanceApiProvider)
          .convertRequest(widget.requestId);
      if (mounted) {
        context.push('/fleet/maintenance/work-orders/$woId');
      }
    }, ok: 'Converted to work order');
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(maintenanceRequestProvider(widget.requestId));
    final session = ref.watch(fleetSessionProvider);
    final canApprove = session?.hasPermission(
          FleetPermissions.maintenanceRequestApprove,
        ) ??
        false;
    final canConvert = session?.hasPermission(
          FleetPermissions.maintenanceWorkOrderManage,
        ) ??
        false;
    final df = DateFormat('dd MMM yyyy');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Service request')),
      body: async.when(
        loading: () => const Center(child: SgBrandLoader()),
        error: (e, _) => SgErrorView(
          message: ErrorHandler.message(e),
          onRetry: () =>
              ref.invalidate(maintenanceRequestProvider(widget.requestId)),
        ),
        data: (r) => ListView(
          physics: const AlwaysScrollableScrollPhysics(),
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
                  _Row(
                    'Vehicle',
                    r.vehicleName ?? r.vehicleRegistration ?? '#${r.vehicleId}',
                  ),
                  _Row('Driver', r.driverName ?? '—'),
                  _Row('Type', r.requestType),
                  _Row('Category', r.issueCategory),
                  _Row('Priority', r.priority),
                  _Row('Date', df.format(r.requestDate.toLocal())),
                  if (r.breakdownLocation != null &&
                      r.breakdownLocation!.isNotEmpty)
                    _Row('Location', r.breakdownLocation!),
                  if (r.driverRemarks != null && r.driverRemarks!.isNotEmpty)
                    _Row('Driver notes', r.driverRemarks!),
                  if (r.branchName != null) _Row('Branch', r.branchName!),
                  if (r.workOrderId != null)
                    _Row('Work order', '#${r.workOrderId}'),
                  if (r.approvedBy != null)
                    _Row(
                      'Approved',
                      '${r.approvedBy}${r.approvedAt != null ? ' · ${df.format(r.approvedAt!.toLocal())}' : ''}',
                    ),
                  if (r.rejectionReason != null)
                    _Row('Rejection', r.rejectionReason!),
                  if (r.rejectedBy != null) _Row('Rejected by', r.rejectedBy!),
                ],
              ),
            ),
            if (r.vehicleId > 0) ...[
              const SizedBox(height: 12),
              OutlinedButton(
                onPressed: () => context.push('/fleet/vehicles/${r.vehicleId}'),
                child: const Text('Open vehicle'),
              ),
            ],
            if (r.workOrderId != null) ...[
              const SizedBox(height: 8),
              OutlinedButton(
                onPressed: () => context.push(
                  '/fleet/maintenance/work-orders/${r.workOrderId}',
                ),
                child: const Text('Open work order'),
              ),
            ],
            if (!_busy &&
                (canApprove || canConvert) &&
                (r.canApprove || r.canConvert)) ...[
              const SizedBox(height: 16),
              if (canApprove && r.canApprove) ...[
                FilledButton(
                  onPressed: () => _run(
                    () => ref
                        .read(maintenanceApiProvider)
                        .approveRequest(widget.requestId),
                    ok: 'Request approved',
                  ),
                  child: const Text('Approve'),
                ),
                const SizedBox(height: 8),
                OutlinedButton(
                  onPressed: _reject,
                  child: const Text('Reject'),
                ),
              ],
              if (canConvert && r.canConvert) ...[
                const SizedBox(height: 8),
                FilledButton.tonal(
                  onPressed: _convert,
                  child: const Text('Convert to work order'),
                ),
              ],
            ],
            if (_busy) ...[
              const SizedBox(height: 16),
              const Center(child: SgBrandLoader()),
            ],
          ],
        ),
      ),
    );
  }
}

class WorkOrderDetailScreen extends ConsumerStatefulWidget {
  const WorkOrderDetailScreen({super.key, required this.workOrderId});
  final int workOrderId;

  @override
  ConsumerState<WorkOrderDetailScreen> createState() =>
      _WorkOrderDetailScreenState();
}

class _WorkOrderDetailScreenState extends ConsumerState<WorkOrderDetailScreen> {
  bool _busy = false;

  Future<void> _setStatus(String status) async {
    setState(() => _busy = true);
    try {
      await ref.read(maintenanceApiProvider).updateWorkOrderStatus(
            widget.workOrderId,
            status: status,
          );
      ref.invalidate(workOrderProvider(widget.workOrderId));
      await ref.read(maintenanceHubProvider.notifier).refresh();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Status → $status')),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(ErrorHandler.message(e))),
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(workOrderProvider(widget.workOrderId));
    final session = ref.watch(fleetSessionProvider);
    final canManage = session?.hasPermission(
          FleetPermissions.maintenanceWorkOrderManage,
        ) ??
        false;
    final df = DateFormat('dd MMM yyyy');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Work order')),
      body: async.when(
        loading: () => const Center(child: SgBrandLoader()),
        error: (e, _) => SgErrorView(
          message: ErrorHandler.message(e),
          onRetry: () => ref.invalidate(workOrderProvider(widget.workOrderId)),
        ),
        data: (w) => ListView(
          physics: const AlwaysScrollableScrollPhysics(),
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
                  if (w.notes != null && w.notes!.isNotEmpty) ...[
                    const SizedBox(height: 8),
                    Text(
                      w.notes!,
                      style: const TextStyle(color: AppColors.textSecondary),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 12),
            SgCard(
              child: Column(
                children: [
                  _Row(
                    'Vehicle',
                    w.vehicleName ?? w.vehicleRegistration ?? '#${w.vehicleId}',
                  ),
                  if (w.driverName != null) _Row('Driver', w.driverName!),
                  _Row('Workshop', w.workshopName ?? '—'),
                  _Row('Technician', w.technicianName ?? '—'),
                  _Row('Priority', w.priority ?? '—'),
                  _Row('Type', w.maintenanceType ?? '—'),
                  _Row('Labor', _pkr(w.laborCost)),
                  _Row('Parts', _pkr(w.partsCost)),
                  _Row('Total cost', _pkr(w.totalCost)),
                  _Row('Created', df.format(w.createdAt.toLocal())),
                  if (w.startDate != null)
                    _Row('Start', df.format(w.startDate!.toLocal())),
                  if (w.estimatedCompletionDate != null)
                    _Row(
                      'ETA',
                      df.format(w.estimatedCompletionDate!.toLocal()),
                    ),
                  if (w.completedAt != null)
                    _Row('Completed', df.format(w.completedAt!.toLocal())),
                  if (w.technicianNotes != null &&
                      w.technicianNotes!.isNotEmpty)
                    _Row('Tech notes', w.technicianNotes!),
                ],
              ),
            ),
            if (w.partsUsage.isNotEmpty) ...[
              const SizedBox(height: 12),
              const Text(
                'Parts used',
                style: TextStyle(fontWeight: FontWeight.w800),
              ),
              const SizedBox(height: 8),
              SgCard(
                child: Column(
                  children: w.partsUsage
                      .map(
                        (p) => Padding(
                          padding: const EdgeInsets.symmetric(vertical: 6),
                          child: Row(
                            children: [
                              Expanded(
                                child: Text(
                                  '${p.partName} × ${p.quantity}',
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ),
                              Text(_pkr(p.totalCost)),
                            ],
                          ),
                        ),
                      )
                      .toList(),
                ),
              ),
            ],
            if (w.vehicleId > 0) ...[
              const SizedBox(height: 12),
              OutlinedButton(
                onPressed: () => context.push('/fleet/vehicles/${w.vehicleId}'),
                child: const Text('Open vehicle'),
              ),
            ],
            if (w.requestId != null) ...[
              const SizedBox(height: 8),
              OutlinedButton(
                onPressed: () => context.push(
                  '/fleet/maintenance/requests/${w.requestId}',
                ),
                child: const Text('Open service request'),
              ),
            ],
            if (canManage && w.canComplete && !_busy) ...[
              const SizedBox(height: 16),
              if (w.status == 'Draft' || w.status == 'Open')
                FilledButton.tonal(
                  onPressed: () => _setStatus('InProgress'),
                  child: const Text('Start work'),
                ),
              if (w.status == 'InProgress' ||
                  w.status == 'Assigned' ||
                  w.status == 'WaitingParts') ...[
                const SizedBox(height: 8),
                FilledButton(
                  onPressed: () => _setStatus('Completed'),
                  child: const Text('Mark completed'),
                ),
              ],
              const SizedBox(height: 8),
              OutlinedButton(
                onPressed: () => _setStatus('Cancelled'),
                child: const Text('Cancel'),
              ),
            ],
            if (_busy) ...[
              const SizedBox(height: 16),
              const Center(child: SgBrandLoader()),
            ],
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
        crossAxisAlignment: CrossAxisAlignment.start,
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
