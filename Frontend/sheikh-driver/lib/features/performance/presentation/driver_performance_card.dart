import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import 'driver_score_notifier.dart';

class DriverPerformanceCard extends ConsumerWidget {
  const DriverPerformanceCard({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(myDriverGpsScoreProvider);
    return SgCard(
      onTap: () => context.push('/performance'),
      child: async.when(
        loading: () => const SgSkeleton(height: 72, radius: AppRadii.md),
        error: (_, __) => const Text('Performance score unavailable'),
        data: (s) => Row(
          children: [
            const SgIconWell(icon: Icons.speed_rounded),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'My performance',
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      color: context.sg.textPrimary,
                    ),
                  ),
                  Text(
                    s.rank > 0
                        ? 'Score ${s.score} · Rank #${s.rank} of ${s.totalDrivers}'
                        : 'Score ${s.score} · ${s.rating}',
                    style: TextStyle(
                      fontSize: 12,
                      color: context.sg.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            Icon(Icons.chevron_right_rounded, color: context.sg.textMuted),
          ],
        ),
      ),
    );
  }
}
