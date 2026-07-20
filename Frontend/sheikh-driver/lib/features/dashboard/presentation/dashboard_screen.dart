import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../trips/presentation/trips_notifier.dart';
import '../domain/dashboard_models.dart';
import 'dashboard_notifier.dart';

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key});

  String _greeting() {
    final h = DateTime.now().hour;
    if (h < 12) return 'Good Morning';
    if (h < 17) return 'Good Afternoon';
    return 'Good Evening';
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(authRepositoryProvider).session;
    final dashAsync = ref.watch(dashboardProvider);
    final name = session?.fullName.split(' ').first ?? 'Driver';

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.menu_rounded),
          onPressed: () => context.push('/settings'),
        ),
        title: const Text('Dashboard'),
        actions: [
          PopupMenuButton<String>(
            tooltip: 'Set availability',
            onSelected: (v) => ref.read(dashboardProvider.notifier).setStatus(v),
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'Online', child: Text('Online')),
              PopupMenuItem(value: 'Busy', child: Text('Busy (On Trip)')),
              PopupMenuItem(value: 'Break', child: Text('Break')),
              PopupMenuItem(value: 'Unavailable', child: Text('Unavailable')),
            ],
            icon: const Icon(Icons.toggle_on_outlined),
          ),
          IconButton(
            icon: const Icon(Icons.notifications_none_rounded),
            onPressed: () => context.go('/notifications'),
          ),
        ],
      ),
      body: dashAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => _ErrorView(
          message: e.toString(),
          onRetry: () => ref.read(dashboardProvider.notifier).refresh(),
        ),
        data: (summary) => RefreshIndicator(
          color: AppColors.primary,
          onRefresh: () async {
            await ref.read(dashboardProvider.notifier).refresh();
            await ref.read(offlineSyncProvider).syncNow();
            ref.invalidate(tripsProvider);
          },
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
            children: [
              Text(
                '${_greeting()}, $name 👋',
                style: const TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w800,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: 16),
              _VehicleCard(summary: summary),
              const SizedBox(height: 14),
              _StatsRow(summary: summary),
              const SizedBox(height: 14),
              _EarningsCard(summary: summary),
              const SizedBox(height: 20),
              const SgSectionTitle('Quick Actions'),
              const SizedBox(height: 12),
              _QuickActions(),
            ],
          ),
        ),
      ),
    );
  }
}

class _VehicleCard extends StatelessWidget {
  const _VehicleCard({required this.summary});
  final DashboardSummary summary;

  @override
  Widget build(BuildContext context) {
    return SgCard(
      child: Row(
        children: [
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(AppRadii.md),
            ),
            child: const Icon(Icons.airport_shuttle_rounded,
                color: AppColors.primary, size: 30),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  summary.currentVehicle ?? 'No vehicle assigned',
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 16,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  summary.currentVehiclePlate ?? '—',
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 13,
                  ),
                ),
              ],
            ),
          ),
          StatusBadge(
            summary.driverStatus.isNotEmpty ? summary.driverStatus : 'Active',
          ),
        ],
      ),
    );
  }
}

class _StatsRow extends StatelessWidget {
  const _StatsRow({required this.summary});
  final DashboardSummary summary;

  @override
  Widget build(BuildContext context) {
    final assigned = summary.assignedTripsToday;
    final completed = summary.completedToday;
    final remaining = (assigned - completed).clamp(0, 999);
    final unread = summary.unreadNotifications;

    return Row(
      children: [
        Expanded(
          child: _MiniStat(
            label: 'Trips Today',
            value: '$assigned',
            color: AppColors.primary,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _MiniStat(
            label: 'Completed',
            value: '$completed',
            color: AppColors.success,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _MiniStat(
            label: 'Remaining',
            value: '$remaining',
            color: AppColors.warning,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: _MiniStat(
            label: 'Alerts',
            value: '$unread',
            color: AppColors.error,
          ),
        ),
      ],
    );
  }
}

class _MiniStat extends StatelessWidget {
  const _MiniStat({
    required this.label,
    required this.value,
    required this.color,
  });

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return SgCard(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 12),
      child: Column(
        children: [
          Text(
            value,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: color,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            label,
            textAlign: TextAlign.center,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w600,
              color: AppColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}

class _EarningsCard extends StatelessWidget {
  const _EarningsCard({required this.summary});
  final DashboardSummary summary;

  @override
  Widget build(BuildContext context) {
    final fmt = NumberFormat('#,##0');
    return SgCard(
      onTap: () => context.push('/earnings'),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Weekly Earnings',
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  'PKR ${fmt.format(summary.earningsThisWeek)}',
                  style: const TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: 6),
                const Row(
                  children: [
                    Icon(Icons.trending_up, size: 14, color: AppColors.success),
                    SizedBox(width: 4),
                    Text(
                      'View details',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: AppColors.success,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          SizedBox(
            width: 72,
            height: 40,
            child: CustomPaint(
              painter: _SparklinePainter(color: AppColors.info),
            ),
          ),
        ],
      ),
    );
  }
}

class _SparklinePainter extends CustomPainter {
  _SparklinePainter({required this.color});
  final Color color;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..strokeWidth = 2
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;
    final path = Path();
    final pts = [0.2, 0.45, 0.35, 0.7, 0.55, 0.85, 0.65];
    for (var i = 0; i < pts.length; i++) {
      final x = size.width * i / (pts.length - 1);
      final y = size.height * (1 - pts[i]);
      if (i == 0) {
        path.moveTo(x, y);
      } else {
        path.lineTo(x, y);
      }
    }
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

class _QuickActions extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final actions = [
      (Icons.route_rounded, 'Trips', AppColors.primary, '/trips'),
      (Icons.fingerprint, 'Attendance', AppColors.success, '/attendance'),
      (Icons.local_gas_station_rounded, 'Fuel', AppColors.warning, '/fuel'),
      (Icons.fact_check_rounded, 'Inspection', AppColors.info, '/inspection'),
    ];

    return SizedBox(
      height: 104,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: actions.length,
        separatorBuilder: (_, __) => const SizedBox(width: 12),
        itemBuilder: (_, i) {
          final a = actions[i];
          return SizedBox(
            width: 88,
            child: SgCard(
              padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 8),
              onTap: () => context.push(a.$4),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 40,
                    height: 40,
                    decoration: BoxDecoration(
                      color: a.$3.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(AppRadii.sm),
                    ),
                    child: Icon(a.$1, color: a.$3, size: 22),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    a.$2,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textPrimary,
                      height: 1.1,
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off_outlined,
                size: 48, color: AppColors.textSecondary),
            const SizedBox(height: 12),
            Text(message,
                textAlign: TextAlign.center,
                style: const TextStyle(color: AppColors.textSecondary)),
            const SizedBox(height: 16),
            SgPrimaryButton(label: 'Retry', onPressed: onRetry),
          ],
        ),
      ),
    );
  }
}
