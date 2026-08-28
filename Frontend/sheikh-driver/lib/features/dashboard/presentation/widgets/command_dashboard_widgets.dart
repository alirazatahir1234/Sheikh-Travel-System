import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../data/dashboard_api.dart';
import '../../domain/dashboard_models.dart';
import '../../domain/dashboard_role.dart';
import 'dashboard_widgets.dart';

/// Role-specific 4-KPI strip for Command Dashboard.
class FleetStatsStrip extends StatelessWidget {
  const FleetStatsStrip({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    return KpiStrip(items: _itemsForRole());
  }

  List<(String, String, Color)> _itemsForRole() {
    switch (data.role) {
      case DashboardRole.dispatcher:
        final availDrivers = data.driverStats?.available ?? 0;
        final availVehicles =
            data.gps?.online ?? data.fleet?.activeVehicles ?? 0;
        final delayed = data.trips?.delayed ?? 0;
        return [
          ('Trips', '${data.trips?.total ?? 0}', AppColors.primary),
          ('Drivers', '$availDrivers', AppColors.success),
          ('Available', '$availVehicles', AppColors.info),
          ('Delayed', '$delayed', AppColors.error),
        ];
      case DashboardRole.driverManager:
        final s = data.driverStats;
        return [
          ('Drivers', '${s?.totalDrivers ?? 0}', AppColors.primary),
          ('Active', '${s?.active ?? 0}', AppColors.success),
          ('On trip', '${s?.onTrip ?? 0}', AppColors.info),
          ('Lic. soon', '${s?.licensesExpiringSoon ?? 0}', AppColors.warning),
        ];
      case DashboardRole.tenantAdmin:
      case DashboardRole.superAdmin:
        return [
          (
            'Vehicles',
            '${data.fleet?.totalVehicles ?? data.gps?.totalVehicles ?? 0}',
            AppColors.primary
          ),
          (
            'Drivers',
            '${data.driverStats?.totalDrivers ?? data.fleet?.driversOnDuty ?? 0}',
            AppColors.info
          ),
          ('Trips', '${data.trips?.total ?? 0}', AppColors.success),
          (
            'Fuel',
            _shortMoney(
              data.fuelAnalytics?.todayCost ?? data.fleet?.monthlyFuelCost ?? 0,
            ),
            AppColors.warning
          ),
        ];
      case DashboardRole.fleetManager:
      default:
        return [
          (
            'Vehicles',
            '${data.fleet?.totalVehicles ?? data.gps?.totalVehicles ?? 0}',
            AppColors.primary
          ),
          (
            'Drivers',
            '${data.driverStats?.totalDrivers ?? data.fleet?.driversOnDuty ?? 0}',
            AppColors.info
          ),
          ('Trips', '${data.trips?.total ?? 0}', AppColors.success),
          (
            'Maint.',
            '${data.fleet?.maintenanceDue ?? data.maintenance?.dueForService ?? 0}',
            AppColors.warning
          ),
        ];
    }
  }

  String _shortMoney(double v) {
    if (v >= 1000) return '${(v / 1000).toStringAsFixed(0)}k';
    return v.toStringAsFixed(0);
  }
}

/// 2×2 ops status grid (On Duty / Trips / Alerts / Maintenance).
class OpsKpiGridCard extends StatelessWidget {
  const OpsKpiGridCard({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    final cards = _cards(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Today at a glance'),
        const SizedBox(height: 8),
        ChunkedGrid(
          crossAxisCount: 2,
          mainAxisSpacing: 10,
          crossAxisSpacing: 10,
          children: cards,
        ),
      ],
    );
  }

  List<Widget> _cards(BuildContext context) {
    switch (data.role) {
      case DashboardRole.dispatcher:
        return [
          _gridTile(
            context,
            title: 'Live trips',
            value: '${data.trips?.inProgress ?? data.liveTrips.length}',
            detail: '${data.pendingTrips.length} pending assign',
            color: AppColors.info,
            route: '/trips',
          ),
          _gridTile(
            context,
            title: 'Available drivers',
            value: '${data.driverStats?.available ?? 0}',
            detail: 'Ready to assign',
            color: AppColors.success,
            route: '/more/drivers',
          ),
          _gridTile(
            context,
            title: 'Alerts',
            value: '${data.alerts?.active ?? data.alertEvents.length}',
            detail: 'View all →',
            color: AppColors.error,
            route: '/alerts',
          ),
          _gridTile(
            context,
            title: 'Delayed',
            value: '${data.trips?.delayed ?? 0}',
            detail: 'Needs attention',
            color: AppColors.warning,
            route: '/trips',
          ),
        ];
      default:
        final onDuty =
            data.driverStats?.active ?? data.fleet?.driversOnDuty ?? 0;
        return [
          _gridTile(
            context,
            title: 'On Duty',
            value: '$onDuty Drivers',
            detail: data.attendanceTotal > 0
                ? '${((onDuty / data.attendanceTotal) * 100).round()}%'
                : 'Active now',
            color: AppColors.info,
            route: '/more/drivers',
          ),
          _gridTile(
            context,
            title: 'Trips Today',
            value: '${data.trips?.total ?? 0} Total',
            detail: '${data.trips?.completed ?? 0} done',
            color: AppColors.primary,
            route: '/trips',
          ),
          _gridTile(
            context,
            title: 'Alerts',
            value: '${data.alerts?.active ?? data.alertEvents.length} Active',
            detail: 'View all →',
            color: AppColors.error,
            route: '/alerts',
          ),
          _gridTile(
            context,
            title: 'Maintenance',
            value:
                '${data.maintenance?.dueForService ?? data.fleet?.maintenanceDue ?? 0} Due',
            detail: 'View all →',
            color: AppColors.warning,
            route: '/more/maintenance',
          ),
        ];
    }
  }

  Widget _gridTile(
    BuildContext context, {
    required String title,
    required String value,
    required String detail,
    required Color color,
    required String route,
  }) {
    return SgCard(
      padding: const EdgeInsets.all(12),
      onTap: () => context.push(route),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: AppColors.textMuted,
            ),
          ),
          const Spacer(),
          Text(
            value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: color,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            detail,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontSize: 11, color: AppColors.textSecondary),
          ),
        ],
      ),
    );
  }
}

class MapSummaryCard extends StatelessWidget {
  const MapSummaryCard({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    final total = data.gps?.totalVehicles ?? data.fleet?.totalVehicles ?? 0;
    final online = data.gps?.online ?? data.fleet?.activeVehicles ?? 0;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Live fleet'),
        const SizedBox(height: 8),
        SgCard(
          onTap: () => context.push('/fleet/map'),
          child: Row(
            children: [
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadii.md),
                ),
                child: const Icon(Icons.map_rounded, color: AppColors.primary),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '$total Vehicles',
                      style: const TextStyle(
                        fontWeight: FontWeight.w800,
                        fontSize: 16,
                      ),
                    ),
                    Text(
                      '$online online · tap to open map',
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right_rounded,
                  color: AppColors.textMuted),
            ],
          ),
        ),
      ],
    );
  }
}

class UniversalSearchBarCard extends StatelessWidget {
  const UniversalSearchBarCard({super.key});

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(AppRadii.xl),
      elevation: 0,
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadii.xl),
        onTap: () => showUniversalSearchSheet(context),
        child: Container(
          padding:
              const EdgeInsets.symmetric(horizontal: 16, vertical: 13),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.xl),
            border: Border.all(color: AppColors.border),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.04),
                blurRadius: 8,
                offset: const Offset(0, 2),
              ),
            ],
          ),
          child: Row(
            children: [
              const Icon(Icons.search_rounded,
                  color: AppColors.textMuted, size: 20),
              const SizedBox(width: 10),
              const Expanded(
                child: Text(
                  'Search vehicle, driver, trip, booking, or location...',
                  style:
                      TextStyle(color: AppColors.textMuted, fontSize: 14),
                ),
              ),
              Container(
                padding: const EdgeInsets.all(6),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: AppColors.border),
                ),
                child: const Icon(Icons.tune_rounded,
                    size: 16, color: AppColors.textSecondary),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

Future<void> showUniversalSearchSheet(BuildContext context) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (ctx) => const _UniversalSearchSheet(),
  );
}

class _UniversalSearchSheet extends ConsumerStatefulWidget {
  const _UniversalSearchSheet();

  @override
  ConsumerState<_UniversalSearchSheet> createState() =>
      _UniversalSearchSheetState();
}

class _UniversalSearchSheetState extends ConsumerState<_UniversalSearchSheet> {
  final _ctrl = TextEditingController();
  List<DashboardSearchHit> _hits = const [];
  bool _loading = false;
  Timer? _debounce;

  @override
  void dispose() {
    _debounce?.cancel();
    _ctrl.dispose();
    super.dispose();
  }

  void _onChanged(String q) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 320), () async {
      if (!mounted) return;
      setState(() => _loading = true);
      try {
        final hits = await ref.read(dashboardApiProvider).searchUniversal(q);
        if (!mounted) return;
        setState(() {
          _hits = hits;
          _loading = false;
        });
      } catch (_) {
        if (!mounted) return;
        setState(() {
          _hits = const [];
          _loading = false;
        });
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: SizedBox(
        height: MediaQuery.sizeOf(context).height * 0.72,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
              child: TextField(
                controller: _ctrl,
                autofocus: true,
                onChanged: _onChanged,
                decoration: InputDecoration(
                  hintText: 'Plate, driver, trip, booking…',
                  prefixIcon: const Icon(Icons.search_rounded),
                  filled: true,
                  fillColor: AppColors.surface,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadii.md),
                    borderSide: BorderSide.none,
                  ),
                ),
              ),
            ),
            if (_loading) const LinearProgressIndicator(minHeight: 2),
            Expanded(
              child: _hits.isEmpty
                  ? Center(
                      child: Text(
                        _ctrl.text.trim().length < 2
                            ? 'Type at least 2 characters'
                            : 'No matches',
                        style: const TextStyle(color: AppColors.textSecondary),
                      ),
                    )
                  : ListView.separated(
                      itemCount: _hits.length,
                      separatorBuilder: (_, __) => const Divider(height: 1),
                      itemBuilder: (_, i) {
                        final h = _hits[i];
                        return ListTile(
                          leading: Icon(_iconFor(h.kind)),
                          title: Text(h.title),
                          subtitle: Text('${h.kind} · ${h.subtitle}'),
                          onTap: () {
                            Navigator.pop(context);
                            context.push(h.route);
                          },
                        );
                      },
                    ),
            ),
          ],
        ),
      ),
    );
  }

  IconData _iconFor(String kind) => switch (kind) {
        'driver' => Icons.badge_outlined,
        'trip' => Icons.route_outlined,
        'booking' => Icons.event_note_outlined,
        _ => Icons.local_shipping_outlined,
      };
}

class AttentionVehiclesCard extends StatelessWidget {
  const AttentionVehiclesCard({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    final items = data.attentionVehicles;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Expanded(
                child: SgSectionTitle('Vehicles Needing Attention')),
            TextButton(
              onPressed: () => context.push('/fleet'),
              style: TextButton.styleFrom(
                foregroundColor: AppColors.primary,
                padding:
                    const EdgeInsets.symmetric(horizontal: 8, vertical: 0),
              ),
              child: const Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('View fleet',
                      style: TextStyle(
                          fontSize: 12, fontWeight: FontWeight.w600)),
                  SizedBox(width: 2),
                  Icon(Icons.chevron_right_rounded, size: 16),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        if (items.isEmpty)
          SgCard(
            padding:
                const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
            child: const Text(
              'No vehicles flagged right now',
              style: TextStyle(color: AppColors.textSecondary),
            ),
          )
        else
          ChunkedGrid(
            crossAxisCount: 2,
            mainAxisSpacing: 8,
            crossAxisSpacing: 8,
            children: items
                .map(
                  (v) => SgCard(
                    padding: const EdgeInsets.symmetric(
                        horizontal: 12, vertical: 10),
                    onTap: () =>
                        context.push('/fleet/vehicles/${v.id}'),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Expanded(
                              child: Text(
                                v.name,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  fontSize: 13,
                                  fontWeight: FontWeight.w700,
                                  color: AppColors.textPrimary,
                                ),
                              ),
                            ),
                            const Icon(Icons.chevron_right_rounded,
                                size: 14, color: AppColors.textMuted),
                          ],
                        ),
                        const SizedBox(height: 3),
                        Text(
                          v.registrationNumber,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            fontSize: 11,
                            color: AppColors.textSecondary,
                          ),
                        ),
                        const SizedBox(height: 6),
                        StatusBadge(v.status),
                      ],
                    ),
                  ),
                )
                .toList(),
          ),
      ],
    );
  }
}
