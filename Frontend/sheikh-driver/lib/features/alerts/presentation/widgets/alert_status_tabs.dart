import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';
import '../alerts_notifier.dart';

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
      padding: const EdgeInsets.fromLTRB(16, 4, 16, 12),
      child: Container(
        padding: const EdgeInsets.all(4),
        decoration: BoxDecoration(
          color: const Color(0xFFE8EEF2),
          borderRadius: BorderRadius.circular(14),
        ),
        child: Row(
          children: [
            Expanded(
              child: _TabButton(
                label: activeCount == null
                    ? 'Active'
                    : 'Active ($activeCount)',
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
        color: selected ? AppColors.primary : Colors.transparent,
        borderRadius: BorderRadius.circular(11),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(11),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 11),
            child: Text(
              label,
              textAlign: TextAlign.center,
              style: TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w700,
                color: selected ? Colors.white : AppColors.navy,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
