import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../../alerts/domain/gps_alert_models.dart';
import '../../../fleet/domain/fleet_models.dart';
import '../../../fleet/domain/fleet_status.dart';
import '../../../fleet/presentation/widgets/vehicle_pulse_icons.dart';
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
    final dateLabel = DateFormat('MMM d, yyyy').format(DateTime.now());

    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Dashboard',
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w800,
                  color: AppColors.textPrimary,
                  height: 1.1,
                ),
              ),
              const SizedBox(height: 3),
              RichText(
                text: TextSpan(
                  style: const TextStyle(
                    fontSize: 13,
                    color: AppColors.textSecondary,
                  ),
                  children: [
                    const TextSpan(text: 'Welcome back, '),
                    TextSpan(
                      text: name,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        color: AppColors.primary,
                      ),
                    ),
                    const TextSpan(text: ' 👋'),
                  ],
                ),
              ),
            ],
          ),
        ),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadii.md),
            border: Border.all(color: AppColors.border),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.calendar_today_outlined,
                  size: 12, color: AppColors.textSecondary),
              const SizedBox(width: 4),
              Text(
                dateLabel,
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textSecondary,
                ),
              ),
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
        ChunkedGrid(
          crossAxisCount: 2,
          mainAxisSpacing: 8,
          crossAxisSpacing: 8,
          children: [for (final c in cells) _PrimaryKpiTile(cell: c)],
        ),
      ],
    );
  }
}

class _PrimaryKpiTile extends StatelessWidget {
  const _PrimaryKpiTile({required this.cell});
  final KpiCell cell;

  IconData _icon(String label) {
    final l = label.toLowerCase();
    if (l.contains('vehicle')) return Icons.local_shipping_rounded;
    if (l.contains('driver')) return Icons.person_rounded;
    if (l.contains('trip')) return Icons.route_rounded;
    if (l.contains('alert')) return Icons.shield_outlined;
    if (l.contains('online')) return Icons.wifi_rounded;
    return Icons.bar_chart_rounded;
  }

  @override
  Widget build(BuildContext context) {
    final color = _colorKey(cell.colorKey);
    return SgCard(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
      onTap: cell.route == null ? null : () => context.push(cell.route!),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.10),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(_icon(cell.label), color: color, size: 18),
          ),
          const SizedBox(height: 8),
          Text(
            cell.label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w500,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 1),
          Text(
            cell.value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.w800,
              color: AppColors.textPrimary,
              height: 1.1,
            ),
          ),
          if (cell.subtitle != null && cell.subtitle!.isNotEmpty)
            Text(
              cell.subtitle!,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: 10,
                fontWeight: FontWeight.w600,
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
          child: total == 0
              ? _NoVehiclesPlaceholder(updated: updated)
              : Column(
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

class _NoVehiclesPlaceholder extends StatelessWidget {
  const _NoVehiclesPlaceholder({required this.updated});
  final String updated;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 12),
      child: Column(
        children: [
          const Icon(
            Icons.local_shipping_outlined,
            size: 40,
            color: AppColors.textMuted,
          ),
          const SizedBox(height: 10),
          const Text(
            'No vehicles in fleet',
            style: TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: 15,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'Add vehicles to see live GPS health and tracking data.',
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: 12,
              height: 1.4,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.refresh_rounded,
                  size: 13, color: AppColors.textMuted),
              const SizedBox(width: 4),
              Text(
                'Updated $updated',
                style: const TextStyle(
                  fontSize: 11,
                  color: AppColors.textMuted,
                ),
              ),
            ],
          ),
        ],
      ),
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
  final Map<FleetTrackStatus, BitmapDescriptor> _icons = {};
  bool _iconsReady = false;

  static const _defaultCamera = CameraPosition(
    target: LatLng(25.3463, 55.4209), // Sharjah-ish default
    zoom: 11,
  );

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      final dpr = MediaQuery.devicePixelRatioOf(context);
      _loadIcons(dpr);
    });
  }

  Future<void> _loadIcons(double dpr) async {
    final next = <FleetTrackStatus, BitmapDescriptor>{};
    for (final s in FleetTrackStatus.values) {
      next[s] = await VehiclePulseIcons.iconFor(
        s,
        devicePixelRatio: dpr,
        mini: true,
      );
    }
    if (!mounted) return;
    setState(() {
      _icons
        ..clear()
        ..addAll(next);
      _iconsReady = true;
    });
  }

  @override
  void dispose() {
    _map?.dispose();
    super.dispose();
  }

  Set<Marker> _markers() {
    if (!_iconsReady) return {};
    return widget.positions
        .where((p) => p.latitude != 0 || p.longitude != 0)
        .map(
          (p) {
            final status = resolveFleetStatus(
              speed: p.speed,
              ignition: p.ignition,
              lastUpdated: p.timestamp,
              hasGps: true,
              alarmType: p.alarmType,
            );
            return Marker(
              markerId: MarkerId('v${p.vehicleId}'),
              position: LatLng(p.latitude, p.longitude),
              rotation: p.heading ?? 0,
              flat: true,
              anchor: const Offset(0.5, 0.5),
              icon: _icons[status] ??
                  BitmapDescriptor.defaultMarkerWithHue(
                    BitmapDescriptor.hueAzure,
                  ),
              infoWindow: InfoWindow(
                title: 'Vehicle ${p.vehicleId}',
                snippet: '${p.speed.toStringAsFixed(0)} km/h',
              ),
            );
          },
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
    // fleetHealthPercent is no longer shown in the dashboard strip.
    // ignore: avoid_unused_constructor_parameters
    double? fleetHealthPercent,
  });

  final List<AiAttentionItem> items;
  final bool canOpenAi;

  @override
  Widget build(BuildContext context) {
    final firstPrompt = items
        .map((e) => e.suggestedPrompt)
        .whereType<String>()
        .where((s) => s.isNotEmpty)
        .firstOrNull;

    return SgCard(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      child: Row(
        children: [
          Container(
            width: 38,
            height: 38,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.10),
              borderRadius: BorderRadius.circular(10),
            ),
            child: const Icon(Icons.auto_awesome_rounded,
                size: 20, color: AppColors.primary),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'AI Copilot',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                const Text(
                  'Ask anything about your fleet...',
                  style: TextStyle(
                    fontSize: 11,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          if (canOpenAi)
            TextButton(
              onPressed: () {
                if (firstPrompt != null) {
                  context.push('/ai?q=${Uri.encodeComponent(firstPrompt)}');
                } else {
                  context.push('/ai');
                }
              },
              style: TextButton.styleFrom(
                foregroundColor: AppColors.primary,
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              ),
              child: const Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('Ask AI',
                      style: TextStyle(
                          fontSize: 13, fontWeight: FontWeight.w700)),
                  SizedBox(width: 2),
                  Icon(Icons.chevron_right_rounded, size: 16),
                ],
              ),
            ),
        ],
      ),
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
    final grouped = _groupAlerts(events);
    final badge = criticalCount > 0 ? criticalCount : events.length;
    final visible = grouped.take(3).toList();

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
            const SizedBox(width: 8),
            GestureDetector(
              onTap: () => context.push('/alerts'),
              child: const Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'View all alerts',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: AppColors.primary,
                    ),
                  ),
                  SizedBox(width: 2),
                  Icon(Icons.chevron_right_rounded,
                      size: 16, color: AppColors.primary),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        SgCard(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
          child: events.isEmpty
              ? const Padding(
                  padding: EdgeInsets.symmetric(vertical: 6),
                  child: Text(
                    'No open critical alerts',
                    style: TextStyle(color: AppColors.textSecondary),
                  ),
                )
              : Column(
                  children: [
                    for (var i = 0; i < visible.length; i++) ...[
                      if (i > 0)
                        const Divider(height: 1, color: AppColors.divider),
                      _AlertRow(alert: visible[i]),
                    ],
                    const Divider(height: 1, color: AppColors.divider),
                    InkWell(
                      onTap: () => context.push('/alerts'),
                      child: Padding(
                        padding: const EdgeInsets.symmetric(vertical: 10),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Text(
                              'View all $badge alerts →',
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
                  ],
                ),
        ),
      ],
    );
  }

  List<_GroupedAlert> _groupAlerts(List<GpsAlertEvent> source) {
    final map = <String, _GroupedAlert>{};
    for (final event in source) {
      final key = '${event.vehicleId}:${event.eventType.toLowerCase()}';
      final existing = map[key];
      if (existing == null) {
        map[key] = _GroupedAlert(event: event, count: 1);
        continue;
      }
      if (event.timestamp.isAfter(existing.event.timestamp)) {
        map[key] = _GroupedAlert(event: event, count: existing.count + 1);
      } else {
        map[key] = _GroupedAlert(event: existing.event, count: existing.count + 1);
      }
    }
    final grouped = map.values.toList();
    grouped.sort((a, b) => b.event.timestamp.compareTo(a.event.timestamp));
    return grouped;
  }
}

class _AlertRow extends StatelessWidget {
  const _AlertRow({required this.alert});
  final _GroupedAlert alert;

  static String _humanize(String eventType) {
    if (eventType.isEmpty) return 'Alert';
    return eventType
        .replaceAll('_', ' ')
        .split(' ')
        .map((w) => w.isEmpty ? '' : '${w[0].toUpperCase()}${w.substring(1).toLowerCase()}')
        .join(' ');
  }

  @override
  Widget build(BuildContext context) {
    final event = alert.event;
    final color = _severityColor(event.severity);
    final vehicleName =
        (event.vehicleName?.isNotEmpty == true) ? event.vehicleName! : 'VH-${event.vehicleId}';

    return InkWell(
      onTap: () => context.push('/alerts'),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 10),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Container(
              width: 8,
              height: 8,
              decoration: BoxDecoration(
                color: color,
                shape: BoxShape.circle,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    _humanize(event.eventType),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 1),
                  Text(
                    alert.count > 1 ? '$vehicleName · x${alert.count}' : vehicleName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 11,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Text(
              _relativeTime(event.timestamp),
              style: const TextStyle(
                fontSize: 11,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(width: 4),
            const Icon(Icons.chevron_right_rounded,
                size: 16, color: AppColors.textMuted),
          ],
        ),
      ),
    );
  }
}

class _GroupedAlert {
  const _GroupedAlert({
    required this.event,
    required this.count,
  });

  final GpsAlertEvent event;
  final int count;
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
            return ChunkedGrid(
              crossAxisCount: 2,
              mainAxisSpacing: 10,
              crossAxisSpacing: 10,
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

/// Replaces shrinkWrap GridView.count (which triggers semantics assertion
/// floods when nested in a ListView) with a plain Column + Row layout.
class ChunkedGrid extends StatelessWidget {
  const ChunkedGrid({
    super.key,
    required this.children,
    required this.crossAxisCount,
    this.mainAxisSpacing = 8,
    this.crossAxisSpacing = 8,
  });

  final List<Widget> children;
  final int crossAxisCount;
  final double mainAxisSpacing;
  final double crossAxisSpacing;

  @override
  Widget build(BuildContext context) {
    final rows = <Widget>[];
    for (var i = 0; i < children.length; i += crossAxisCount) {
      final rowItems = children.sublist(
        i,
        (i + crossAxisCount).clamp(0, children.length),
      );
      // Pad with Spacer/Expanded placeholders if last row is short
      final cells = <Widget>[];
      for (var j = 0; j < rowItems.length; j++) {
        if (j > 0) cells.add(SizedBox(width: crossAxisSpacing));
        cells.add(Expanded(child: rowItems[j]));
      }
      // Fill remainder
      for (var j = rowItems.length; j < crossAxisCount; j++) {
        cells.add(SizedBox(width: crossAxisSpacing));
        cells.add(const Expanded(child: SizedBox.shrink()));
      }
      rows.add(IntrinsicHeight(child: Row(children: cells)));
      if (i + crossAxisCount < children.length) {
        rows.add(SizedBox(height: mainAxisSpacing));
      }
    }
    return Column(mainAxisSize: MainAxisSize.min, children: rows);
  }
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
  const KpiStrip({
    super.key,
    required this.items,
    this.title,
    this.viewAllLabel,
    this.viewAllRoute,
  });
  final List<(String, String, Color)> items;
  final String? title;
  final String? viewAllLabel;
  final String? viewAllRoute;

  @override
  Widget build(BuildContext context) {
    // Parse total for % bar
    int total = 0;
    for (final it in items) {
      total += int.tryParse(it.$2) ?? 0;
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (title != null) ...[
          Row(
            children: [
              Expanded(child: SgSectionTitle(title!)),
              if (viewAllLabel != null && viewAllRoute != null)
                GestureDetector(
                  onTap: () => context.push(viewAllRoute!),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        viewAllLabel!,
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: AppColors.primary,
                        ),
                      ),
                      const SizedBox(width: 2),
                      const Icon(Icons.chevron_right_rounded,
                          size: 16, color: AppColors.primary),
                    ],
                  ),
                ),
            ],
          ),
          const SizedBox(height: 8),
        ],
        SgCard(
          child: Column(
            children: [
              Row(
                children: [
                  for (var i = 0; i < items.length; i++) ...[
                    if (i > 0)
                      Container(
                          height: 36,
                          width: 1,
                          color: AppColors.divider),
                    Expanded(
                      child: Column(
                        children: [
                          Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            crossAxisAlignment: CrossAxisAlignment.center,
                            children: [
                              Container(
                                width: 10,
                                height: 10,
                                decoration: BoxDecoration(
                                  color:
                                      items[i].$3.withValues(alpha: 0.8),
                                  shape: BoxShape.circle,
                                ),
                              ),
                              const SizedBox(width: 5),
                              Text(
                                items[i].$2,
                                style: TextStyle(
                                  fontSize: 22,
                                  fontWeight: FontWeight.w800,
                                  color: items[i].$3,
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 3),
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
              if (total > 0) ...[
                const SizedBox(height: 10),
                ClipRRect(
                  borderRadius: BorderRadius.circular(4),
                  child: Row(
                    children: [
                      for (final it in items)
                        Builder(builder: (ctx) {
                          final n = int.tryParse(it.$2) ?? 0;
                          final frac = total > 0 ? n / total : 0.0;
                          if (frac <= 0) return const SizedBox.shrink();
                          return Flexible(
                            flex: n,
                            child: Container(
                              height: 4,
                              color: it.$3.withValues(alpha: 0.7),
                            ),
                          );
                        }),
                    ],
                  ),
                ),
                const SizedBox(height: 4),
                Row(
                  children: [
                    for (var i = 0; i < items.length; i++) ...[
                      Expanded(
                        child: Builder(builder: (_) {
                          final n = int.tryParse(items[i].$2) ?? 0;
                          final pct = total > 0
                              ? '${(n * 100 ~/ total)}%'
                              : '0%';
                          return Text(
                            pct,
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              fontSize: 10,
                              fontWeight: FontWeight.w600,
                              color:
                                  items[i].$3.withValues(alpha: 0.8),
                            ),
                          );
                        }),
                      ),
                    ],
                  ],
                ),
              ],
            ],
          ),
        ),
      ],
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

  String _subtitle(String iconName) => switch (iconName) {
        'fleet' => 'Manage vehicles',
        'map' => 'Track in real-time',
        'alert' => 'View all alerts',
        'route' => 'All trips',
        'reports' => 'Analytics & insights',
        'drivers' => 'All drivers',
        'fuel' => 'Fuel logs',
        'build' => 'Work orders',
        _ => '',
      };

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Expanded(child: SgSectionTitle('Quick Actions')),
            TextButton(
              onPressed: () => context.push('/more'),
              style: TextButton.styleFrom(
                foregroundColor: AppColors.primary,
                padding:
                    const EdgeInsets.symmetric(horizontal: 8, vertical: 0),
              ),
              child: const Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('View all',
                      style: TextStyle(
                          fontSize: 13, fontWeight: FontWeight.w600)),
                  SizedBox(width: 2),
                  Icon(Icons.chevron_right_rounded, size: 16),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        SizedBox(
          height: 96,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: actions.length,
            separatorBuilder: (_, __) => const SizedBox(width: 12),
            itemBuilder: (context, i) {
              final a = actions[i];
              final color = _colorKey(a.colorKey);
              final sub = _subtitle(a.iconName);
              return InkWell(
                onTap: () => context.push(a.route),
                borderRadius: BorderRadius.circular(14),
                child: SizedBox(
                  width: 76,
                  child: Column(
                    children: [
                      Container(
                        width: 52,
                        height: 52,
                        decoration: BoxDecoration(
                          color: color.withValues(alpha: 0.11),
                          borderRadius: BorderRadius.circular(16),
                        ),
                        child: Icon(_iconName(a.iconName),
                            color: color, size: 26),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        a.label,
                        textAlign: TextAlign.center,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w700,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      if (sub.isNotEmpty)
                        Text(
                          sub,
                          textAlign: TextAlign.center,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            fontSize: 9,
                            color: AppColors.textSecondary,
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
