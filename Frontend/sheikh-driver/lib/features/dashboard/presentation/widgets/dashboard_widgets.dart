import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import '../../../../core/constants/app_brand.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../../alerts/domain/gps_alert_models.dart';
import '../../../auth/data/auth_repository.dart';
import '../../../fleet/domain/fleet_models.dart';
import '../../../fleet/domain/fleet_status.dart';
import '../../../fleet/presentation/platform_company_filter.dart';
import '../../../fleet/presentation/widgets/vehicle_pulse_icons.dart';
import '../../../notifications/presentation/notifications_notifier.dart';
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
      'mic' => Icons.mic_rounded,
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

/// Compact enterprise header matching Ali’s mockup.
class OpsHeaderCard extends ConsumerWidget {
  const OpsHeaderCard({
    super.key,
    required this.name,
    required this.role,
    this.tenantId,
    this.lastSyncedAt,
    this.companyName,
    this.branchName,
  });

  final String name;
  final DashboardRole role;
  final int? tenantId;
  final DateTime? lastSyncedAt;
  final String? companyName;
  final String? branchName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final p = context.sg;
    final first = name.trim().split(RegExp(r'\s+')).first;
    final greeting = dashboardGreeting();
    final syncLabel = lastSyncedAt == null
        ? 'Synced just now'
        : 'Synced ${_relativeTime(lastSyncedAt!)}';
    final tenantLabel = (companyName != null && companyName!.trim().isNotEmpty)
        ? companyName!.trim()
        : (tenantId != null ? 'Tenant $tenantId' : AppBrand.productName);
    final unread = ref.watch(unreadNotificationsCountProvider).valueOrNull ?? 0;

    return Stack(
      clipBehavior: Clip.none,
      children: [
        Positioned(
          right: -24,
          top: -28,
          child: IgnorePointer(
            child: CustomPaint(
              size: const Size(180, 120),
              painter: _HeaderWavePainter(
                color: AppColors.primary.withValues(alpha: 0.14),
                accent: AppColors.cyan.withValues(alpha: 0.18),
              ),
            ),
          ),
        ),
        Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Image.asset(
                        AppBrand.logoOnLightAsset,
                        height: 36,
                        fit: BoxFit.contain,
                        errorBuilder: (_, __, ___) => Image.asset(
                          AppBrand.logoAsset,
                          height: 36,
                          fit: BoxFit.contain,
                          errorBuilder: (_, __, ___) => const SizedBox.shrink(),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Flexible(
                                  child: Text(
                                    AppBrand.productName,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                    style: TextStyle(
                                      fontWeight: FontWeight.w800,
                                      fontSize: 18,
                                      letterSpacing: -0.3,
                                      color: p.textPrimary,
                                    ),
                                  ),
                                ),
                                const SizedBox(width: 6),
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 6,
                                    vertical: 2,
                                  ),
                                  decoration: BoxDecoration(
                                    color: AppColors.primary
                                        .withValues(alpha: 0.10),
                                    borderRadius: BorderRadius.circular(6),
                                  ),
                                  child: const Text(
                                    'FLEET',
                                    style: TextStyle(
                                      fontSize: 10,
                                      fontWeight: FontWeight.w700,
                                      letterSpacing: 0.4,
                                      color: AppColors.primaryDark,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Consumer(
                              builder: (context, ref, _) {
                                final cross = ref
                                        .watch(fleetSessionProvider)
                                        ?.canCrossTenantVehicles ==
                                    true;
                                final filterId =
                                    ref.watch(platformCompanyFilterProvider);
                                final companies = cross
                                    ? (ref
                                            .watch(adminCompaniesProvider)
                                            .valueOrNull ??
                                        const [])
                                    : const [];
                                String chipLabel = tenantLabel;
                                if (cross) {
                                  if (filterId == null) {
                                    chipLabel = 'All Companies';
                                  } else {
                                    final match = companies
                                        .where((c) => c.id == filterId)
                                        .toList();
                                    chipLabel = match.isEmpty
                                        ? 'Company #$filterId'
                                        : match.first.name;
                                  }
                                }
                                return InkWell(
                                  onTap: () {
                                    if (cross) {
                                      // Fleet hub owns the filter sheet; push Vehicles tab.
                                      context.go('/fleet');
                                      return;
                                    }
                                    _showTenantSheet(context, tenantLabel);
                                  },
                                  borderRadius: BorderRadius.circular(8),
                                  child: Row(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      const Icon(
                                        Icons.apartment_rounded,
                                        size: 14,
                                        color: AppColors.primary,
                                      ),
                                      const SizedBox(width: 4),
                                      Flexible(
                                        child: Text(
                                          chipLabel,
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                          style: TextStyle(
                                            fontSize: 12,
                                            fontWeight: FontWeight.w600,
                                            color: p.textSecondary,
                                          ),
                                        ),
                                      ),
                                      Icon(
                                        Icons.keyboard_arrow_down_rounded,
                                        size: 16,
                                        color: p.textMuted,
                                      ),
                                    ],
                                  ),
                                );
                              },
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 4),
                IconButton(
                  tooltip: 'Settings',
                  visualDensity: VisualDensity.compact,
                  style: IconButton.styleFrom(
                    backgroundColor: p.card,
                    side: BorderSide(color: p.border.withValues(alpha: 0.85)),
                  ),
                  onPressed: () => context.push('/settings'),
                  icon: Icon(Icons.settings_outlined, color: p.textPrimary),
                ),
                IconButton(
                  tooltip: 'Notifications',
                  visualDensity: VisualDensity.compact,
                  style: IconButton.styleFrom(
                    backgroundColor: p.card,
                    side: BorderSide(color: p.border.withValues(alpha: 0.85)),
                  ),
                  onPressed: () => context.go('/notifications'),
                  icon: SgNotificationBellIcon(
                    unreadCount: unread,
                    iconColor: p.textPrimary,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '$greeting, $first',
                        style:
                            Theme.of(context).textTheme.headlineSmall?.copyWith(
                                  color: p.textPrimary,
                                  fontWeight: FontWeight.w800,
                                  letterSpacing: -0.4,
                                  height: 1.15,
                                  fontSize: 20,
                                ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        role == DashboardRole.driver
                            ? "Here's what's on your schedule"
                            : 'Fleet command overview.',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: p.textSecondary,
                              fontSize: 12,
                            ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                  decoration: BoxDecoration(
                    color: AppColors.success.withValues(alpha: 0.10),
                    borderRadius: BorderRadius.circular(999),
                    border: Border.all(
                      color: AppColors.success.withValues(alpha: 0.22),
                    ),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(
                        Icons.sync_rounded,
                        size: 12,
                        color: AppColors.success,
                      ),
                      const SizedBox(width: 4),
                      Text(
                        syncLabel,
                        style: const TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w600,
                          color: AppColors.primaryDark,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ],
        ),
      ],
    );
  }

  void _showTenantSheet(BuildContext context, String tenantLabel) {
    final p = context.sg;
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (ctx) => Padding(
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Active company',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w800,
                color: p.textPrimary,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              tenantLabel,
              style: TextStyle(fontSize: 15, color: p.textPrimary),
            ),
            if (branchName != null && branchName!.trim().isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                'Branch · ${branchName!.trim()}',
                style: TextStyle(fontSize: 13, color: p.textSecondary),
              ),
            ],
            const SizedBox(height: 12),
            Text(
              'Company switching is managed from your organization settings.',
              style: TextStyle(fontSize: 12, color: p.textMuted),
            ),
          ],
        ),
      ),
    );
  }
}

class _HeaderWavePainter extends CustomPainter {
  _HeaderWavePainter({required this.color, required this.accent});
  final Color color;
  final Color accent;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()..style = PaintingStyle.fill;
    paint.color = color;
    final path = Path()
      ..moveTo(size.width * 0.15, size.height)
      ..quadraticBezierTo(
        size.width * 0.35,
        size.height * 0.15,
        size.width * 0.7,
        size.height * 0.45,
      )
      ..quadraticBezierTo(
        size.width * 0.92,
        size.height * 0.7,
        size.width,
        0,
      )
      ..lineTo(size.width, size.height)
      ..close();
    canvas.drawPath(path, paint);
    paint.color = accent;
    final path2 = Path()
      ..moveTo(size.width * 0.4, size.height)
      ..quadraticBezierTo(
        size.width * 0.55,
        size.height * 0.35,
        size.width,
        size.height * 0.2,
      )
      ..lineTo(size.width, size.height)
      ..close();
    canvas.drawPath(path2, paint);
  }

  @override
  bool shouldRepaint(covariant _HeaderWavePainter oldDelegate) =>
      oldDelegate.color != color || oldDelegate.accent != accent;
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
    return SgCard(
      child: Row(
        children: [
          const Icon(Icons.admin_panel_settings_outlined, color: AppColors.primary),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              'Platform admin — showing this tenant’s operational view. Multi-tenant KPIs come in a later release.',
              style: TextStyle(fontSize: 12, color: context.sg.textSecondary),
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
    return ChunkedGrid(
      crossAxisCount: 2,
      mainAxisSpacing: AppSpacing.sm,
      crossAxisSpacing: AppSpacing.sm,
      children: [for (final c in cells) _PrimaryKpiTile(cell: c)],
    );
  }
}

class _PrimaryKpiTile extends StatelessWidget {
  const _PrimaryKpiTile({required this.cell});
  final KpiCell cell;

  /// Icons: Online wifi · Offline power · Idle pause · Moving play.
  IconData _icon(String label) {
    final l = label.toLowerCase();
    if (l.contains('online')) return Icons.wifi_rounded;
    if (l.contains('offline')) return Icons.power_settings_new_rounded;
    if (l.contains('idle')) return Icons.pause_rounded;
    if (l.contains('moving')) return Icons.play_arrow_rounded;
    if (l.contains('vehicle')) return Icons.directions_car_filled_rounded;
    if (l.contains('driver')) return Icons.person_rounded;
    if (l.contains('trip')) return Icons.route_rounded;
    if (l.contains('alert')) return Icons.shield_outlined;
    return Icons.bar_chart_rounded;
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final color = _colorKey(cell.colorKey);
    return SgCard(
      padding: const EdgeInsets.fromLTRB(12, 12, 10, 12),
      onTap: cell.route == null ? null : () => context.push(cell.route!),
      child: SizedBox(
        // Compact bento so Fleet Activity + map stay above the fold.
        height: 100,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  width: 32,
                  height: 32,
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.14),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Icon(_icon(cell.label), color: color, size: 18),
                ),
                const Spacer(),
                Container(
                  width: 6,
                  height: 6,
                  decoration: BoxDecoration(
                    color: color,
                    shape: BoxShape.circle,
                  ),
                ),
                const SizedBox(width: 4),
                Icon(Icons.chevron_right_rounded, size: 16, color: p.textMuted),
              ],
            ),
            const Spacer(),
            Text(
              cell.label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w500,
                color: p.textSecondary,
              ),
            ),
            const SizedBox(height: 2),
            Text(
              cell.value,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: 24,
                fontWeight: FontWeight.w800,
                letterSpacing: -0.5,
                color: p.textPrimary,
                height: 1.05,
                fontFeatures: const [FontFeature.tabularFigures()],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Horizontal Moving / Idle / Offline / Never Seen distribution (mockup).
class FleetActivityDistributionCard extends StatelessWidget {
  const FleetActivityDistributionCard({
    super.key,
    required this.moving,
    required this.idle,
    required this.offline,
    required this.neverSeen,
  });

  final int moving;
  final int idle;
  final int offline;
  final int neverSeen;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final items = [
      _DistItem('Moving', moving < 0 ? 0 : moving, AppColors.primary,
          '/fleet?status=moving'),
      _DistItem(
          'Idle', idle < 0 ? 0 : idle, AppColors.warning, '/fleet?status=idle'),
      _DistItem('Offline', offline < 0 ? 0 : offline, AppColors.error,
          '/fleet?status=offline'),
      _DistItem(
          'Never Seen', neverSeen < 0 ? 0 : neverSeen, p.textMuted, '/fleet'),
    ];
    final total = items.fold<int>(0, (s, e) => s + e.count);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                'Fleet Activity',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w800,
                  color: p.textPrimary,
                ),
              ),
            ),
            GestureDetector(
              onTap: () => context.push('/more/reports'),
              child: const Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'View Report',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
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
        const SizedBox(height: 10),
        SgCard(
          padding: const EdgeInsets.fromLTRB(14, 14, 14, 12),
          child: Column(
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(999),
                child: SizedBox(
                  height: 10,
                  child: total <= 0
                      ? ColoredBox(color: p.border)
                      : Row(
                          children: [
                            for (final it in items)
                              if (it.count > 0)
                                Expanded(
                                  flex: it.count,
                                  child: ColoredBox(color: it.color),
                                ),
                          ],
                        ),
                ),
              ),
              const SizedBox(height: 12),
              // Mockup: 2×2 legend (Moving / Idle / Offline / Never Seen).
              ChunkedGrid(
                crossAxisCount: 2,
                mainAxisSpacing: 10,
                crossAxisSpacing: 8,
                children: [
                  for (final it in items)
                    InkWell(
                      onTap: () => context.push(it.route),
                      borderRadius: BorderRadius.circular(8),
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 4, vertical: 2),
                        child: Row(
                          children: [
                            Container(
                              width: 8,
                              height: 8,
                              decoration: BoxDecoration(
                                color: it.color,
                                shape: BoxShape.circle,
                              ),
                            ),
                            const SizedBox(width: 6),
                            Expanded(
                              child: Text(
                                it.label,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(
                                  fontSize: 12,
                                  fontWeight: FontWeight.w600,
                                  color: p.textSecondary,
                                ),
                              ),
                            ),
                            Text(
                              '${it.count}',
                              style: TextStyle(
                                fontSize: 14,
                                fontWeight: FontWeight.w800,
                                color: p.textPrimary,
                              ),
                            ),
                            const SizedBox(width: 4),
                            Text(
                              total <= 0
                                  ? '(0%)'
                                  : '(${(it.count * 100 / total).round()}%)',
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                                color: it.color,
                              ),
                            ),
                          ],
                        ),
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

class _DistItem {
  const _DistItem(this.label, this.count, this.color, this.route);
  final String label;
  final int count;
  final Color color;
  final String route;
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
    final maintenance = fleet?.maintenanceDue ?? maint?.dueForService ?? 0;
    final updated =
        data.lastSyncedAt == null ? '—' : _relativeTime(data.lastSyncedAt!);

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
                                  backgroundColor: context.sg.border,
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
                                    style: TextStyle(
                                      fontSize: 20,
                                      fontWeight: FontWeight.w800,
                                      color: context.sg.textPrimary,
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
                          style: TextStyle(
                            fontSize: 12,
                            height: 1.35,
                            color: context.sg.textSecondary,
                          ),
                        ),
                      ),
                    ],
                    const SizedBox(height: 10),
                    Row(
                      children: [
                        Icon(Icons.refresh_rounded,
                            size: 14, color: context.sg.textSecondary),
                        const SizedBox(width: 4),
                        Text(
                          'Updated $updated',
                          style: TextStyle(
                            fontSize: 11,
                            color: context.sg.textSecondary,
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
          Icon(
            Icons.local_shipping_outlined,
            size: 40,
            color: context.sg.textMuted,
          ),
          const SizedBox(height: 10),
          Text(
            'No vehicles in fleet',
            style: TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: 15,
              color: context.sg.textPrimary,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Add vehicles to see live GPS health and tracking data.',
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: 12,
              height: 1.4,
              color: context.sg.textSecondary,
            ),
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(Icons.refresh_rounded,
                  size: 13, color: context.sg.textMuted),
              const SizedBox(width: 4),
              Text(
                'Updated $updated',
                style: TextStyle(
                  fontSize: 11,
                  color: context.sg.textMuted,
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
              style: TextStyle(
                fontSize: 12,
                color: context.sg.textSecondary,
              ),
            ),
          ),
          Text(
            value,
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w700,
              color: context.sg.textPrimary,
            ),
          ),
        ],
      ),
    );
  }
}

/// Compact non-interactive Google Map snapshot for the home feed.
class LiveMapPreviewCard extends StatefulWidget {
  const LiveMapPreviewCard({
    super.key,
    required this.positions,
    this.movingCount = 0,
    this.idleCount = 0,
  });
  final List<GpsPosition> positions;
  final int movingCount;
  final int idleCount;

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
    ).toSet();
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
    final p = context.sg;
    final moving = widget.movingCount < 0 ? 0 : widget.movingCount;
    final idle = widget.idleCount < 0 ? 0 : widget.idleCount;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                'Live Fleet Map',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w800,
                  color: p.textPrimary,
                ),
              ),
            ),
            GestureDetector(
              onTap: () => context.push('/fleet/map'),
              child: const Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'Open Map',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
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
          padding: EdgeInsets.zero,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(16),
            child: SizedBox(
              height: 176,
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
                  // Top-left status pill — avoids Google watermark collision.
                  Positioned(
                    left: 10,
                    top: 10,
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 6,
                      ),
                      decoration: BoxDecoration(
                        color: Colors.white.withValues(alpha: 0.95),
                        borderRadius: BorderRadius.circular(20),
                        border: Border.all(
                          color: p.border.withValues(alpha: 0.85),
                        ),
                        boxShadow: [
                          BoxShadow(
                            color: Colors.black.withValues(alpha: 0.06),
                            blurRadius: 8,
                            offset: const Offset(0, 2),
                          ),
                        ],
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Container(
                            width: 7,
                            height: 7,
                            decoration: BoxDecoration(
                              color: moving > 0
                                  ? AppColors.primary
                                  : AppColors.warning,
                              shape: BoxShape.circle,
                            ),
                          ),
                          const SizedBox(width: 6),
                          Text(
                            '$moving moving · $idle idle',
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                              color: p.textPrimary,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  if (widget.positions.isEmpty)
                    Positioned.fill(
                      child: ColoredBox(
                        color: p.card.withValues(alpha: 0.72),
                        child: Center(
                          child: Text(
                            'No live positions yet',
                            style: TextStyle(color: p.textSecondary),
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
                Text(
                  'AI Copilot',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                    color: context.sg.textPrimary,
                  ),
                ),
                Text(
                  'Ask about vehicles, trips, or alerts…',
                  style: TextStyle(
                    fontSize: 11,
                    color: context.sg.textSecondary,
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
                      style:
                          TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
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
    this.activeCount = 0,
  });

  final List<GpsAlertEvent> events;
  final int criticalCount;
  final int activeCount;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final grouped = _groupAlerts(events);
    final badge = activeCount > 0
        ? activeCount
        : (criticalCount > 0 ? criticalCount : events.length);
    final visible = grouped.take(3).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                'Alerts Requiring Attention',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w800,
                  color: p.textPrimary,
                ),
              ),
            ),
            GestureDetector(
              onTap: () => context.push('/alerts'),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    badge > 0 ? 'View All ($badge)' : 'View All',
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
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
        SgCard(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
          child: events.isEmpty
              ? const SgInlineEmpty(
                  message: 'No open alerts requiring attention',
                  icon: Icons.shield_outlined,
                )
              : Column(
                  children: [
                    for (var i = 0; i < visible.length; i++) ...[
                      if (i > 0) Divider(height: 1, color: p.divider),
                      _AlertRow(alert: visible[i]),
                    ],
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
        map[key] =
            _GroupedAlert(event: existing.event, count: existing.count + 1);
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
        .map((w) => w.isEmpty
            ? ''
            : '${w[0].toUpperCase()}${w.substring(1).toLowerCase()}')
        .join(' ');
  }

  static IconData _iconFor(String eventType) {
    final t = eventType.toLowerCase();
    if (t.contains('battery') || t.contains('power')) {
      return Icons.battery_alert_rounded;
    }
    if (t.contains('speed') || t.contains('overspeed')) {
      return Icons.speed_rounded;
    }
    if (t.contains('geofence') || t.contains('fence')) {
      return Icons.fence_outlined;
    }
    if (t.contains('sos') || t.contains('panic')) {
      return Icons.sos_rounded;
    }
    if (t.contains('ignition')) return Icons.key_rounded;
    return Icons.warning_amber_rounded;
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final event = alert.event;
    final color = _severityColor(event.severity);
    final vehicleName = (event.vehicleName?.isNotEmpty == true)
        ? event.vehicleName!
        : 'VH-${event.vehicleId}';

    return InkWell(
      onTap: () => context.push('/alerts'),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 10),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Icon(_iconFor(event.eventType), color: color, size: 18),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Flexible(
                        child: Text(
                          _humanize(event.eventType),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w700,
                            color: p.textPrimary,
                          ),
                        ),
                      ),
                      if (alert.count > 1) ...[
                        const SizedBox(width: 6),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 6,
                            vertical: 2,
                          ),
                          decoration: BoxDecoration(
                            color: color.withValues(alpha: 0.14),
                            borderRadius: BorderRadius.circular(6),
                          ),
                          child: Text(
                            'x${alert.count}',
                            style: TextStyle(
                              fontSize: 10,
                              fontWeight: FontWeight.w700,
                              color: color,
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                  const SizedBox(height: 1),
                  Text(
                    vehicleName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 11,
                      color: p.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Text(
              _relativeTime(event.timestamp),
              style: TextStyle(
                fontSize: 11,
                color: p.textSecondary,
              ),
            ),
            const SizedBox(width: 4),
            Icon(Icons.chevron_right_rounded, size: 16, color: p.textMuted),
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
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: context.sg.textSecondary,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            value,
            style: TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w800,
              color: context.sg.textPrimary,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            detail,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 11,
              color: context.sg.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}

class RecentActivitiesCard extends StatelessWidget {
  const RecentActivitiesCard({
    super.key,
    required this.items,
    this.totalCount,
  });
  final List<ActivityItem> items;

  /// Optional badge count for View All (parity with Alerts Requiring Attention).
  final int? totalCount;

  IconData _icon(String kind) => switch (kind) {
        'alert' => Icons.warning_amber_rounded,
        'trip' => Icons.location_on_outlined,
        'attendance' => Icons.person_outline_rounded,
        'maintenance' => Icons.build_outlined,
        _ => Icons.notifications_none_rounded,
      };

  Color _color(String kind) => switch (kind) {
        'alert' => AppColors.error,
        'trip' => AppColors.primary,
        'attendance' => AppColors.primary,
        'maintenance' => AppColors.warning,
        _ => AppColors.info,
      };

  /// Collapse consecutive identical titles into one row with a count pill.
  List<({ActivityItem item, int count})> _dedupe(List<ActivityItem> source) {
    final out = <({ActivityItem item, int count})>[];
    for (final item in source) {
      if (out.isNotEmpty &&
          out.last.item.title.trim().toLowerCase() ==
              item.title.trim().toLowerCase()) {
        final prev = out.removeLast();
        out.add((item: prev.item, count: prev.count + 1));
      } else {
        out.add((item: item, count: 1));
      }
    }
    return out;
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final grouped = _dedupe(items).take(3).toList();
    final badge = totalCount ?? items.length;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                'Recent Activity',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w800,
                  color: p.textPrimary,
                ),
              ),
            ),
            GestureDetector(
              onTap: () => context.push('/activities'),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    badge > 0 ? 'View All ($badge)' : 'View All',
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
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
        SgCard(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
          child: grouped.isEmpty
              ? const SgInlineEmpty(
                  message: 'No recent activity yet',
                  icon: Icons.timeline_outlined,
                )
              : Column(
                  children: [
                    for (var i = 0; i < grouped.length; i++) ...[
                      if (i > 0) Divider(height: 1, color: p.divider),
                      InkWell(
                        onTap: grouped[i].item.route == null
                            ? null
                            : () => context.push(grouped[i].item.route!),
                        child: Padding(
                          padding: const EdgeInsets.symmetric(vertical: 10),
                          child: Row(
                            children: [
                              Container(
                                width: 36,
                                height: 36,
                                decoration: BoxDecoration(
                                  color: _color(grouped[i].item.kind)
                                      .withValues(alpha: 0.12),
                                  borderRadius: BorderRadius.circular(10),
                                ),
                                child: Icon(
                                  _icon(grouped[i].item.kind),
                                  size: 18,
                                  color: _color(grouped[i].item.kind),
                                ),
                              ),
                              const SizedBox(width: 10),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      children: [
                                        Flexible(
                                          child: Text(
                                            grouped[i].item.title,
                                            maxLines: 1,
                                            overflow: TextOverflow.ellipsis,
                                            style: TextStyle(
                                              fontWeight: FontWeight.w700,
                                              fontSize: 13,
                                              color: p.textPrimary,
                                            ),
                                          ),
                                        ),
                                        if (grouped[i].count > 1) ...[
                                          const SizedBox(width: 6),
                                          Container(
                                            padding: const EdgeInsets.symmetric(
                                              horizontal: 6,
                                              vertical: 2,
                                            ),
                                            decoration: BoxDecoration(
                                              color: AppColors.warning
                                                  .withValues(alpha: 0.15),
                                              borderRadius:
                                                  BorderRadius.circular(6),
                                            ),
                                            child: Text(
                                              '${grouped[i].count}x',
                                              style: const TextStyle(
                                                fontSize: 10,
                                                fontWeight: FontWeight.w700,
                                                color: AppColors.warning,
                                              ),
                                            ),
                                          ),
                                        ],
                                      ],
                                    ),
                                    if (grouped[i].item.subtitle.isNotEmpty)
                                      Text(
                                        grouped[i].item.subtitle,
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: TextStyle(
                                          fontSize: 11,
                                          color: p.textSecondary,
                                        ),
                                      ),
                                  ],
                                ),
                              ),
                              Text(
                                _relativeTime(grouped[i].item.at),
                                style: TextStyle(
                                  fontSize: 11,
                                  color: p.textSecondary,
                                ),
                              ),
                            ],
                          ),
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
    return LiveMapPreviewCard(
      positions: data.livePositions,
      idleCount: (data.gps?.idle ?? 0) +
          (data.gps?.parked ?? 0) +
          (data.gps?.sos ?? 0),
      movingCount: data.gps?.moving ?? 0,
    );
  }
}

class KpiStrip extends StatelessWidget {
  const KpiStrip({
    super.key,
    required this.items,
    this.title,
    this.viewAllLabel,
    this.viewAllRoute,
    this.itemRoutes,
  });
  final List<(String, String, Color)> items;
  final String? title;
  final String? viewAllLabel;
  final String? viewAllRoute;

  /// Optional per-cell routes (same length as [items]).
  final List<String?>? itemRoutes;

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
                          height: 36, width: 1, color: context.sg.divider),
                    Expanded(
                      child: Builder(builder: (context) {
                        final route =
                            (itemRoutes != null && i < itemRoutes!.length)
                                ? itemRoutes![i]
                                : null;
                        final cell = Column(
                          children: [
                            Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              crossAxisAlignment: CrossAxisAlignment.center,
                              children: [
                                Container(
                                  width: 8,
                                  height: 8,
                                  decoration: BoxDecoration(
                                    color: items[i].$3.withValues(alpha: 0.85),
                                    shape: BoxShape.circle,
                                  ),
                                ),
                                const SizedBox(width: 5),
                                Text(
                                  items[i].$2,
                                  style: TextStyle(
                                    fontSize: 22,
                                    fontWeight: FontWeight.w800,
                                    letterSpacing: -0.3,
                                    color: items[i].$3,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 3),
                            Text(
                              items[i].$1,
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                fontSize: 11,
                                color: context.sg.textSecondary,
                              ),
                            ),
                          ],
                        );
                        if (route == null) return cell;
                        return InkWell(
                          onTap: () => context.push(route),
                          borderRadius: BorderRadius.circular(8),
                          child: Padding(
                            padding: const EdgeInsets.symmetric(vertical: 2),
                            child: cell,
                          ),
                        );
                      }),
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
                          final pct =
                              total > 0 ? '${(n * 100 ~/ total)}%' : '0%';
                          return Text(
                            pct,
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              fontSize: 10,
                              fontWeight: FontWeight.w600,
                              color: items[i].$3.withValues(alpha: 0.8),
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
    final p = context.sg;
    final name = driver.currentVehicle ?? 'No vehicle assigned';
    final plate = driver.currentVehiclePlate;
    final status =
        driver.driverStatus.isNotEmpty ? driver.driverStatus : 'Active';
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('My vehicle'),
        const SizedBox(height: 8),
        SgCard(
          child: Row(
            children: [
              const SgIconWell(
                icon: Icons.directions_car_filled_outlined,
                size: 44,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      name,
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        letterSpacing: -0.2,
                        color: p.textPrimary,
                      ),
                    ),
                    if (plate != null && plate.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Text(
                          plate,
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                            color: p.textSecondary,
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              StatusBadge(status),
            ],
          ),
        ),
      ],
    );
  }
}

class EarningsCard extends StatelessWidget {
  const EarningsCard({super.key, required this.driver});
  final DriverDashboardSummary driver;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return SgCard(
      onTap: () => context.push('/earnings'),
      child: Row(
        children: [
          const SgIconWell(
            icon: Icons.payments_outlined,
            color: AppColors.success,
            size: 44,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Earnings this week',
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w500,
                    color: p.textSecondary,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  _money(driver.earningsThisWeek),
                  style: TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                    letterSpacing: -0.4,
                    color: p.textPrimary,
                  ),
                ),
              ],
            ),
          ),
          Icon(Icons.chevron_right_rounded, color: p.textMuted),
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
                  style: TextStyle(
                    fontSize: 12,
                    color: context.sg.textSecondary,
                  ),
                ),
                Text(
                  _money(amount),
                  style: TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.w800,
                    color: context.sg.textPrimary,
                  ),
                ),
                Text(
                  subtitle,
                  style: TextStyle(
                    fontSize: 12,
                    color: context.sg.textSecondary,
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
          Text(
            'Monthly Fuel',
            style: TextStyle(
              fontWeight: FontWeight.w700,
              color: context.sg.textPrimary,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            _money(monthly),
            style: TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w800,
              color: context.sg.textPrimary,
            ),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: Text(
                  'Today\n${_money(fuel?.todayCost ?? 0)}',
                  style: TextStyle(
                    fontSize: 12,
                    color: context.sg.textSecondary,
                  ),
                ),
              ),
              Expanded(
                child: Text(
                  'Efficiency\n${eff == null ? '—' : '${eff.toStringAsFixed(1)} km/L'}',
                  style: TextStyle(
                    fontSize: 12,
                    color: context.sg.textSecondary,
                  ),
                ),
              ),
              Expanded(
                child: Text(
                  'Liters\n${(fuel?.totalLiters ?? 0).toStringAsFixed(0)} L',
                  style: TextStyle(
                    fontSize: 12,
                    color: context.sg.textSecondary,
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
          SgCard(
            child: Text(
              'No live trips right now',
              style: TextStyle(color: context.sg.textSecondary),
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
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        color: context.sg.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      '${t.driverName ?? 'No driver'} · ${t.vehicleName ?? 'No vehicle'}',
                      style: TextStyle(
                        fontSize: 12,
                        color: context.sg.textSecondary,
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
          SgCard(
            child: Text(
              'All scheduled trips have driver and vehicle',
              style: TextStyle(color: context.sg.textSecondary),
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
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        color: context.sg.textPrimary,
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
  const QuickActionsGrid({
    super.key,
    required this.actions,
    this.viewAllRoute = '/more',
  });
  final List<DashboardQuickAction> actions;

  /// When null, the "View all" control is hidden.
  final String? viewAllRoute;

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
            if (viewAllRoute != null)
              TextButton(
                onPressed: () => context.push(viewAllRoute!),
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
                borderRadius: BorderRadius.circular(AppRadii.md),
                child: SizedBox(
                  width: 76,
                  child: Column(
                    children: [
                      SgIconWell(
                        icon: _iconName(a.iconName),
                        color: color,
                        size: 52,
                      ),
                      const SizedBox(height: 6),
                      Text(
                        a.label,
                        textAlign: TextAlign.center,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w700,
                          color: context.sg.textPrimary,
                        ),
                      ),
                      if (sub.isNotEmpty)
                        Text(
                          sub,
                          textAlign: TextAlign.center,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 9,
                            color: context.sg.textSecondary,
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
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.only(top: AppSpacing.sm),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: AppColors.warning.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(AppRadii.md),
        border: Border.all(color: AppColors.warning.withValues(alpha: 0.28)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.info_outline_rounded,
              size: 16, color: AppColors.warning),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(
                fontSize: 12,
                height: 1.35,
                fontWeight: FontWeight.w600,
                color: AppColors.warning,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
