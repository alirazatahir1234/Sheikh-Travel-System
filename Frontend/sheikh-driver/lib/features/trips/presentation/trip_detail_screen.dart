import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../domain/trip_model.dart';
import 'trips_notifier.dart';
import 'widgets/trip_map_preview.dart';

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
        final trip = trips.where((t) => t.matchesRouteId(tripId)).firstOrNull;
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
    if (_busy) return;
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
              ?.where((t) => t.matchesRouteId(trip.routeId))
              .firstOrNull;
          final paymentRequired = updated?.paymentRequired ?? false;
          // Payment API resolves trip or booking id; keep routeId for consistency.
          final targetId = updated?.routeId ?? trip.routeId;
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
        'Accept' || 'Start' => 'Trip accepted. Navigate to pickup.',
        'Arrived' => 'Arrived at pickup. Notify passenger to board.',
        'Onboard' => 'Passenger onboard. Navigate to drop-off.',
        'Complete' => 'Trip completed successfully.',
        'Reject' => 'Trip declined.',
        _ => '$action successful',
      };

  Future<void> _confirmReject(Trip latest) async {
    if (_busy || !latest.canReject) return;
    final reason = await showSgTextEntrySheet(
      context,
      title: 'Reason for rejection',
      hint: 'Why are you rejecting this trip?',
      textCapitalization: TextCapitalization.sentences,
      maxLines: 3,
    );
    if (reason != null && reason.isNotEmpty && mounted) {
      await _run('Reject', reason: reason);
    }
  }

  /// Opens in-app Google Maps navigation (Routes polyline + turn-by-turn CTA).
  Future<void> _openNavigate(Trip latest) async {
    if (_busy) return;
    context.push('/trips/${latest.routeId}/navigate');
  }

  @override
  Widget build(BuildContext context) {
    final latest = ref.watch(tripsProvider).valueOrNull
            ?.where((t) => t.matchesRouteId(trip.routeId))
            .firstOrNull ??
        trip;

    final status = latest.lifecycleStatusName.isNotEmpty
        ? latest.lifecycleStatusName
        : latest.statusName;
    final pickup = latest.pickupAddress?.isNotEmpty == true
        ? latest.pickupAddress!
        : latest.routeName;
    final drop = latest.dropoffAddress?.isNotEmpty == true
        ? latest.dropoffAddress!
        : 'Drop-off';

    Widget? primaryCta;
    if (!latest.isCompleted && !latest.isCancelled) {
      if (latest.canAccept) {
        primaryCta = SgPrimaryButton(
          label: 'Accept trip',
          loading: _busy,
          onPressed: _busy ? null : () => _run('Accept'),
        );
      } else if (latest.canArrive) {
        primaryCta = SgPrimaryButton(
          label: 'Arrived at pickup',
          icon: Icons.place_outlined,
          loading: _busy,
          onPressed: _busy ? null : () => _run('Arrived'),
        );
      } else if (latest.canOnboard) {
        primaryCta = SgPrimaryButton(
          label: 'Start trip',
          icon: Icons.play_arrow_rounded,
          loading: _busy,
          onPressed: _busy ? null : () => _run('Onboard'),
        );
      } else if (latest.canComplete) {
        primaryCta = SgPrimaryButton(
          label: 'Complete trip',
          icon: Icons.check_circle_outline,
          loading: _busy,
          onPressed: _busy ? null : () => _run('Complete'),
        );
      }
    }

    final showNavigate = !latest.isCompleted &&
        !latest.isCancelled &&
        latest.canNavigate;
    final showReject = !latest.isCompleted &&
        !latest.isCancelled &&
        latest.canReject;
    final showSticky = _busy ||
        primaryCta != null ||
        showNavigate ||
        showReject;

    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(title: const Text('Trip details')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
        children: [
          SgCard(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        latest.bookingNumber,
                        style: Theme.of(context).textTheme.titleLarge?.copyWith(
                              color: context.sg.textPrimary,
                              fontWeight: FontWeight.w800,
                            ),
                      ),
                    ),
                    StatusBadge(status),
                  ],
                ),
                const SizedBox(height: 4),
                Text(
                  latest.scheduledDateLabel,
                  style: TextStyle(
                    color: context.sg.textPrimary,
                    fontWeight: FontWeight.w600,
                    fontSize: 14,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  latest.source.isNotEmpty ? latest.source : 'Transfer',
                  style: TextStyle(
                    color: context.sg.textSecondary,
                    fontWeight: FontWeight.w500,
                    fontSize: 13,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 10),
          SgCard(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Column(
              children: [
                _TimelineStop(
                  title: 'Pickup',
                  subtitle: pickup,
                  time: latest.pickupDateTimeLabel,
                  timeEmphasis: latest.isPickupOverdue,
                  color: AppColors.success,
                  isFirst: true,
                ),
                _TimelineStop(
                  title: 'Drop-off',
                  subtitle: drop,
                  time: latest.dropoffDateTimeLabel,
                  color: AppColors.error,
                  isLast: true,
                ),
              ],
            ),
          ),
          const SizedBox(height: 10),
          SgCard(
            padding: const EdgeInsets.all(AppSpacing.md),
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
                _InfoGridRow('Source', latest.source, isLast: true),
              ],
            ),
          ),
          const SizedBox(height: 10),
          TripMapPreviewCard(
            trip: latest,
            onTap: showNavigate && !_busy
                ? () => _openNavigate(latest)
                : null,
          ),
          if (latest.isCompleted) ...[
            const SizedBox(height: 16),
            Text(
              'This trip is already completed.',
              textAlign: TextAlign.center,
              style: TextStyle(color: context.sg.textSecondary),
            ),
          ],
        ],
      ),
      bottomNavigationBar: showSticky
          ? SgStickyActionBar(
              child: _busy && primaryCta == null
                  ? const Center(
                      child: SizedBox(
                        height: 28,
                        width: 28,
                        child: CircularProgressIndicator(strokeWidth: 2.5),
                      ),
                    )
                  : Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        if (primaryCta != null) primaryCta,
                        if (showNavigate) ...[
                          if (primaryCta != null) const SizedBox(height: 8),
                          OutlinedButton.icon(
                            onPressed:
                                _busy ? null : () => _openNavigate(latest),
                            icon: const Icon(Icons.near_me_rounded),
                            label: const Text('Navigate'),
                          ),
                        ],
                        if (showReject) ...[
                          if (primaryCta != null || showNavigate)
                            const SizedBox(height: 8),
                          SgDangerOutlineButton(
                            label: 'Reject trip',
                            onPressed:
                                _busy ? null : () => _confirmReject(latest),
                          ),
                        ],
                      ],
                    ),
            )
          : null,
    );
  }
}

class _TimelineStop extends StatelessWidget {
  const _TimelineStop({
    required this.title,
    required this.subtitle,
    required this.time,
    required this.color,
    this.timeEmphasis = false,
    this.isFirst = false,
    this.isLast = false,
  });

  final String title;
  final String subtitle;
  final String time;
  final Color color;
  final bool timeEmphasis;
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
                top: isFirst ? 0 : 8,
                bottom: isLast ? 0 : 8,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: context.sg.textSecondary,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: context.sg.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    time,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: timeEmphasis
                          ? AppColors.warning
                          : context.sg.textSecondary,
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
  const _InfoGridRow(this.label, this.value, {this.isLast = false});
  final String label;
  final String value;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(top: 6, bottom: isLast ? 0 : 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 88,
            child: Text(
              label,
              style: TextStyle(
                color: context.sg.textSecondary,
                fontSize: 13,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              textAlign: TextAlign.right,
              style: TextStyle(
                color: context.sg.textPrimary,
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
