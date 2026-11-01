import 'package:flutter/material.dart';

import '../alerts_notifier.dart';
import 'fleet_alerts_colors.dart';

/// Active / Resolved segmented control (Fleet Alerts mockup).
class AlertStatusTabs extends StatelessWidget {
  const AlertStatusTabs({
    super.key,
    required this.selected,
    required this.activeCount,
    required this.resolvedCount,
    required this.onChanged,
  });

  final FleetAlertStatusTab selected;
  final int? activeCount;
  final int? resolvedCount;
  final ValueChanged<FleetAlertStatusTab> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 2, 16, 12),
      child: Container(
        padding: const EdgeInsets.all(4),
        decoration: BoxDecoration(
          color: FleetAlertsColors.mutedFill,
          borderRadius: BorderRadius.circular(14),
        ),
        child: Row(
          children: [
            Expanded(
              child: _TabButton(
                label: activeCount == null ? 'Active' : 'Active ($activeCount)',
                selected: selected == FleetAlertStatusTab.active,
                onTap: () => onChanged(FleetAlertStatusTab.active),
              ),
            ),
            Expanded(
              child: _TabButton(
                label: resolvedCount == null
                    ? 'Resolved'
                    : 'Resolved ($resolvedCount)',
                selected: selected == FleetAlertStatusTab.resolved,
                onTap: () => onChanged(FleetAlertStatusTab.resolved),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TabButton extends StatelessWidget {
  const _TabButton({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOut,
      child: Material(
        color: selected ? FleetAlertsColors.teal : Colors.transparent,
        borderRadius: BorderRadius.circular(11),
        elevation: selected ? 1 : 0,
        shadowColor: FleetAlertsColors.teal.withValues(alpha: 0.35),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(11),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 12),
            child: Text(
              label,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w700,
                color: selected ? Colors.white : FleetAlertsColors.navy,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
