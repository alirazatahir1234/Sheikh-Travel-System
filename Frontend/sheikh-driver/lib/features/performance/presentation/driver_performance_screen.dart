import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import 'driver_score_notifier.dart';

class DriverPerformanceScreen extends ConsumerWidget {
  const DriverPerformanceScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(myDriverGpsScoreProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('My Performance')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => SgErrorView(
          message: formatDioError(e),
          onRetry: () => ref.invalidate(myDriverGpsScoreProvider),
        ),
        data: (s) => ListView(
          padding: AppSpacing.screen,
          children: [
            _ScoreHero(score: s),
            const SizedBox(height: AppSpacing.lg),
            _MetricRow(label: 'Fleet rank', value: s.rank > 0 ? '#${s.rank} of ${s.totalDrivers}' : '—'),
            _MetricRow(label: 'Safety', value: '${s.safetyScore}'),
            _MetricRow(label: 'Fuel efficiency', value: '${s.fuelEfficiencyScore}'),
            _MetricRow(label: 'On-time delivery', value: '${s.onTimeScore}'),
            _MetricRow(label: 'Trips in period', value: '${s.tripCount}'),
            if (s.rewardEligible)
              _NoteCard(
                color: AppColors.success,
                text: 'Top performer — you qualify for fleet rewards this period.',
              ),
            if (s.needsTraining)
              _NoteCard(
                color: AppColors.warning,
                text: 'Coaching recommended — review safety and on-time targets with dispatch.',
              ),
            if (s.isPartial)
              _NoteCard(
                color: AppColors.info,
                text: 'Some telematics inputs were unavailable; score uses trips and alerts only.',
              ),
          ],
        ),
      ),
    );
  }
}

class _ScoreHero extends StatelessWidget {
  const _ScoreHero({required this.score});
  final dynamic score;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.lg),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppRadii.lg),
        gradient: LinearGradient(
          colors: [
            AppColors.primary.withValues(alpha: 0.12),
            AppColors.accent.withValues(alpha: 0.08),
          ],
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Overall score', style: Theme.of(context).textTheme.labelLarge),
          Text(
            '${score.score}',
            style: Theme.of(context).textTheme.displaySmall?.copyWith(
                  fontWeight: FontWeight.w800,
                  color: AppColors.primary,
                ),
          ),
          Text(score.rating, style: Theme.of(context).textTheme.titleMedium),
        ],
      ),
    );
  }
}

class _NoteCard extends StatelessWidget {
  const _NoteCard({required this.color, required this.text});
  final Color color;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: AppSpacing.sm),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(AppSpacing.md),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AppRadii.md),
          border: Border.all(color: color.withValues(alpha: 0.45)),
          color: color.withValues(alpha: 0.08),
        ),
        child: Text(text),
      ),
    );
  }
}

class _MetricRow extends StatelessWidget {
  const _MetricRow({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Expanded(child: Text(label, style: const TextStyle(fontWeight: FontWeight.w600))),
          Text(value),
        ],
      ),
    );
  }
}
