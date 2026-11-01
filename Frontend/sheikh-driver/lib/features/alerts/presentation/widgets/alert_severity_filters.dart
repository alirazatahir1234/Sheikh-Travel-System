import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';
import '../../domain/alert_display.dart';

/// Horizontal Critical / Warning / Info filter chips with counts.
class AlertSeverityFilters extends StatelessWidget {
  const AlertSeverityFilters({
    super.key,
    required this.selected,
    required this.criticalCount,
    required this.warningCount,
    required this.infoCount,
    required this.onToggle,
  });

  final Set<AlertDisplaySeverity> selected;
  final int criticalCount;
  final int warningCount;
  final int infoCount;
  final ValueChanged<AlertDisplaySeverity> onToggle;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 40,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        children: [
          _SeverityChip(
            label: 'Critical ($criticalCount)',
            severity: AlertDisplaySeverity.critical,
            color: AppColors.error,
            softBg: const Color(0xFFFEE2E2),
            icon: Icons.error_rounded,
            selected: selected.contains(AlertDisplaySeverity.critical),
            onTap: () => onToggle(AlertDisplaySeverity.critical),
          ),
          _SeverityChip(
            label: 'Warning ($warningCount)',
            severity: AlertDisplaySeverity.warning,
            color: AppColors.warning,
            softBg: const Color(0xFFFEF3C7),
            icon: Icons.warning_amber_rounded,
            selected: selected.contains(AlertDisplaySeverity.warning),
            onTap: () => onToggle(AlertDisplaySeverity.warning),
          ),
          _SeverityChip(
            label: 'Info ($infoCount)',
            severity: AlertDisplaySeverity.info,
            color: const Color(0xFF3B82F6),
            softBg: const Color(0xFFDBEAFE),
            icon: Icons.info_rounded,
            selected: selected.contains(AlertDisplaySeverity.info),
            onTap: () => onToggle(AlertDisplaySeverity.info),
          ),
        ],
      ),
    );
  }
}

class _SeverityChip extends StatelessWidget {
  const _SeverityChip({
    required this.label,
    required this.severity,
    required this.color,
    required this.softBg,
    required this.icon,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final AlertDisplaySeverity severity;
  final Color color;
  final Color softBg;
  final IconData icon;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(right: 8),
      child: Material(
        color: selected ? color : softBg,
        borderRadius: BorderRadius.circular(20),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(20),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(
                  selected ? Icons.check_circle_rounded : icon,
                  size: 16,
                  color: selected ? Colors.white : color,
                ),
                const SizedBox(width: 6),
                Text(
                  label,
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: selected ? Colors.white : color,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
