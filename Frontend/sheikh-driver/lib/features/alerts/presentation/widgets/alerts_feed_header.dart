import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';
import '../../domain/alert_display.dart';

class CriticalAttentionBanner extends StatelessWidget {
  const CriticalAttentionBanner({
    super.key,
    required this.count,
    required this.onReview,
  });

  final int count;
  final VoidCallback onReview;

  @override
  Widget build(BuildContext context) {
    if (count <= 0) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: const Color(0xFFFEF2F2),
          borderRadius: BorderRadius.circular(AppRadii.md),
          border: Border.all(color: AppColors.error.withValues(alpha: 0.35)),
        ),
        child: Row(
          children: [
            const Icon(Icons.warning_amber_rounded, color: AppColors.error),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '$count Critical Alerts Require Attention',
                    style: const TextStyle(
                      color: AppColors.error,
                      fontWeight: FontWeight.w800,
                      fontSize: 13,
                    ),
                  ),
                  const SizedBox(height: 2),
                  const Text(
                    'Overspeeding & geofence violations flagged by telematics.',
                    style: TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 11,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            FilledButton(
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.error,
                foregroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                minimumSize: Size.zero,
                tapTargetSize: MaterialTapTargetSize.shrinkWrap,
              ),
              onPressed: onReview,
              child: const Text('Review'),
            ),
          ],
        ),
      ),
    );
  }
}

class AlertsSummaryChips extends StatelessWidget {
  const AlertsSummaryChips({super.key, required this.summary});

  final AlertsFeedSummary summary;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: Row(
        children: [
          Expanded(
            child: _Chip(
              label: 'Total',
              value: '${summary.total}',
              border: AppColors.border,
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: _Chip(
              label: 'Critical',
              value: '${summary.critical}',
              border: AppColors.error.withValues(alpha: 0.55),
              valueColor: AppColors.error,
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: _Chip(
              label: 'Warning',
              value: '${summary.warning}',
              border: AppColors.warning.withValues(alpha: 0.55),
              valueColor: AppColors.warning,
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: _Chip(
              label: 'Closed',
              value: '${summary.closed}',
              border: AppColors.success.withValues(alpha: 0.55),
              valueColor: AppColors.success,
            ),
          ),
        ],
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({
    required this.label,
    required this.value,
    required this.border,
    this.valueColor,
  });

  final String label;
  final String value;
  final Color border;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 8),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(AppRadii.md),
        border: Border.all(color: border),
      ),
      child: Column(
        children: [
          Text(
            value,
            style: TextStyle(
              fontWeight: FontWeight.w800,
              fontSize: 16,
              color: valueColor ?? AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            style: const TextStyle(fontSize: 11, color: AppColors.textMuted),
          ),
        ],
      ),
    );
  }
}

class AlertsCategoryPills extends StatelessWidget {
  const AlertsCategoryPills({
    super.key,
    required this.selected,
    required this.counts,
    required this.onSelected,
  });

  final AlertCategory selected;
  final Map<AlertCategory, int> counts;
  final ValueChanged<AlertCategory> onSelected;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 44,
      child: ListView.separated(
        padding: const EdgeInsets.symmetric(horizontal: 16),
        scrollDirection: Axis.horizontal,
        itemCount: AlertCategoryX.pillOrder.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (_, i) {
          final cat = AlertCategoryX.pillOrder[i];
          final count = counts[cat] ?? 0;
          final isOn = selected == cat;
          final accent = cat == AlertCategory.critical
              ? AppColors.error
              : AppColors.primary;
          return FilterChip(
            selected: isOn,
            showCheckmark: false,
            label: Text('${cat.label} $count'),
            selectedColor: accent,
            labelStyle: TextStyle(
              color: isOn ? Colors.white : AppColors.textSecondary,
              fontWeight: FontWeight.w700,
              fontSize: 12,
            ),
            side: BorderSide(
              color: isOn
                  ? accent
                  : (cat == AlertCategory.critical
                      ? AppColors.error.withValues(alpha: 0.4)
                      : AppColors.border),
            ),
            backgroundColor: Colors.white,
            onSelected: (_) => onSelected(cat),
          );
        },
      ),
    );
  }
}

class AlertsQuickActions extends StatelessWidget {
  const AlertsQuickActions({
    super.key,
    required this.onSimulate,
    required this.onAcknowledgeAll,
  });

  final VoidCallback onSimulate;
  final VoidCallback onAcknowledgeAll;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: Row(
        children: [
          Expanded(
            child: OutlinedButton.icon(
              onPressed: onSimulate,
              icon: const Icon(Icons.bolt_rounded, size: 18),
              label: const Text('Simulate Incident'),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.primary,
                side: const BorderSide(color: AppColors.primary),
              ),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: TextButton.icon(
              onPressed: onAcknowledgeAll,
              icon: const Icon(Icons.done_all_rounded, size: 18),
              label: const Text('Acknowledge All'),
              style: TextButton.styleFrom(
                foregroundColor: AppColors.textSecondary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
