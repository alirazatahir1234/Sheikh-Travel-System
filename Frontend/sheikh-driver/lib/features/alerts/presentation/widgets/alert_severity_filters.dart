import 'package:flutter/material.dart';

import '../../domain/alert_display.dart';
import 'fleet_alerts_colors.dart';

/// Horizontal Critical / Warning / Info filter chips with counts (Image B).
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
      height: 42,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        children: [
          _SeverityChip(
            label: 'Critical ($criticalCount)',
            color: FleetAlertsColors.critical,
            softBg: FleetAlertsColors.criticalSoft,
            icon: Icons.priority_high_rounded,
            selected: selected.contains(AlertDisplaySeverity.critical),
            onTap: () => onToggle(AlertDisplaySeverity.critical),
          ),
          _SeverityChip(
            label: 'Warning ($warningCount)',
            color: FleetAlertsColors.warning,
            softBg: FleetAlertsColors.warningSoft,
            icon: Icons.warning_amber_rounded,
            selected: selected.contains(AlertDisplaySeverity.warning),
            onTap: () => onToggle(AlertDisplaySeverity.warning),
          ),
          _SeverityChip(
            label: 'Info ($infoCount)',
            color: FleetAlertsColors.info,
            softBg: FleetAlertsColors.infoSoft,
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
    required this.color,
    required this.softBg,
    required this.icon,
    required this.selected,
    required this.onTap,
  });

  final String label;
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
        color: softBg,
        borderRadius: BorderRadius.circular(22),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(22),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 160),
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(22),
              border: Border.all(
                color: color.withValues(alpha: selected ? 1 : 0.55),
                width: selected ? 1.8 : 1.2,
              ),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(icon, size: 15, color: color),
                const SizedBox(width: 5),
                Text(
                  label,
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: color,
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
