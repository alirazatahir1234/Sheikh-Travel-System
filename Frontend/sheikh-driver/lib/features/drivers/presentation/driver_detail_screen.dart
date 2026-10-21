import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../features/bookings/presentation/dispatch_pickers.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/drivers_api.dart';
import '../domain/driver_models.dart';
import 'drivers_notifier.dart';

class DriverDetailScreen extends ConsumerWidget {
  const DriverDetailScreen({super.key, required this.driverId});
  final int driverId;

  bool _isVerified(DriverDetail d) =>
      (d.verificationStatus ?? '').toLowerCase() == 'verified';

  Future<void> _assignVehicle(
    BuildContext context,
    WidgetRef ref,
    DriverDetail driver,
  ) async {
    if (!_isVerified(driver)) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Driver must be verified before vehicle assignment.',
            ),
          ),
        );
      }
      return;
    }
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
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(ErrorHandler.message(e))),
        );
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
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(ErrorHandler.message(e))),
        );
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
    final p = context.sg;

    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(
        title: const Text('Driver'),
        actions: [
          if (canManage)
            PopupMenuButton<String>(
              tooltip: 'Update status',
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
                      SnackBar(content: Text(ErrorHandler.message(e))),
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
        loading: () => ListView(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
          children: const [
            SgSkeleton(height: 120, radius: AppRadii.lg),
            SizedBox(height: 14),
            SgSkeleton(height: 24, width: 160, radius: AppRadii.sm),
            SizedBox(height: 8),
            SgSkeleton(height: 140, radius: AppRadii.lg),
            SizedBox(height: 14),
            SgSkeleton(height: 24, width: 120, radius: AppRadii.sm),
            SizedBox(height: 8),
            SgSkeleton(height: 160, radius: AppRadii.lg),
          ],
        ),
        error: (e, _) => SgErrorView(
          message: '$e',
          onRetry: () => ref.invalidate(driverDetailProvider(driverId)),
        ),
        data: (d) {
          final df = DateFormat('dd MMM yyyy');
          final tf = DateFormat('dd MMM, HH:mm');
          final displayName =
              d.fullName.trim().isEmpty ? 'Driver' : d.fullName.trim();
          final verified = _isVerified(d);
          final licenseColor = d.licenseExpired
              ? AppColors.error
              : d.licenseExpiringSoon
                  ? AppColors.warning
                  : null;
          return ListView(
            padding: EdgeInsets.fromLTRB(
              16,
              8,
              16,
              28 + MediaQuery.of(context).padding.bottom,
            ),
            children: [
              SgCard(
                padding: const EdgeInsets.all(18),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        SgAvatar(label: displayName, size: 56),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                displayName,
                                style: Theme.of(context)
                                    .textTheme
                                    .titleLarge
                                    ?.copyWith(
                                      fontWeight: FontWeight.w800,
                                      letterSpacing: -0.3,
                                      color: p.textPrimary,
                                    ),
                              ),
                              if (d.driverCode != null) ...[
                                const SizedBox(height: 2),
                                Text(
                                  d.driverCode!,
                                  style: TextStyle(
                                    fontSize: 13,
                                    fontWeight: FontWeight.w500,
                                    color: p.textMuted,
                                  ),
                                ),
                              ],
                              const SizedBox(height: 8),
                              Wrap(
                                spacing: 8,
                                runSpacing: 6,
                                children: [
                                  StatusBadge(d.status),
                                  if (d.verificationStatus != null &&
                                      d.verificationStatus!.isNotEmpty)
                                    StatusBadge(
                                      d.verificationStatus!,
                                      color: verified
                                          ? AppColors.success
                                          : AppColors.warning,
                                    ),
                                  if (d.gpsOnline)
                                    const StatusBadge(
                                      'GPS online',
                                      color: AppColors.success,
                                    ),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),
                    Divider(height: 1, color: p.divider),
                    const SizedBox(height: 12),
                    _ContactLine(
                      icon: Icons.phone_outlined,
                      value: d.phone,
                    ),
                    if (d.email != null && d.email!.isNotEmpty) ...[
                      const SizedBox(height: 8),
                      _ContactLine(
                        icon: Icons.mail_outline_rounded,
                        value: d.email!,
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 18),
              const SgSectionTitle(
                'License & compliance',
                subtitle: 'Documents and verification',
              ),
              const SizedBox(height: 8),
              SgCard(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
                child: Column(
                  children: [
                    SgDetailRow('License', d.licenseNumber, showDivider: true),
                    SgDetailRow(
                      'Expiry',
                      d.licenseExpiryDate != null
                          ? df.format(d.licenseExpiryDate!.toLocal())
                          : '—',
                      showDivider: true,
                    ),
                    SgDetailRow(
                      'License status',
                      d.licenseExpired
                          ? 'Expired'
                          : d.licenseExpiringSoon
                              ? 'Expiring soon'
                              : 'Valid',
                      valueColor: licenseColor,
                      showDivider: true,
                    ),
                    SgDetailRow(
                      'Verification',
                      d.verificationStatus ?? '—',
                      showDivider: true,
                    ),
                    SgDetailRow('CNIC', d.cnic ?? '—'),
                  ],
                ),
              ),
              const SizedBox(height: 18),
              const SgSectionTitle(
                'Assignment',
                subtitle: 'Vehicle, branch, availability',
              ),
              const SizedBox(height: 8),
              SgCard(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
                onTap: d.assignedVehicleId != null
                    ? () => context
                        .push('/fleet/vehicles/${d.assignedVehicleId}')
                    : null,
                child: Column(
                  children: [
                    SgDetailRow(
                      'Vehicle',
                      d.assignedVehicleName ??
                          d.assignedVehicleRegistration ??
                          'Unassigned',
                      showDivider: true,
                    ),
                    SgDetailRow(
                      'Plate',
                      d.assignedVehicleRegistration ?? '—',
                      showDivider: true,
                    ),
                    SgDetailRow(
                      'Branch',
                      d.branchName ?? '—',
                      showDivider: true,
                    ),
                    SgDetailRow(
                      'GPS',
                      d.gpsOnline ? 'Online' : 'Offline',
                      valueColor:
                          d.gpsOnline ? AppColors.success : p.textMuted,
                      showDivider: true,
                    ),
                    SgDetailRow(
                      'Availability',
                      d.availabilityBucket ?? '—',
                      showDivider: true,
                    ),
                    SgDetailRow(
                      'Rating',
                      d.rating != null ? d.rating!.toStringAsFixed(1) : '—',
                    ),
                  ],
                ),
              ),
              if (canAssign) ...[
                const SizedBox(height: 12),
                if (!verified)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: Container(
                      width: double.infinity,
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 10,
                      ),
                      decoration: BoxDecoration(
                        color: AppColors.warning.withValues(alpha: 0.10),
                        borderRadius: BorderRadius.circular(AppRadii.md),
                        border: Border.all(
                          color: AppColors.warning.withValues(alpha: 0.28),
                        ),
                      ),
                      child: const Text(
                        'Vehicle assignment requires Verified status.',
                        style: TextStyle(
                          fontSize: 12,
                          color: AppColors.warning,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ),
                Row(
                  children: [
                    Expanded(
                      child: FilledButton.icon(
                        onPressed: () => _assignVehicle(context, ref, d),
                        icon: const Icon(Icons.local_shipping_outlined),
                        label: Text(
                          d.assignedVehicleId != null ? 'Reassign' : 'Assign',
                        ),
                      ),
                    ),
                    if (d.assignedVehicleId != null) ...[
                      const SizedBox(width: 10),
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
                const SizedBox(height: 18),
                const SgSectionTitle('Performance'),
                const SizedBox(height: 8),
                _PerformanceCard(driverId: driverId),
                const SizedBox(height: 18),
                const SgSectionTitle('Violations'),
                const SizedBox(height: 8),
                _ViolationsCard(driverId: driverId, df: df),
                const SizedBox(height: 18),
                const SgSectionTitle('Attendance (30 days)'),
                const SizedBox(height: 8),
                _AttendanceCard(driverId: driverId, tf: tf),
              ],
              const SizedBox(height: 18),
              const SgSectionTitle('Documents'),
              const SizedBox(height: 8),
              _DocumentsCard(driverId: driverId, df: df),
              const SizedBox(height: 18),
              const SgSectionTitle('Profile'),
              const SizedBox(height: 8),
              SgCard(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
                child: Column(
                  children: [
                    SgDetailRow('Address', d.address ?? '—', showDivider: true),
                    SgDetailRow(
                      'Hire date',
                      d.hireDate != null
                          ? df.format(d.hireDate!.toLocal())
                          : '—',
                      showDivider: true,
                    ),
                    SgDetailRow(
                      'Experience',
                      d.yearsExperience != null
                          ? '${d.yearsExperience} yrs'
                          : '—',
                      showDivider: true,
                    ),
                    SgDetailRow(
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

class _ContactLine extends StatelessWidget {
  const _ContactLine({required this.icon, required this.value});
  final IconData icon;
  final String value;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Row(
      children: [
        Icon(icon, size: 16, color: p.textMuted),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            value,
            style: TextStyle(
              fontWeight: FontWeight.w600,
              fontSize: 14,
              color: p.textPrimary,
            ),
          ),
        ),
      ],
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
      error: (_, __) => const SgCard(
        child: SgInlineEmpty(message: 'Performance unavailable'),
      ),
      data: (p) {
        if (p == null) {
          return const SgCard(
            child: SgInlineEmpty(message: 'Performance unavailable'),
          );
        }
        return SgCard(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
          child: Column(
            children: [
              SgDetailRow(
                'Trips',
                '${p.completedTrips} / ${p.totalTrips}',
                showDivider: true,
              ),
              SgDetailRow(
                'Completion',
                '${p.completionRate.toStringAsFixed(0)}%',
                showDivider: true,
              ),
              SgDetailRow(
                'Revenue',
                p.totalRevenue.toStringAsFixed(0),
                showDivider: true,
              ),
              SgDetailRow(
                'Violations',
                '${p.violationCount}',
                showDivider: true,
              ),
              SgDetailRow(
                'Present days',
                '${p.attendancePresentCount}',
                showDivider: true,
              ),
              SgDetailRow(
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
    final palette = context.sg;
    return async.when(
      loading: () => const SgCard(
        child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
      ),
      error: (_, __) => const SgCard(
        child: SgInlineEmpty(message: 'No violations loaded'),
      ),
      data: (items) {
        if (items.isEmpty) {
          return const SgCard(
            child: SgInlineEmpty(
              message: 'No violations on record',
              icon: Icons.check_circle_outline_rounded,
            ),
          );
        }
        final visible = items.take(8).toList();
        return SgCard(
          padding: const EdgeInsets.fromLTRB(14, 6, 14, 6),
          child: Column(
            children: [
              for (var i = 0; i < visible.length; i++) ...[
                if (i > 0) Divider(height: 1, color: palette.divider),
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 10),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const SgIconWell(
                        icon: Icons.report_outlined,
                        color: AppColors.error,
                        size: 34,
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              visible[i].violationType,
                              style: TextStyle(
                                fontWeight: FontWeight.w600,
                                color: palette.textPrimary,
                              ),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              [
                                visible[i].severity,
                                df.format(visible[i].occurredAt.toLocal()),
                                visible[i].status,
                                if (visible[i].description != null &&
                                    visible[i].description!.isNotEmpty)
                                  visible[i].description!,
                              ].join(' · '),
                              style: TextStyle(
                                fontSize: 12,
                                height: 1.35,
                                color: palette.textSecondary,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ],
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
    final palette = context.sg;
    return async.when(
      loading: () => const SgCard(
        child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
      ),
      error: (_, __) => const SgCard(
        child: SgInlineEmpty(message: 'No attendance loaded'),
      ),
      data: (items) {
        if (items.isEmpty) {
          return const SgCard(
            child: SgInlineEmpty(message: 'No attendance records'),
          );
        }
        final visible = items.take(10).toList();
        return SgCard(
          padding: const EdgeInsets.fromLTRB(14, 6, 14, 6),
          child: Column(
            children: [
              for (var i = 0; i < visible.length; i++) ...[
                if (i > 0) Divider(height: 1, color: palette.divider),
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 10),
                  child: Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              DateFormat('dd MMM yyyy')
                                  .format(visible[i].attendanceDate.toLocal()),
                              style: TextStyle(
                                fontWeight: FontWeight.w600,
                                color: palette.textPrimary,
                              ),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              [
                                visible[i].status,
                                if (visible[i].checkInAt != null)
                                  'in ${tf.format(visible[i].checkInAt!.toLocal())}',
                                if (visible[i].checkOutAt != null)
                                  'out ${tf.format(visible[i].checkOutAt!.toLocal())}',
                              ].join(' · '),
                              style: TextStyle(
                                fontSize: 12,
                                color: palette.textSecondary,
                              ),
                            ),
                          ],
                        ),
                      ),
                      StatusBadge(visible[i].status),
                    ],
                  ),
                ),
              ],
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
    final palette = context.sg;
    return async.when(
      loading: () => const SgCard(
        child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
      ),
      error: (_, __) => const SgCard(
        child: SgInlineEmpty(message: 'No documents loaded'),
      ),
      data: (items) {
        if (items.isEmpty) {
          return const SgCard(
            child: SgInlineEmpty(message: 'No documents on file'),
          );
        }
        return SgCard(
          padding: const EdgeInsets.fromLTRB(14, 6, 14, 6),
          child: Column(
            children: [
              for (var i = 0; i < items.length; i++) ...[
                if (i > 0) Divider(height: 1, color: palette.divider),
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 10),
                  child: Row(
                    children: [
                      SgIconWell(
                        icon: Icons.description_outlined,
                        color: items[i].isExpired
                            ? AppColors.error
                            : items[i].isExpiringSoon
                                ? AppColors.warning
                                : AppColors.primary,
                        size: 34,
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              items[i].documentType,
                              style: TextStyle(
                                fontWeight: FontWeight.w600,
                                color: palette.textPrimary,
                              ),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              [
                                items[i].status,
                                if (items[i].expiryDate != null)
                                  'exp ${df.format(items[i].expiryDate!.toLocal())}',
                                if (items[i].isExpired)
                                  'EXPIRED'
                                else if (items[i].isExpiringSoon)
                                  'expiring soon',
                              ].join(' · '),
                              style: TextStyle(
                                fontSize: 12,
                                color: items[i].isExpired
                                    ? AppColors.error
                                    : items[i].isExpiringSoon
                                        ? AppColors.warning
                                        : palette.textSecondary,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ],
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
