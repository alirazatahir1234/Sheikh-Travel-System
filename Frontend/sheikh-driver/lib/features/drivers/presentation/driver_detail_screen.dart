import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../features/bookings/presentation/dispatch_pickers.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/drivers_api.dart';
import 'drivers_notifier.dart';

class DriverDetailScreen extends ConsumerWidget {
  const DriverDetailScreen({super.key, required this.driverId});
  final int driverId;

  Future<void> _assignVehicle(BuildContext context, WidgetRef ref) async {
    final vehicleId = await pickVehicleId(context, ref);
    if (vehicleId == null) return;
    try {
      await ref.read(driversApiProvider).assignVehicle(driverId, vehicleId);
      _invalidate(ref);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Vehicle assigned')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
      }
    }
  }

  Future<void> _unassignVehicle(BuildContext context, WidgetRef ref) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Unassign vehicle?'),
        content: const Text('This driver will have no assigned vehicle.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Unassign'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await ref.read(driversApiProvider).unassignVehicle(driverId);
      _invalidate(ref);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Vehicle unassigned')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
      }
    }
  }

  void _invalidate(WidgetRef ref) {
    ref.invalidate(driverDetailProvider(driverId));
    ref.invalidate(driversHubProvider);
    ref.invalidate(driverPerformanceProvider(driverId));
    ref.invalidate(driverViolationsProvider(driverId));
    ref.invalidate(driverAttendanceProvider(driverId));
    ref.invalidate(driverDocumentsProvider(driverId));
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(driverDetailProvider(driverId));
    final session = ref.watch(fleetSessionProvider);
    final canManage =
        session?.hasPermission(FleetPermissions.driverManageStatus) ?? false;
    final canAssign =
        session?.hasPermission(FleetPermissions.driverAssign) ?? false;
    final canPerf =
        session?.hasPermission(FleetPermissions.driverViewPerformance) ?? false;

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Driver'),
        actions: [
          if (canManage)
            PopupMenuButton<String>(
              onSelected: (status) async {
                try {
                  await ref
                      .read(driversApiProvider)
                      .updateStatus(driverId, status);
                  _invalidate(ref);
                  if (context.mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text('Status → $status')),
                    );
                  }
                } catch (e) {
                  if (context.mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text('$e')),
                    );
                  }
                }
              },
              itemBuilder: (_) => const [
                PopupMenuItem(value: 'Available', child: Text('Available')),
                PopupMenuItem(value: 'OnTrip', child: Text('On trip')),
                PopupMenuItem(value: 'OffDuty', child: Text('Off duty')),
                PopupMenuItem(value: 'OnLeave', child: Text('On leave')),
                PopupMenuItem(value: 'Suspended', child: Text('Suspended')),
              ],
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
                    ref.invalidate(driverDetailProvider(driverId)),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (d) {
          final df = DateFormat('dd MMM yyyy');
          final tf = DateFormat('dd MMM, HH:mm');
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
            children: [
              SgCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            d.fullName,
                            style: const TextStyle(
                              fontSize: 20,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                        ),
                        StatusBadge(d.status),
                      ],
                    ),
                    if (d.driverCode != null) ...[
                      const SizedBox(height: 4),
                      Text(
                        d.driverCode!,
                        style: const TextStyle(color: AppColors.textMuted),
                      ),
                    ],
                    const SizedBox(height: 8),
                    Text(d.phone,
                        style: const TextStyle(fontWeight: FontWeight.w600)),
                    if (d.email != null) Text(d.email!),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              const SgSectionTitle('License & compliance'),
              const SizedBox(height: 8),
              SgCard(
                child: Column(
                  children: [
                    _Row('License', d.licenseNumber),
                    _Row(
                      'Expiry',
                      d.licenseExpiryDate != null
                          ? df.format(d.licenseExpiryDate!.toLocal())
                          : '—',
                    ),
                    _Row(
                      'License status',
                      d.licenseExpired
                          ? 'Expired'
                          : d.licenseExpiringSoon
                              ? 'Expiring soon'
                              : 'Valid',
                    ),
                    _Row('Verification', d.verificationStatus ?? '—'),
                    _Row('CNIC', d.cnic ?? '—'),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              const SgSectionTitle('Assignment'),
              const SizedBox(height: 8),
              SgCard(
                onTap: d.assignedVehicleId != null
                    ? () => context
                        .push('/fleet/vehicles/${d.assignedVehicleId}')
                    : null,
                child: Column(
                  children: [
                    _Row(
                      'Vehicle',
                      d.assignedVehicleName ??
                          d.assignedVehicleRegistration ??
                          'Unassigned',
                    ),
                    _Row('Plate', d.assignedVehicleRegistration ?? '—'),
                    _Row('Branch', d.branchName ?? '—'),
                    _Row('GPS', d.gpsOnline ? 'Online' : 'Offline'),
                    _Row('Availability', d.availabilityBucket ?? '—'),
                    _Row(
                      'Rating',
                      d.rating != null ? d.rating!.toStringAsFixed(1) : '—',
                    ),
                  ],
                ),
              ),
              if (canAssign) ...[
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: () => _assignVehicle(context, ref),
                        icon: const Icon(Icons.local_shipping_outlined),
                        label: Text(
                          d.assignedVehicleId != null ? 'Reassign' : 'Assign',
                        ),
                      ),
                    ),
                    if (d.assignedVehicleId != null) ...[
                      const SizedBox(width: 8),
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: () => _unassignVehicle(context, ref),
                          icon: const Icon(Icons.link_off_outlined),
                          label: const Text('Unassign'),
                        ),
                      ),
                    ],
                  ],
                ),
              ],
              if (canPerf) ...[
                const SizedBox(height: 12),
                const SgSectionTitle('Performance'),
                const SizedBox(height: 8),
                _PerformanceCard(driverId: driverId),
                const SizedBox(height: 12),
                const SgSectionTitle('Violations'),
                const SizedBox(height: 8),
                _ViolationsCard(driverId: driverId, df: df),
                const SizedBox(height: 12),
                const SgSectionTitle('Attendance (30 days)'),
                const SizedBox(height: 8),
                _AttendanceCard(driverId: driverId, tf: tf),
              ],
              const SizedBox(height: 12),
              const SgSectionTitle('Documents'),
              const SizedBox(height: 8),
              _DocumentsCard(driverId: driverId, df: df),
              const SizedBox(height: 12),
              const SgSectionTitle('Profile'),
              const SizedBox(height: 8),
              SgCard(
                child: Column(
                  children: [
                    _Row('Address', d.address ?? '—'),
                    _Row(
                      'Hire date',
                      d.hireDate != null
                          ? df.format(d.hireDate!.toLocal())
                          : '—',
                    ),
                    _Row(
                      'Experience',
                      d.yearsExperience != null
                          ? '${d.yearsExperience} yrs'
                          : '—',
                    ),
                    _Row(
                      'Emergency',
                      [
                        d.emergencyContactName,
                        d.emergencyContact,
                      ]
                          .whereType<String>()
                          .where((s) => s.isNotEmpty)
                          .join(' · ')
                          .ifEmpty('—'),
                    ),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _PerformanceCard extends ConsumerWidget {
  const _PerformanceCard({required this.driverId});
  final int driverId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(driverPerformanceProvider(driverId));
    return async.when(
      loading: () => const SgCard(
        child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
      ),
      error: (_, __) => const SgCard(child: Text('Performance unavailable')),
      data: (p) {
        if (p == null) {
          return const SgCard(child: Text('Performance unavailable'));
        }
        return SgCard(
          child: Column(
            children: [
              _Row('Trips', '${p.completedTrips} / ${p.totalTrips}'),
              _Row(
                'Completion',
                '${p.completionRate.toStringAsFixed(0)}%',
              ),
              _Row('Revenue', p.totalRevenue.toStringAsFixed(0)),
              _Row('Violations', '${p.violationCount}'),
              _Row('Present days', '${p.attendancePresentCount}'),
              _Row(
                'Rating',
                p.rating != null ? p.rating!.toStringAsFixed(1) : '—',
              ),
            ],
          ),
        );
      },
    );
  }
}

class _ViolationsCard extends ConsumerWidget {
  const _ViolationsCard({required this.driverId, required this.df});
  final int driverId;
  final DateFormat df;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(driverViolationsProvider(driverId));
    return async.when(
      loading: () => const SgCard(
        child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
      ),
      error: (_, __) => const SgCard(child: Text('No violations loaded')),
      data: (items) {
        if (items.isEmpty) {
          return const SgCard(child: Text('No violations'));
        }
        return SgCard(
          child: Column(
            children: [
              for (final v in items.take(8))
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  dense: true,
                  title: Text(
                    v.violationType,
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                  subtitle: Text(
                    [
                      v.severity,
                      df.format(v.occurredAt.toLocal()),
                      v.status,
                      if (v.description != null && v.description!.isNotEmpty)
                        v.description!,
                    ].join(' · '),
                    style: const TextStyle(fontSize: 12),
                  ),
                ),
            ],
          ),
        );
      },
    );
  }
}

class _AttendanceCard extends ConsumerWidget {
  const _AttendanceCard({required this.driverId, required this.tf});
  final int driverId;
  final DateFormat tf;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(driverAttendanceProvider(driverId));
    return async.when(
      loading: () => const SgCard(
        child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
      ),
      error: (_, __) => const SgCard(child: Text('No attendance loaded')),
      data: (items) {
        if (items.isEmpty) {
          return const SgCard(child: Text('No attendance records'));
        }
        return SgCard(
          child: Column(
            children: [
              for (final a in items.take(10))
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  dense: true,
                  title: Text(
                    DateFormat('dd MMM yyyy').format(a.attendanceDate.toLocal()),
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                  subtitle: Text(
                    [
                      a.status,
                      if (a.checkInAt != null)
                        'in ${tf.format(a.checkInAt!.toLocal())}',
                      if (a.checkOutAt != null)
                        'out ${tf.format(a.checkOutAt!.toLocal())}',
                    ].join(' · '),
                    style: const TextStyle(fontSize: 12),
                  ),
                ),
            ],
          ),
        );
      },
    );
  }
}

class _DocumentsCard extends ConsumerWidget {
  const _DocumentsCard({required this.driverId, required this.df});
  final int driverId;
  final DateFormat df;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(driverDocumentsProvider(driverId));
    return async.when(
      loading: () => const SgCard(
        child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
      ),
      error: (_, __) => const SgCard(child: Text('No documents loaded')),
      data: (items) {
        if (items.isEmpty) {
          return const SgCard(child: Text('No documents on file'));
        }
        return SgCard(
          child: Column(
            children: [
              for (final doc in items)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  dense: true,
                  title: Text(
                    doc.documentType,
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                  subtitle: Text(
                    [
                      doc.status,
                      if (doc.expiryDate != null)
                        'exp ${df.format(doc.expiryDate!.toLocal())}',
                      if (doc.isExpired)
                        'EXPIRED'
                      else if (doc.isExpiringSoon)
                        'expiring soon',
                    ].join(' · '),
                    style: TextStyle(
                      fontSize: 12,
                      color: doc.isExpired
                          ? AppColors.error
                          : doc.isExpiringSoon
                              ? AppColors.warning
                              : null,
                    ),
                  ),
                ),
            ],
          ),
        );
      },
    );
  }
}

extension on String {
  String ifEmpty(String fallback) => isEmpty ? fallback : this;
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
              style: const TextStyle(fontSize: 13, color: AppColors.textMuted),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
