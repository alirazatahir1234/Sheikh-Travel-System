import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../data/earnings_api.dart';
import '../domain/earnings_models.dart';

final earningsProvider = FutureProvider.autoDispose<EarningsSummary>(
  (ref) => ref.read(earningsApiProvider).getEarnings(),
);

class EarningsScreen extends ConsumerWidget {
  const EarningsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(earningsProvider);
    final fmt = NumberFormat('#,##0');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Earnings'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.invalidate(earningsProvider),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$e', textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () => ref.invalidate(earningsProvider),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (summary) => RefreshIndicator(
          onRefresh: () async => ref.invalidate(earningsProvider),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _PeriodGrid(summary: summary, fmt: fmt),
              const SizedBox(height: 16),
              _PaidPendingRow(summary: summary, fmt: fmt),
              const SizedBox(height: 16),
              _ChartCard(summary: summary, fmt: fmt),
              const SizedBox(height: 16),
              _BreakdownCard(summary: summary, fmt: fmt),
              const SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }
}

class _PeriodGrid extends StatelessWidget {
  const _PeriodGrid({required this.summary, required this.fmt});
  final EarningsSummary summary;
  final NumberFormat fmt;

  @override
  Widget build(BuildContext context) {
    return GridView.count(
      crossAxisCount: 3,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      crossAxisSpacing: 10,
      mainAxisSpacing: 10,
      childAspectRatio: 0.95,
      children: [
        _MoneyTile(
          label: 'Today',
          amount: summary.today,
          color: AppColors.primary,
          fmt: fmt,
        ),
        _MoneyTile(
          label: 'This week',
          amount: summary.thisWeek,
          color: AppColors.accent,
          fmt: fmt,
        ),
        _MoneyTile(
          label: 'This month',
          amount: summary.thisMonth,
          color: AppColors.primaryLight,
          fmt: fmt,
        ),
      ],
    );
  }
}

class _MoneyTile extends StatelessWidget {
  const _MoneyTile({
    required this.label,
    required this.amount,
    required this.color,
    required this.fmt,
  });

  final String label;
  final double amount;
  final Color color;
  final NumberFormat fmt;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              label,
              style: const TextStyle(
                fontSize: 12,
                color: AppColors.textSecondary,
              ),
            ),
            const Spacer(),
            Text(
              'PKR',
              style: TextStyle(
                fontSize: 11,
                color: color.withValues(alpha: 0.8),
                fontWeight: FontWeight.w600,
              ),
            ),
            Text(
              fmt.format(amount),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w700,
                color: color,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _PaidPendingRow extends StatelessWidget {
  const _PaidPendingRow({required this.summary, required this.fmt});
  final EarningsSummary summary;
  final NumberFormat fmt;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: _StatusAmountCard(
            label: 'Paid',
            amount: summary.paid,
            icon: Icons.check_circle_outline,
            color: AppColors.success,
            fmt: fmt,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: _StatusAmountCard(
            label: 'Pending',
            amount: summary.pending,
            icon: Icons.schedule,
            color: AppColors.warning,
            fmt: fmt,
          ),
        ),
      ],
    );
  }
}

class _StatusAmountCard extends StatelessWidget {
  const _StatusAmountCard({
    required this.label,
    required this.amount,
    required this.icon,
    required this.color,
    required this.fmt,
  });

  final String label;
  final double amount;
  final IconData icon;
  final Color color;
  final NumberFormat fmt;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          children: [
            Icon(icon, color: color, size: 28),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  Text(
                    'PKR ${fmt.format(amount)}',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: color,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ChartCard extends StatelessWidget {
  const _ChartCard({required this.summary, required this.fmt});
  final EarningsSummary summary;
  final NumberFormat fmt;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Last 7 days',
              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
            ),
            const SizedBox(height: 4),
            Text(
              'PKR ${fmt.format(summary.thisWeek)} this week',
              style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 12,
              ),
            ),
            const SizedBox(height: 16),
            SizedBox(
              height: 140,
              child: CustomPaint(
                painter: _BarChartPainter(days: summary.daily),
                child: const SizedBox.expand(),
              ),
            ),
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: summary.daily
                  .map(
                    (d) => Text(
                      DateFormat('E').format(d.date).substring(0, 1),
                      style: const TextStyle(
                        fontSize: 11,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  )
                  .toList(),
            ),
          ],
        ),
      ),
    );
  }
}

class _BarChartPainter extends CustomPainter {
  _BarChartPainter({required this.days});
  final List<EarningsDay> days;

  @override
  void paint(Canvas canvas, Size size) {
    if (days.isEmpty) return;

    final maxVal = days.map((d) => d.amount).fold<double>(0, (a, b) => a > b ? a : b);
    final peak = maxVal <= 0 ? 1.0 : maxVal;
    final barWidth = size.width / (days.length * 1.8);
    final gap = barWidth * 0.8;
    final paint = Paint()
      ..color = AppColors.primaryLight
      ..style = PaintingStyle.fill;

    for (var i = 0; i < days.length; i++) {
      final h = (days[i].amount / peak) * size.height;
      final x = i * (barWidth + gap) + gap * 0.5;
      final rect = RRect.fromRectAndRadius(
        Rect.fromLTWH(x, size.height - h, barWidth, h < 2 && days[i].amount > 0 ? 2 : h),
        const Radius.circular(4),
      );
      canvas.drawRRect(rect, paint);
    }
  }

  @override
  bool shouldRepaint(covariant _BarChartPainter oldDelegate) =>
      oldDelegate.days != days;
}

class _BreakdownCard extends StatelessWidget {
  const _BreakdownCard({required this.summary, required this.fmt});
  final EarningsSummary summary;
  final NumberFormat fmt;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Breakdown',
              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
            ),
            const SizedBox(height: 12),
            _BreakdownRow(
              icon: Icons.route,
              label: 'Completed trips',
              value: '${summary.completedTripCount}',
            ),
            _BreakdownRow(
              icon: Icons.straighten,
              label: 'Distance',
              value: '${summary.distanceKm.toStringAsFixed(1)} km',
            ),
            _BreakdownRow(
              icon: Icons.timer_outlined,
              label: 'Hours worked',
              value: '${summary.hoursWorked.toStringAsFixed(1)} h',
            ),
            _BreakdownRow(
              icon: Icons.payments_outlined,
              label: 'Allowances (range)',
              value: 'PKR ${fmt.format(summary.tripAllowances)}',
            ),
            _BreakdownRow(
              icon: Icons.local_gas_station,
              label: 'Fuel cost',
              value: 'PKR ${fmt.format(summary.fuelCost)}',
              isLast: true,
            ),
          ],
        ),
      ),
    );
  }
}

class _BreakdownRow extends StatelessWidget {
  const _BreakdownRow({
    required this.icon,
    required this.label,
    required this.value,
    this.isLast = false,
  });

  final IconData icon;
  final String label;
  final String value;
  final bool isLast;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 10),
          child: Row(
            children: [
              Icon(icon, size: 20, color: AppColors.textSecondary),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  label,
                  style: const TextStyle(color: AppColors.textPrimary),
                ),
              ),
              Text(
                value,
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
            ],
          ),
        ),
        if (!isLast) const Divider(height: 1),
      ],
    );
  }
}
