import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../domain/trip_model.dart';
import 'trips_notifier.dart';

class TripDetailScreen extends ConsumerWidget {
  const TripDetailScreen({super.key, required this.tripId});
  final int tripId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripsAsync = ref.watch(tripsProvider);
    return tripsAsync.when(
      loading: () =>
          const Scaffold(body: Center(child: CircularProgressIndicator())),
      error: (e, _) => Scaffold(body: Center(child: Text(e.toString()))),
      data: (trips) {
        final trip = trips
            .where((t) => t.id == tripId || t.bookingId == tripId)
            .firstOrNull;
        if (trip == null) {
          return const Scaffold(body: Center(child: Text('Trip not found')));
        }
        return _TripDetailContent(trip: trip);
      },
    );
  }
}

class _TripDetailContent extends ConsumerStatefulWidget {
  const _TripDetailContent({required this.trip});
  final Trip trip;

  @override
  ConsumerState<_TripDetailContent> createState() => _TripDetailContentState();
}

class _TripDetailContentState extends ConsumerState<_TripDetailContent> {
  bool _busy = false;

  Trip get trip => widget.trip;

  Future<void> _run(String action, {String? reason}) async {
    setState(() => _busy = true);
    try {
      final offlineMsg = await ref.read(tripsProvider.notifier).advance(
            trip.actionId,
            action,
            reason: reason,
          );
      if (mounted) {
        final message = offlineMsg ?? _successMessage(action);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(message),
            backgroundColor:
                offlineMsg != null ? AppColors.warning : AppColors.success,
          ),
        );
        if (action == 'Complete' && offlineMsg == null) {
          final updated = ref.read(tripsProvider).valueOrNull
              ?.where((t) => t.id == trip.id || t.bookingId == trip.id)
              .firstOrNull;
          final paymentRequired = updated?.paymentRequired ?? false;
          final targetId = updated?.actionId ?? trip.actionId;
          if (paymentRequired) {
            context.push('/trips/$targetId/collect-payment');
          }
        }
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(ErrorHandler.message(e)),
            backgroundColor: AppColors.error,
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  String _successMessage(String action) => switch (action) {
        'Accept' => 'Trip accepted. Navigate to pickup.',
        'Arrived' => 'Arrived at pickup. Notify passenger to board.',
        'Onboard' => 'Passenger onboard. Navigate to drop-off.',
        'Complete' => 'Trip completed successfully.',
        'Reject' => 'Trip declined.',
        _ => '$action successful',
      };

  Future<void> _confirmReject(Trip latest) async {
    final ctrl = TextEditingController();
    final reason = await showDialog<String>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Reject Trip'),
        content: TextField(
          controller: ctrl,
          decoration:
              const InputDecoration(labelText: 'Reason for rejection'),
          autofocus: true,
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.pop(context, ctrl.text),
            style: FilledButton.styleFrom(backgroundColor: AppColors.error),
            child: const Text('Reject'),
          ),
        ],
      ),
    );
    ctrl.dispose();
    if (reason != null && reason.isNotEmpty && mounted) {
      await _run('Reject', reason: reason);
    }
  }

  @override
  Widget build(BuildContext context) {
    final latest = ref.watch(tripsProvider).valueOrNull
            ?.where((t) => t.id == trip.id || t.bookingId == trip.id)
            .firstOrNull ??
        trip;

    final timeFmt = DateFormat('hh:mm a');
    final status = latest.lifecycleStatusName.isNotEmpty
        ? latest.lifecycleStatusName
        : latest.statusName;
    final pickup = latest.pickupAddress?.isNotEmpty == true
        ? latest.pickupAddress!
        : latest.routeName;
    final drop = latest.dropoffAddress?.isNotEmpty == true
        ? latest.dropoffAddress!
        : 'Drop-off';

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Trip Details'),
        actions: [
          IconButton(
            icon: const Icon(Icons.more_vert_rounded),
            onPressed: () {},
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
        children: [
          SgCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        latest.bookingNumber,
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                          color: AppColors.textPrimary,
                        ),
                      ),
                    ),
                    StatusBadge(status),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  latest.source.isNotEmpty ? latest.source : 'Transfer',
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 14),
          SgCard(
            child: Column(
              children: [
                _TimelineStop(
                  title: 'Pickup',
                  subtitle: pickup,
                  time: timeFmt.format(latest.pickupTime.toLocal()),
                  color: AppColors.success,
                  isFirst: true,
                ),
                _TimelineStop(
                  title: 'Drop-off',
                  subtitle: drop,
                  time: latest.dropoffTime != null
                      ? timeFmt.format(latest.dropoffTime!.toLocal())
                      : '—',
                  color: AppColors.error,
                  isLast: true,
                ),
              ],
            ),
          ),
          const SizedBox(height: 14),
          SgCard(
            child: Column(
              children: [
                _InfoGridRow('Customer', latest.customerName),
                _InfoGridRow(
                  'Vehicle',
                  latest.vehicleName ?? 'Not assigned',
                ),
                _InfoGridRow(
                  'Fare',
                  'PKR ${NumberFormat('#,##0.00').format(latest.totalAmount)}',
                ),
                _InfoGridRow('Source', latest.source),
              ],
            ),
          ),
          const SizedBox(height: 24),
          if (_busy)
            const Center(child: CircularProgressIndicator())
          else ...[
            if (latest.canAccept) ...[
              SgPrimaryButton(
                label: 'Accept Trip',
                onPressed: () => _run('Accept'),
              ),
              const SizedBox(height: 10),
            ],
            if (latest.canReject) ...[
              SgDangerOutlineButton(
                label: 'Reject Trip',
                onPressed: () => _confirmReject(latest),
              ),
              const SizedBox(height: 10),
            ],
            if (latest.canNavigate)
              OutlinedButton.icon(
                onPressed: () => context.push('/trips/${latest.id}/navigate'),
                icon: const Icon(Icons.near_me_rounded),
                label: const Text('NAVIGATE'),
              ),
            if (latest.canArrive) ...[
              const SizedBox(height: 10),
              SgPrimaryButton(
                label: 'Arrived at Pickup',
                icon: Icons.place_outlined,
                onPressed: () => _run('Arrived'),
              ),
            ],
            if (latest.canOnboard) ...[
              const SizedBox(height: 10),
              SgPrimaryButton(
                label: 'Passenger Onboard',
                icon: Icons.airline_seat_recline_normal,
                onPressed: () => _run('Onboard'),
              ),
            ],
            if (latest.isCompleted) ...[
              const SizedBox(height: 10),
              Text(
                'This trip is already completed.',
                textAlign: TextAlign.center,
                style: TextStyle(color: AppColors.textSecondary.withValues(alpha: 0.95)),
              ),
            ] else if (latest.canComplete) ...[
              const SizedBox(height: 10),
              SgPrimaryButton(
                label: 'Complete Trip',
                icon: Icons.check_circle_outline,
                onPressed: () => _run('Complete'),
              ),
            ],
          ],
        ],
      ),
    );
  }
}

class _TimelineStop extends StatelessWidget {
  const _TimelineStop({
    required this.title,
    required this.subtitle,
    required this.time,
    required this.color,
    this.isFirst = false,
    this.isLast = false,
  });

  final String title;
  final String subtitle;
  final String time;
  final Color color;
  final bool isFirst;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 24,
            child: Column(
              children: [
                if (!isFirst)
                  Expanded(
                    child: Container(width: 2, color: AppColors.divider),
                  )
                else
                  const SizedBox(height: 4),
                Container(
                  width: 12,
                  height: 12,
                  decoration: BoxDecoration(
                    color: color,
                    shape: BoxShape.circle,
                    border: Border.all(color: Colors.white, width: 2),
                    boxShadow: [
                      BoxShadow(
                        color: color.withValues(alpha: 0.35),
                        blurRadius: 4,
                      ),
                    ],
                  ),
                ),
                if (!isLast)
                  Expanded(
                    child: Container(width: 2, color: AppColors.divider),
                  )
                else
                  const SizedBox(height: 4),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(
                top: isFirst ? 0 : 12,
                bottom: isLast ? 0 : 12,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          title,
                          style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ),
                      Text(
                        time,
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w700,
                          color: AppColors.textPrimary,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textPrimary,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _InfoGridRow extends StatelessWidget {
  const _InfoGridRow(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          SizedBox(
            width: 100,
            child: Text(
              label,
              style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 13,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              textAlign: TextAlign.right,
              style: const TextStyle(
                color: AppColors.textPrimary,
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
