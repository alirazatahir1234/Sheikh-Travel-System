import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';
import '../../domain/alert_display.dart';
import '../../domain/gps_alert_models.dart';

/// Compact alert row matching the Fleet Alerts mockup (not oversized cards).
class FleetAlertRow extends StatelessWidget {
  const FleetAlertRow({
    super.key,
    required this.incident,
    required this.selected,
    required this.selectionMode,
    required this.onTap,
    required this.onToggleSelect,
    this.expanded = false,
    this.onToggleExpand,
  });

  final AlertIncident incident;
  final bool selected;
  final bool selectionMode;
  final VoidCallback onTap;
  final VoidCallback onToggleSelect;
  final bool expanded;
  final VoidCallback? onToggleExpand;

  @override
  Widget build(BuildContext context) {
    final primary = incident.primary;
    final sev = alertDisplaySeverity(
      primary,
      recovered: incident.recovered && incident.isOfflineIncident,
    );
    final sevColor = switch (sev) {
      AlertDisplaySeverity.critical => AppColors.error,
      AlertDisplaySeverity.warning => AppColors.warning,
      AlertDisplaySeverity.info => AppColors.info,
      AlertDisplaySeverity.resolved => AppColors.success,
    };
    final isGroup = incident.isGrouped;
    final iconBg = isGroup
        ? AppColors.primary.withValues(alpha: 0.12)
        : sevColor.withValues(alpha: 0.12);
    final iconColor = isGroup ? AppColors.primary : sevColor;
    final icon = isGroup
        ? Icons.layers_rounded
        : alertTypeSquareIcon(primary.eventType);

    final vehicle = primary.vehicleLabel;
    final driver = primary.driverName?.trim();
    final metaLine = [
      vehicle,
      if (driver != null && driver.isNotEmpty) driver,
    ].join(' • ');

    final ackLabel = isGroup
        ? (incident.isAcknowledged
            ? 'Acknowledged'
            : incident.isAssigned
                ? 'Assigned • ${incident.assignedToName ?? ''}'
                : 'Unacknowledged')
        : alertAckStatusLabel(primary);
    final ackColor = isGroup
        ? (incident.isAcknowledged
            ? AppColors.success
            : incident.isAssigned
                ? AppColors.warning
                : AppColors.error)
        : alertAckStatusColor(primary);

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        onLongPress: onToggleSelect,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
          decoration: const BoxDecoration(
            border: Border(
              bottom: BorderSide(color: Color(0xFFE8EEF2), width: 1),
            ),
          ),
          child: Column(
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (selectionMode) ...[
                    Padding(
                      padding: const EdgeInsets.only(right: 8, top: 6),
                      child: SizedBox(
                        width: 22,
                        height: 22,
                        child: Checkbox(
                          value: selected,
                          onChanged: (_) => onToggleSelect(),
                          activeColor: AppColors.primary,
                          materialTapTargetSize:
                              MaterialTapTargetSize.shrinkWrap,
                          visualDensity: VisualDensity.compact,
                        ),
                      ),
                    ),
                  ],
                  Container(
                    width: 40,
                    height: 40,
                    decoration: BoxDecoration(
                      color: iconBg,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Icon(icon, color: iconColor, size: 20),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          isGroup ? incident.groupTitle : alertTitle(primary),
                          style: const TextStyle(
                            fontWeight: FontWeight.w700,
                            fontSize: 14,
                            color: AppColors.navy,
                            height: 1.2,
                          ),
                        ),
                        const SizedBox(height: 2),
                        if (!isGroup)
                          Text(
                            metaLine,
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.textSecondary,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        const SizedBox(height: 2),
                        Text(
                          incident.groupSubtitle,
                          style: const TextStyle(
                            fontSize: 12,
                            color: AppColors.textMuted,
                          ),
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 8),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text(
                        formatAlertClock(primary.timestamp),
                        style: const TextStyle(
                          fontSize: 11,
                          color: AppColors.textMuted,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      const SizedBox(height: 4),
                      _SeverityTag(severity: sev, color: sevColor),
                      const SizedBox(height: 4),
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Container(
                            width: 6,
                            height: 6,
                            decoration: BoxDecoration(
                              color: ackColor,
                              shape: BoxShape.circle,
                            ),
                          ),
                          const SizedBox(width: 4),
                          Text(
                            ackLabel,
                            style: TextStyle(
                              fontSize: 10,
                              fontWeight: FontWeight.w600,
                              color: ackColor,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                  if (isGroup && onToggleExpand != null)
                    IconButton(
                      visualDensity: VisualDensity.compact,
                      padding: EdgeInsets.zero,
                      constraints: const BoxConstraints(
                        minWidth: 28,
                        minHeight: 28,
                      ),
                      onPressed: onToggleExpand,
                      icon: Icon(
                        expanded
                            ? Icons.expand_less_rounded
                            : Icons.expand_more_rounded,
                        color: AppColors.textMuted,
                      ),
                    )
                  else
                    const Padding(
                      padding: EdgeInsets.only(left: 2, top: 2),
                      child: Icon(
                        Icons.chevron_right_rounded,
                        color: AppColors.textMuted,
                        size: 20,
                      ),
                    ),
                ],
              ),
              if (isGroup && expanded) ...[
                const SizedBox(height: 8),
                ...incident.allEvents.map(
                  (e) => _NestedEventTile(event: e),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _SeverityTag extends StatelessWidget {
  const _SeverityTag({required this.severity, required this.color});

  final AlertDisplaySeverity severity;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        alertSeverityLabel(severity),
        style: TextStyle(
          fontSize: 9,
          fontWeight: FontWeight.w800,
          letterSpacing: 0.4,
          color: color,
        ),
      ),
    );
  }
}

class _NestedEventTile extends StatelessWidget {
  const _NestedEventTile({required this.event});

  final GpsAlertEvent event;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(left: 50, bottom: 6),
      child: Row(
        children: [
          Icon(
            alertTypeSquareIcon(event.eventType),
            size: 14,
            color: AppColors.textMuted,
          ),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              '${alertTitle(event)} · ${formatAlertClock(event.timestamp)}',
              style: const TextStyle(
                fontSize: 11,
                color: AppColors.textSecondary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
