import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../../alerts/domain/gps_alert_models.dart';
import '../../../fleet/domain/fleet_models.dart';
import '../../../ops_trips/domain/ops_trip_models.dart';
import '../../domain/dashboard_layout.dart';
import '../../domain/dashboard_models.dart';
import '../../domain/dashboard_role.dart';

String dashboardGreeting() {
  final h = DateTime.now().hour;
  if (h < 12) return 'Good Morning';
  if (h < 17) return 'Good Afternoon';
  return 'Good Evening';
}

Color _colorKey(String key) => switch (key) {
      'success' => AppColors.success,
      'warning' => AppColors.warning,
      'error' => AppColors.error,
      'info' => AppColors.info,
      'accent' => AppColors.accent,
      _ => AppColors.primary,
    };

IconData _iconName(String name) => switch (name) {
      'route' => Icons.route_rounded,
      'fingerprint' => Icons.fingerprint,
      'fuel' => Icons.local_gas_station_rounded,
      'inspect' => Icons.fact_check_rounded,
      'fleet' => Icons.local_shipping_rounded,
      'map' => Icons.map_outlined,
      'alert' => Icons.warning_amber_rounded,
      'ai' => Icons.auto_awesome_rounded,
      'build' => Icons.build_rounded,
      'drivers' => Icons.groups_outlined,
      'docs' => Icons.folder_outlined,
      'reports' => Icons.bar_chart_rounded,
      'more' => Icons.apps_outlined,
      'bookings' => Icons.event_note_outlined,
      _ => Icons.apps_rounded,
    };

Color _severityColor(String severity) {
  switch (severity.toLowerCase()) {
    case 'critical':
    case 'error':
      return AppColors.error;
    case 'warning':
      return AppColors.warning;
    case 'success':
      return AppColors.success;
    default:
      return AppColors.info;
  }
}

String _relativeTime(DateTime at) {
  final local = at.toLocal();
  final diff = DateTime.now().difference(local);
  if (diff.inSeconds < 60) return 'just now';
  if (diff.inMinutes < 60) return '${diff.inMinutes} min ago';
  if (diff.inHours < 24) return '${diff.inHours}h ago';
  return DateFormat('dd MMM').format(local);
}

String _money(double v) {
  if (v >= 1000000) return 'PKR ${(v / 1000000).toStringAsFixed(1)}M';
  if (v >= 1000) {
    return 'PKR ${NumberFormat('#,##0').format(v.round())}';
  }
  return 'PKR ${v.toStringAsFixed(0)}';
}

/// Ops command-center header: greeting, role · tenant, date, last sync.
class OpsHeaderCard extends StatelessWidget {
  const OpsHeaderCard({
    super.key,
    required this.name,
    required this.role,
    this.tenantId,
    this.lastSyncedAt,
  });

  final String name;
  final DashboardRole role;
  final int? tenantId;
  final DateTime? lastSyncedAt;

  @override
  Widget build(BuildContext context) {
    final dateLabel = DateFormat('d MMMM yyyy').format(DateTime.now());
    final syncLabel = lastSyncedAt == null
        ? '—'
        : DateFormat('h:mm a').format(lastSyncedAt!.toLocal());
    final isCommand = role == DashboardRole.fleetManager ||
        role == DashboardRole.gpsOperator ||
        role == DashboardRole.tenantAdmin ||
        role == DashboardRole.superAdmin ||
        role == DashboardRole.dispatcher;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                isCommand ? 'Fleet' : '${dashboardGreeting()}, $name',
                style: const TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w800,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                isCommand
                    ? role.subtitle
                    : '${role.commandLabel} · $dateLabel',
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textSecondary,
                ),
              ),
              if (!isCommand) ...[
                const SizedBox(height: 4),
                Text(
                  'Synced $syncLabel',
                  style: const TextStyle(
                    fontSize: 11,
                    color: AppColors.textMuted,
                  ),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class DashboardGreetingCard extends StatelessWidget {
  const DashboardGreetingCard({
    super.key,
    required this.name,
    required this.role,
  });

  final String name;
  final DashboardRole role;

  @override
  Widget build(BuildContext context) {
    return OpsHeaderCard(name: name, role: role);
  }
}

class PlatformBannerCard extends StatelessWidget {
  const PlatformBannerCard({super.key});

  @override
  Widget build(BuildContext context) {
    return const SgCard(
      child: Row(
        children: [
          Icon(Icons.admin_panel_settings_outlined, color: AppColors.primary),
          SizedBox(width: 10),
          Expanded(
            child: Text(
              'Platform admin — showing this tenant’s operational view. Multi-tenant KPIs come in a later release.',
              style: TextStyle(fontSize: 12, color: AppColors.textSecondary),
            ),
          ),
        ],
      ),
    );
  }
}

/// Shared shell primary KPI strip (role-specific cells from API).
class PrimaryKpiStrip extends StatelessWidget {
  const PrimaryKpiStrip({super.key, required this.cells});
  final List<KpiCell> cells;

  @override
  Widget build(BuildContext context) {
    if (cells.isEmpty) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Overview'),
        const SizedBox(height: 8),
        LayoutBuilder(
          builder: (context, constraints) {
            final wide = constraints.maxWidth >= 520;
            final children = [
              for (final c in cells)
                _PrimaryKpiTile(cell: c),
            ];
            if (wide && cells.length <= 4) {
              return Row(
                children: [
                  for (var i = 0; i < children.length; i++) ...[
                    if (i > 0) const SizedBox(width: 8),
                    Expanded(child: children[i]),
                  ],
                ],
              );
            }
            return GridView.count(
              crossAxisCount: wide ? 4 : 2,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              mainAxisSpacing: 8,
              crossAxisSpacing: 8,
              childAspectRatio: 1.55,
              children: children,
            );
          },
        ),
      ],
    );
  }
}

class _PrimaryKpiTile extends StatelessWidget {
  const _PrimaryKpiTile({required this.cell});
  final KpiCell cell;

  @override
  Widget build(BuildContext context) {
    final color = _colorKey(cell.colorKey);
    return SgCard(
      onTap: cell.route == null ? null : () => context.push(cell.route!),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            cell.label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w600,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            cell.value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: color,
            ),
          ),
        ],
      ),
    );
  }
}

class FleetHealthCard extends StatelessWidget {
  const FleetHealthCard({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    final pct = data.fleetHealthPercent;
    final gps = data.gps;
    final fleet = data.fleet;
    final maint = data.maintenance;
    final total = fleet?.totalVehicles ?? gps?.totalVehicles ?? 0;
    final active = fleet?.activeVehicles ?? gps?.online ?? 0;
    final offline = gps?.offline ?? 0;
    final maintenance =
        fleet?.maintenanceDue ?? maint?.dueForService ?? 0;
    final updated = data.lastSyncedAt == null
        ? '—'
        : _relativeTime(data.lastSyncedAt!);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Fleet Health'),
        const SizedBox(height: 8),
        SgCard(
          child: Column(
            children: [
              Row(
                children: [
                  SizedBox(
                    width: 96,
                    height: 96,
                    child: Stack(
                      alignment: Alignment.center,
                      children: [
                        SizedBox(
                          width: 96,
                          height: 96,
                          child: CircularProgressIndicator(
                            value: (pct / 100).clamp(0.0, 1.0),
                            strokeWidth: 8,
                            backgroundColor: AppColors.border,
                            color: pct >= 80
                                ? AppColors.success
                                : pct >= 50
                                    ? AppColors.warning
                                    : AppColors.error,
                          ),
                        ),
                        Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              '${pct.round()}%',
                              style: const TextStyle(
                                fontSize: 20,
                                fontWeight: FontWeight.w800,
                                color: AppColors.textPrimary,
                              ),
                            ),
                            Text(
                              data.healthLabel,
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                                color: pct >= 80
                                    ? AppColors.success
                                    : pct >= 50
                                        ? AppColors.warning
                                        : AppColors.error,
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      children: [
                        _HealthStatRow(
                          icon: Icons.local_shipping_outlined,
                          color: AppColors.primary,
                          label: 'Total Vehicles',
                          value: '$total',
                        ),
                        _HealthStatRow(
                          icon: Icons.person_outline,
                          color: AppColors.success,
                          label: 'Active',
                          value: '$active',
                        ),
                        _HealthStatRow(
                          icon: Icons.signal_wifi_off_outlined,
                          color: AppColors.error,
                          label: 'Offline',
                          value: '$offline',
                        ),
                        _HealthStatRow(
                          icon: Icons.build_outlined,
                          color: AppColors.warning,
                          label: 'Maintenance',
                          value: '$maintenance',
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              if (data.healthSummary != null &&
                  data.healthSummary!.isNotEmpty) ...[
                const SizedBox(height: 12),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: AppColors.primary.withValues(alpha: 0.06),
                    borderRadius: BorderRadius.circular(AppRadii.md),
                  ),
                  child: Text(
                    data.healthSummary!,
                    style: const TextStyle(
                      fontSize: 12,
                      height: 1.35,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 10),
              Row(
                children: [
                  const Icon(Icons.refresh_rounded,
                      size: 14, color: AppColors.textSecondary),
                  const SizedBox(width: 4),
                  Text(
                    'Updated $updated',
                    style: const TextStyle(
                      fontSize: 11,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _HealthStatRow extends StatelessWidget {
  const _HealthStatRow({
    required this.icon,
    required this.color,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final Color color;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          Icon(icon, size: 16, color: color),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              label,
              style: const TextStyle(
                fontSize: 12,
                color: AppColors.textSecondary,
              ),
            ),
          ),
          Text(
            value,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
        ],
      ),
    );
  }
}

/// Compact non-interactive Google Map snapshot for the home feed.
class LiveMapPreviewCard extends StatefulWidget {
  const LiveMapPreviewCard({super.key, required this.positions});
  final List<GpsPosition> positions;

  @override
  State<LiveMapPreviewCard> createState() => _LiveMapPreviewCardState();
}

class _LiveMapPreviewCardState extends State<LiveMapPreviewCard> {
  GoogleMapController? _map;
  bool _fitted = false;

  static const _defaultCamera = CameraPosition(
    target: LatLng(25.3463, 55.4209), // Sharjah-ish default
    zoom: 11,
  );

  @override
  void dispose() {
    _map?.dispose();
    super.dispose();
  }

  Set<Marker> _markers() {
    return widget.positions
        .where((p) => p.latitude != 0 || p.longitude != 0)
        .map(
          (p) => Marker(
            markerId: MarkerId('v${p.vehicleId}'),
            position: LatLng(p.latitude, p.longitude),
            rotation: p.heading ?? 0,
            icon: BitmapDescriptor.defaultMarkerWithHue(
              p.speed > 5
                  ? BitmapDescriptor.hueGreen
                  : (p.alarmType != null && p.alarmType!.isNotEmpty)
                      ? BitmapDescriptor.hueRed
                      : BitmapDescriptor.hueAzure,
            ),
            infoWindow: InfoWindow(
              title: 'Vehicle ${p.vehicleId}',
              snippet: '${p.speed.toStringAsFixed(0)} km/h',
            ),
          ),
        )
        .toSet();
  }

  Future<void> _fit() async {
    final map = _map;
    if (map == null || _fitted) return;
    final pts = widget.positions
        .where((p) => p.latitude != 0 || p.longitude != 0)
        .map((p) => LatLng(p.latitude, p.longitude))
        .toList();
    if (pts.isEmpty) return;
    if (pts.length == 1) {
      await map.animateCamera(CameraUpdate.newLatLngZoom(pts.first, 13));
    } else {
      var minLat = pts.first.latitude;
      var maxLat = pts.first.latitude;
      var minLng = pts.first.longitude;
      var maxLng = pts.first.longitude;
      for (final p in pts) {
        minLat = minLat < p.latitude ? minLat : p.latitude;
        maxLat = maxLat > p.latitude ? maxLat : p.latitude;
        minLng = minLng < p.longitude ? minLng : p.longitude;
        maxLng = maxLng > p.longitude ? maxLng : p.longitude;
      }
      await map.animateCamera(
        CameraUpdate.newLatLngBounds(
          LatLngBounds(
            southwest: LatLng(minLat, minLng),
            northeast: LatLng(maxLat, maxLng),
          ),
          40,
        ),
      );
    }
    _fitted = true;
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Expanded(child: SgSectionTitle('Live Fleet Map')),
            TextButton(
              onPressed: () => context.push('/fleet/map'),
              child: const Text('View full map'),
            ),
          ],
        ),
        const SizedBox(height: 4),
        SgCard(
          padding: EdgeInsets.zero,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: SizedBox(
              height: 200,
              child: Stack(
                children: [
                  IgnorePointer(
                    child: GoogleMap(
                      initialCameraPosition: _defaultCamera,
                      markers: _markers(),
                      myLocationButtonEnabled: false,
                      zoomControlsEnabled: false,
                      mapToolbarEnabled: false,
                      scrollGesturesEnabled: false,
                      zoomGesturesEnabled: false,
                      tiltGesturesEnabled: false,
                      rotateGesturesEnabled: false,
                      liteModeEnabled:
                          defaultTargetPlatform == TargetPlatform.android,
                      onMapCreated: (c) {
                        _map = c;
                        _fit();
                      },
                    ),
                  ),
                  Positioned.fill(
                    child: Material(
                      color: Colors.transparent,
                      child: InkWell(
                        onTap: () => context.push('/fleet/map'),
                      ),
                    ),
                  ),
                  Positioned(
                    right: 10,
                    bottom: 10,
                    child: Material(
                      color: Colors.white,
                      elevation: 2,
                      borderRadius: BorderRadius.circular(8),
                      child: IconButton(
                        tooltip: 'Open full map',
                        icon: const Icon(Icons.fullscreen_rounded, size: 20),
                        onPressed: () => context.push('/fleet/map'),
                      ),
                    ),
                  ),
                  if (widget.positions.isEmpty)
                    const Positioned.fill(
                      child: ColoredBox(
                        color: Color(0xAAFFFFFF),
                        child: Center(
                          child: Text(
                            'No live positions yet',
                            style: TextStyle(color: AppColors.textSecondary),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class AiCopilotSummaryCard extends StatelessWidget {
  const AiCopilotSummaryCard({
    super.key,
    required this.items,
    required this.canOpenAi,
  });

  final List<AiAttentionItem> items;
  final bool canOpenAi;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) return const SizedBox.shrink();
    final prompts = items
        .map((e) => e.suggestedPrompt)
        .whereType<String>()
        .where((s) => s.isNotEmpty)
        .toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Icon(Icons.auto_awesome_rounded,
                size: 18, color: AppColors.primary),
            const SizedBox(width: 6),
            const Expanded(child: SgSectionTitle('AI Copilot Summary')),
            if (canOpenAi)
              TextButton(
                onPressed: () => context.push('/ai'),
                child: const Text('Ask AI'),
              ),
          ],
        ),
        const SizedBox(height: 4),
        SgCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                "Today's Summary",
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: 10),
              ...items.map(
                (item) => Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Container(
                        width: 8,
                        height: 8,
                        margin: const EdgeInsets.only(top: 5),
                        decoration: BoxDecoration(
                          color: _severityColor(item.severity),
                          shape: BoxShape.circle,
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          item.text,
                          style: const TextStyle(
                            fontSize: 13,
                            color: AppColors.textPrimary,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              if (canOpenAi) ...[
                const SizedBox(height: 6),
                SgPrimaryButton(
                  label: 'Open AI Copilot',
                  icon: Icons.auto_awesome_rounded,
                  onPressed: () {
                    if (prompts.isNotEmpty) {
                      context.push(
                        '/ai?q=${Uri.encodeComponent(prompts.first)}',
                      );
                    } else {
                      context.push('/ai');
                    }
                  },
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class CriticalAlertsCard extends StatelessWidget {
  const CriticalAlertsCard({
    super.key,
    required this.events,
    this.criticalCount = 0,
  });

  final List<GpsAlertEvent> events;
  final int criticalCount;

  @override
  Widget build(BuildContext context) {
    final badge = criticalCount > 0 ? criticalCount : events.length;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Expanded(child: SgSectionTitle('Critical Alerts')),
            if (badge > 0)
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                decoration: BoxDecoration(
                  color: AppColors.error,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  '$badge',
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
          ],
        ),
        const SizedBox(height: 8),
        SgCard(
          child: events.isEmpty
              ? const Text(
                  'No open critical alerts',
                  style: TextStyle(color: AppColors.textSecondary),
                )
              : Column(
                  children: [
                    for (var i = 0; i < events.length; i++) ...[
                      if (i > 0) const Divider(height: 16),
                      _AlertRow(event: events[i]),
                    ],
                    const SizedBox(height: 8),
                    Align(
                      alignment: Alignment.centerLeft,
                      child: TextButton(
                        onPressed: () => context.push('/alerts'),
                        child: const Text('View all alerts'),
                      ),
                    ),
                  ],
                ),
        ),
      ],
    );
  }
}

class _AlertRow extends StatelessWidget {
  const _AlertRow({required this.event});
  final GpsAlertEvent event;

  @override
  Widget build(BuildContext context) {
    final color = _severityColor(event.severity);
    final detail = [
      if (event.vehicleName != null && event.vehicleName!.isNotEmpty)
        event.vehicleName!
      else
        'VH-${event.vehicleId}',
      if (event.speed > 0) '${event.speed.toStringAsFixed(0)} km/h',
      if (event.geofenceName != null) event.geofenceName!,
      if (event.batteryHint != null) event.batteryHint!,
    ].join(' · ');

    return InkWell(
      onTap: () => context.push('/alerts'),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 4,
            height: 40,
            decoration: BoxDecoration(
              color: color,
              borderRadius: BorderRadius.circular(2),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  event.eventType.isEmpty ? 'Alert' : event.eventType,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  detail,
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          Text(
            _relativeTime(event.timestamp),
            style: const TextStyle(
              fontSize: 11,
              color: AppColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}

extension on GpsAlertEvent {
  String? get batteryHint {
    final m = message.toLowerCase();
    if (m.contains('battery')) {
      final match = RegExp(r'(\d+)\s*%').firstMatch(message);
      if (match != null) return '${match.group(1)}%';
    }
    return null;
  }
}

class TodayOpsKpiRow extends StatelessWidget {
  const TodayOpsKpiRow({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    final trips = data.trips;
    final fuel = data.fuelAnalytics;
    final maint = data.maintenance;
    final present = data.attendancePresent;
    final total = data.attendanceTotal;
    final absent = (total - present).clamp(0, 999);
    final eff = fuel?.efficiencyKmPerL;

    final cards = [
      _MiniKpi(
        title: "Today's Trips",
        value: '${trips?.total ?? 0}',
        detail:
            '${trips?.completed ?? 0} Completed · ${trips?.inProgress ?? 0} In Progress',
        onTap: () => context.push('/trips'),
      ),
      _MiniKpi(
        title: 'Fuel (Today)',
        value: _money(fuel?.todayCost ?? 0),
        detail: [
          if ((fuel?.todayLiters ?? 0) > 0)
            '${fuel!.todayLiters.toStringAsFixed(0)} Ltr',
          if (eff != null) '${eff.toStringAsFixed(1)} km/L',
        ].join(' · ').ifEmpty('No fuel logged today'),
        onTap: () => context.push('/fuel'),
      ),
      _MiniKpi(
        title: 'Maintenance',
        value: '${maint?.activeWorkOrders ?? maint?.dueForService ?? 0}',
        detail:
            '${maint?.dueForService ?? 0} Due · ${maint?.overdueServices ?? 0} Overdue',
        onTap: () => context.push('/more/maintenance'),
      ),
      _MiniKpi(
        title: 'Driver Attendance',
        value: total > 0 ? '$present/$total' : '$present',
        detail: absent > 0 ? 'Present · $absent Absent' : 'Present',
        onTap: () => context.push('/more/drivers'),
      ),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle("Today's KPIs"),
        const SizedBox(height: 8),
        LayoutBuilder(
          builder: (context, constraints) {
            final wide = constraints.maxWidth >= 520;
            if (wide) {
              return Row(
                children: [
                  for (var i = 0; i < cards.length; i++) ...[
                    if (i > 0) const SizedBox(width: 10),
                    Expanded(child: cards[i]),
                  ],
                ],
              );
            }
            return GridView.count(
              crossAxisCount: 2,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              mainAxisSpacing: 10,
              crossAxisSpacing: 10,
              childAspectRatio: 1.35,
              children: cards,
            );
          },
        ),
      ],
    );
  }
}

extension on String {
  String ifEmpty(String fallback) => isEmpty ? fallback : this;
}

class _MiniKpi extends StatelessWidget {
  const _MiniKpi({
    required this.title,
    required this.value,
    required this.detail,
    required this.onTap,
  });

  final String title;
  final String value;
  final String detail;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SgCard(
      onTap: onTap,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            value,
            style: const TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w800,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            detail,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 11,
              color: AppColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}

class RecentActivitiesCard extends StatelessWidget {
  const RecentActivitiesCard({super.key, required this.items});
  final List<ActivityItem> items;

  IconData _icon(String kind) => switch (kind) {
        'alert' => Icons.warning_amber_rounded,
        'trip' => Icons.check_circle_outline,
        'maintenance' => Icons.build_outlined,
        _ => Icons.notifications_none_rounded,
      };

  Color _color(String kind) => switch (kind) {
        'alert' => AppColors.error,
        'trip' => AppColors.success,
        'maintenance' => AppColors.warning,
        _ => AppColors.info,
      };

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Recent Activities'),
        const SizedBox(height: 8),
        SgCard(
          child: items.isEmpty
              ? const Text(
                  'No recent activity',
                  style: TextStyle(color: AppColors.textSecondary),
                )
              : Column(
                  children: [
                    for (var i = 0; i < items.length; i++) ...[
                      if (i > 0) const Divider(height: 18),
                      InkWell(
                        onTap: items[i].route == null
                            ? null
                            : () => context.push(items[i].route!),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              DateFormat('hh:mm a')
                                  .format(items[i].at.toLocal()),
                              style: const TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                                color: AppColors.textSecondary,
                              ),
                            ),
                            const SizedBox(width: 10),
                            Icon(
                              _icon(items[i].kind),
                              size: 18,
                              color: _color(items[i].kind),
                            ),
                            const SizedBox(width: 10),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    items[i].title,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.w700,
                                      color: AppColors.textPrimary,
                                    ),
                                  ),
                                  if (items[i].subtitle.isNotEmpty)
                                    Text(
                                      items[i].subtitle,
                                      maxLines: 2,
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(
                                        fontSize: 12,
                                        color: AppColors.textSecondary,
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
        ),
      ],
    );
  }
}

class AiAttentionCard extends StatelessWidget {
  const AiAttentionCard({
    super.key,
    required this.items,
    required this.canOpenAi,
  });

  final List<AiAttentionItem> items;
  final bool canOpenAi;

  @override
  Widget build(BuildContext context) {
    return AiCopilotSummaryCard(items: items, canOpenAi: canOpenAi);
  }
}

class FleetHealthHeader extends StatelessWidget {
  const FleetHealthHeader({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) => FleetHealthCard(data: data);
}

class LiveFleetCard extends StatelessWidget {
  const LiveFleetCard({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    return LiveMapPreviewCard(positions: data.livePositions);
  }
}

class KpiStrip extends StatelessWidget {
  const KpiStrip({super.key, required this.items});
  final List<(String, String, Color)> items;

  @override
  Widget build(BuildContext context) {
    return SgCard(
      child: Row(
        children: [
          for (var i = 0; i < items.length; i++) ...[
            if (i > 0) const SizedBox(width: 8),
            Expanded(
              child: Column(
                children: [
                  Text(
                    items[i].$2,
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w800,
                      color: items[i].$3,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    items[i].$1,
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      fontSize: 11,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class MyVehicleCard extends StatelessWidget {
  const MyVehicleCard({super.key, required this.driver});
  final DriverDashboardSummary driver;

  @override
  Widget build(BuildContext context) {
    final name = driver.currentVehicle ?? 'No vehicle assigned';
    final plate = driver.currentVehiclePlate;
    return SgCard(
      child: Row(
        children: [
          const Icon(Icons.directions_car_filled_outlined,
              color: AppColors.primary),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                if (plate != null && plate.isNotEmpty)
                  Text(
                    plate,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
              ],
            ),
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
            decoration: BoxDecoration(
              color: AppColors.success.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Text(
              driver.driverStatus.isNotEmpty ? driver.driverStatus : 'Active',
              style: const TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w600,
                color: AppColors.success,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class EarningsCard extends StatelessWidget {
  const EarningsCard({super.key, required this.driver});
  final DriverDashboardSummary driver;

  @override
  Widget build(BuildContext context) {
    return SgCard(
      onTap: () => context.push('/earnings'),
      child: Row(
        children: [
          const Icon(Icons.payments_outlined, color: AppColors.success),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Earnings this week',
                  style: TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
                Text(
                  _money(driver.earningsThisWeek),
                  style: const TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.w800,
                    color: AppColors.textPrimary,
                  ),
                ),
              ],
            ),
          ),
          const Icon(Icons.chevron_right_rounded,
              color: AppColors.textSecondary),
        ],
      ),
    );
  }
}

class MaintenanceKpisCard extends StatelessWidget {
  const MaintenanceKpisCard({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    final m = data.maintenance;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Maintenance'),
        const SizedBox(height: 8),
        KpiStrip(items: [
          ('Upcoming', '${m?.dueForService ?? 0}', AppColors.warning),
          ('Overdue', '${m?.overdueServices ?? 0}', AppColors.error),
          ('In Progress', '${m?.underMaintenance ?? 0}', AppColors.info),
          ('WOs', '${m?.activeWorkOrders ?? 0}', AppColors.primary),
        ]),
      ],
    );
  }
}

class MoneySummaryCard extends StatelessWidget {
  const MoneySummaryCard({
    super.key,
    required this.title,
    required this.amount,
    required this.subtitle,
    required this.icon,
    required this.route,
  });

  final String title;
  final double amount;
  final String subtitle;
  final IconData icon;
  final String route;

  @override
  Widget build(BuildContext context) {
    return SgCard(
      onTap: () => context.push(route),
      child: Row(
        children: [
          Icon(icon, color: AppColors.primary),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
                Text(
                  _money(amount),
                  style: const TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.w800,
                    color: AppColors.textPrimary,
                  ),
                ),
                Text(
                  subtitle,
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class FuelAnalyticsCard extends StatelessWidget {
  const FuelAnalyticsCard({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    final fuel = data.fuelAnalytics;
    final monthly = fuel?.totalCost ?? data.fleet?.monthlyFuelCost ?? 0;
    final eff = fuel?.efficiencyKmPerL;
    return SgCard(
      onTap: () => context.push('/more/reports'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Monthly Fuel',
            style: TextStyle(
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            _money(monthly),
            style: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w800,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: Text(
                  'Today\n${_money(fuel?.todayCost ?? 0)}',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ),
              Expanded(
                child: Text(
                  'Efficiency\n${eff == null ? '—' : '${eff.toStringAsFixed(1)} km/L'}',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ),
              Expanded(
                child: Text(
                  'Liters\n${(fuel?.totalLiters ?? 0).toStringAsFixed(0)} L',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class AlertsPreviewCard extends StatelessWidget {
  const AlertsPreviewCard({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    return CriticalAlertsCard(
      events: data.alertEvents,
      criticalCount: data.alerts?.critical ?? 0,
    );
  }
}

class LiveTripsPreviewCard extends StatelessWidget {
  const LiveTripsPreviewCard({super.key, required this.trips});
  final List<OpsTripListItem> trips;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Live trips'),
        const SizedBox(height: 8),
        if (trips.isEmpty)
          const SgCard(
            child: Text(
              'No live trips right now',
              style: TextStyle(color: AppColors.textSecondary),
            ),
          )
        else
          ...trips.map(
            (t) => Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: SgCard(
                onTap: () => context.push('/trips/${t.id}'),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      t.tripNumber,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      '${t.driverName ?? 'No driver'} · ${t.vehicleName ?? 'No vehicle'}',
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      t.routeName == null
                          ? t.status
                          : '${t.status} · ${t.routeName}',
                      style: const TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: AppColors.primary,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}

class PendingAssignmentsCard extends StatelessWidget {
  const PendingAssignmentsCard({super.key, required this.trips});
  final List<OpsTripListItem> trips;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Pending assignments'),
        const SizedBox(height: 8),
        if (trips.isEmpty)
          const SgCard(
            child: Text(
              'All scheduled trips have driver and vehicle',
              style: TextStyle(color: AppColors.textSecondary),
            ),
          )
        else
          ...trips.map(
            (t) => Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: SgCard(
                onTap: () => context.push('/trips/${t.id}'),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      t.tripNumber,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      [
                        if (t.driverId == null) 'Needs driver',
                        if (t.vehicleId == null) 'Needs vehicle',
                      ].join(' · '),
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.warning,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}

class ComplianceDocsCard extends StatelessWidget {
  const ComplianceDocsCard({super.key, required this.data});
  final RoleDashboardData data;

  @override
  Widget build(BuildContext context) {
    final c = data.compliance;
    final s = data.driverStats;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Documents & licenses'),
        const SizedBox(height: 8),
        KpiStrip(items: [
          ('Expired docs', '${c?.expired ?? 0}', AppColors.error),
          ('Due 7d', '${c?.expiring7Days ?? 0}', AppColors.warning),
          ('Lic. soon', '${s?.licensesExpiringSoon ?? 0}', AppColors.warning),
          ('Lic. expired', '${s?.licensesExpired ?? 0}', AppColors.error),
        ]),
      ],
    );
  }
}

class QuickActionsGrid extends StatelessWidget {
  const QuickActionsGrid({super.key, required this.actions});
  final List<DashboardQuickAction> actions;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Quick Actions'),
        const SizedBox(height: 8),
        SizedBox(
          height: 88,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: actions.length,
            separatorBuilder: (_, __) => const SizedBox(width: 10),
            itemBuilder: (context, i) {
              final a = actions[i];
              final color = _colorKey(a.colorKey);
              return InkWell(
                onTap: () => context.push(a.route),
                borderRadius: BorderRadius.circular(14),
                child: SizedBox(
                  width: 72,
                  child: Column(
                    children: [
                      Container(
                        width: 48,
                        height: 48,
                        decoration: BoxDecoration(
                          color: color.withValues(alpha: 0.12),
                          borderRadius: BorderRadius.circular(14),
                        ),
                        child: Icon(_iconName(a.iconName), color: color),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        a.label,
                        textAlign: TextAlign.center,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                      ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}

class SectionErrorHint extends StatelessWidget {
  const SectionErrorHint({super.key, required this.message});
  final String message;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Text(
        message,
        style: const TextStyle(fontSize: 12, color: AppColors.warning),
      ),
    );
  }
}
