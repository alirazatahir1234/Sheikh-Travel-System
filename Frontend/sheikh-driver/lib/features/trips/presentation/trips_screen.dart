import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../domain/trip_model.dart';
import 'trips_notifier.dart';

class TripsScreen extends ConsumerStatefulWidget {
  const TripsScreen({super.key});

  @override
  ConsumerState<TripsScreen> createState() => _TripsScreenState();
}

class _TripsScreenState extends ConsumerState<TripsScreen> {
  final _searchCtrl = TextEditingController();
  String _query = '';

  @override
  void initState() {
    super.initState();
    // Dashboard and trips use separate providers — refresh when opening this tab
    // so newly assigned trips appear without requiring a full app restart.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(tripsProvider.notifier).refresh();
    });
  }

  @override
  void dispose() {
    _searchCtrl.dispose();
    super.dispose();
  }

  List<Trip> _filter(List<Trip> trips) {
    final q = _query.trim().toLowerCase();
    if (q.isEmpty) return trips;
    return trips
        .where((t) =>
            t.bookingNumber.toLowerCase().contains(q) ||
            t.customerName.toLowerCase().contains(q) ||
            t.routeName.toLowerCase().contains(q) ||
            (t.pickupAddress ?? '').toLowerCase().contains(q) ||
            (t.dropoffAddress ?? '').toLowerCase().contains(q) ||
            t.statusName.toLowerCase().contains(q) ||
            t.lifecycleStatusName.toLowerCase().contains(q))
        .toList();
  }

  @override
  Widget build(BuildContext context) {
    final tripsAsync = ref.watch(tripsProvider);

    final p = context.sg;
    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(
        title: const Text('My Trips'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => ref.read(tripsProvider.notifier).refresh(),
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
            child: SgSearchField(
              controller: _searchCtrl,
              hintText: 'Search trips',
              onChanged: (v) => setState(() => _query = v),
            ),
          ),
          Expanded(
            child: tripsAsync.when(
              loading: () => ListView(
                padding: const EdgeInsets.all(16),
                children: const [
                  SgSkeleton(height: 110),
                  SizedBox(height: 12),
                  SgSkeleton(height: 110),
                  SizedBox(height: 12),
                  SgSkeleton(height: 110),
                ],
              ),
              error: (e, _) => SgErrorView(
                message: e.toString(),
                onRetry: () => ref.read(tripsProvider.notifier).refresh(),
              ),
              data: (trips) {
                final filtered = _filter(trips);
                if (filtered.isEmpty) {
                  return SgEmptyState(
                    icon: Icons.route_outlined,
                    title:
                        _query.isEmpty ? 'No trips yet' : 'No matching trips',
                    subtitle: _query.isEmpty
                        ? 'Assigned trips will show up here when dispatch sends them.'
                        : 'Try a different search.',
                    actionLabel: _query.isEmpty ? 'Refresh' : null,
                    onAction: _query.isEmpty
                        ? () => ref.read(tripsProvider.notifier).refresh()
                        : null,
                  );
                }
                return RefreshIndicator(
                  color: AppColors.primary,
                  onRefresh: () => ref.read(tripsProvider.notifier).refresh(),
                  child: ListView.separated(
                    padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
                    itemCount: filtered.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 10),
                    itemBuilder: (_, i) => _TripCard(trip: filtered[i]),
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

class _TripCard extends StatelessWidget {
  const _TripCard({required this.trip});
  final Trip trip;

  @override
  Widget build(BuildContext context) {
    final timeFmt = DateFormat('hh:mm a');
    final dateFmt = DateFormat('dd MMM');
    final status = trip.lifecycleStatusName.isNotEmpty
        ? trip.lifecycleStatusName
        : trip.statusName;
    final pickup = trip.pickupAddress?.isNotEmpty == true
        ? trip.pickupAddress!
        : trip.routeName;
    final drop = trip.dropoffAddress?.isNotEmpty == true
        ? trip.dropoffAddress!
        : trip.customerName;

    return SgCard(
      onTap: () => context.go('/trips/${trip.routeId}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  trip.bookingNumber,
                  style: TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 15,
                    color: context.sg.textPrimary,
                  ),
                ),
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(
                    timeFmt.format(trip.pickupTime.toLocal()),
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 13,
                      color: context.sg.textPrimary,
                    ),
                  ),
                  Text(
                    dateFmt.format(trip.pickupTime.toLocal()),
                    style: TextStyle(
                      fontSize: 11,
                      color: context.sg.textSecondary,
                    ),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 8),
          StatusBadge(status),
          const SizedBox(height: 10),
          Text(
            trip.source.isNotEmpty ? trip.source : 'Transfer',
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: context.sg.textPrimary,
            ),
          ),
          const SizedBox(height: 8),
          _LocRow(
              icon: Icons.radio_button_checked,
              text: pickup,
              color: AppColors.success),
          Padding(
            padding: const EdgeInsets.only(left: 7),
            child: Container(
              width: 2,
              height: 10,
              color: context.sg.divider,
            ),
          ),
          _LocRow(icon: Icons.location_on, text: drop, color: AppColors.error),
        ],
      ),
    );
  }
}

class _LocRow extends StatelessWidget {
  const _LocRow({
    required this.icon,
    required this.text,
    required this.color,
  });
  final IconData icon;
  final String text;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, size: 14, color: color),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 13,
              color: context.sg.textSecondary,
            ),
          ),
        ),
      ],
    );
  }
}
