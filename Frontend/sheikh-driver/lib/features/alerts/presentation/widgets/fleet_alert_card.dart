import 'package:flutter/material.dart';

import '../../domain/alert_display.dart';
import 'fleet_alerts_colors.dart';

/// Compact Fleet Alerts list card matching the product mockup (Image B).
class FleetAlertCard extends StatelessWidget {
  const FleetAlertCard({
    super.key,
    required this.incident,
    required this.onTap,
    this.showAsResolved = false,
  });

  final AlertIncident incident;
  final VoidCallback onTap;
  final bool showAsResolved;

  @override
  Widget build(BuildContext context) {
    final primary = incident.primary;
    final bucket = alertSeverityBucket(primary);
    final stripeColor = showAsResolved
        ? FleetAlertsColors.success
        : switch (bucket) {
            AlertDisplaySeverity.critical => FleetAlertsColors.critical,
            AlertDisplaySeverity.warning => FleetAlertsColors.warning,
            AlertDisplaySeverity.info => FleetAlertsColors.info,
            AlertDisplaySeverity.resolved => FleetAlertsColors.success,
          };

    final icon = showAsResolved
        ? Icons.check_rounded
        : switch (bucket) {
            AlertDisplaySeverity.critical => Icons.priority_high_rounded,
            AlertDisplaySeverity.warning => Icons.priority_high_rounded,
            AlertDisplaySeverity.info => Icons.info_rounded,
            AlertDisplaySeverity.resolved => Icons.check_rounded,
          };

    final title = showAsResolved
        ? '${incident.isGrouped ? incident.groupTitle : alertTitle(primary)} (Resolved)'
        : (incident.isGrouped ? incident.groupTitle : alertTitle(primary));

    final description = showAsResolved
        ? (primary.resolutionNotes?.trim().isNotEmpty == true
            ? primary.resolutionNotes!.trim()
            : incident.groupSubtitle)
        : incident.groupSubtitle;

    final time = formatAlertRelativeTimeVerbose(
      showAsResolved
          ? (primary.resolvedAt ??
              incident.endedAt ??
              primary.timestamp)
          : primary.timestamp,
    );

    final vehicle = primary.vehicleLabel;
    final location = alertLocationLabel(primary);

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(18),
          child: Ink(
            decoration: BoxDecoration(
              color: FleetAlertsColors.card,
              borderRadius: BorderRadius.circular(18),
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.06),
                  blurRadius: 12,
                  offset: const Offset(0, 3),
                ),
              ],
            ),
            child: IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Container(
                    width: 6,
                    decoration: BoxDecoration(
                      color: stripeColor,
                      borderRadius: const BorderRadius.only(
                        topLeft: Radius.circular(18),
                        bottomLeft: Radius.circular(18),
                      ),
                    ),
                  ),
                  Expanded(
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(12, 14, 6, 14),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Container(
                            width: 38,
                            height: 38,
                            decoration: BoxDecoration(
                              color: stripeColor,
                              shape: BoxShape.circle,
                            ),
                            child: Icon(icon, color: Colors.white, size: 20),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Expanded(
                                      child: Text(
                                        title,
                                        style: const TextStyle(
                                          fontSize: 15.5,
                                          fontWeight: FontWeight.w800,
                                          color: FleetAlertsColors.navy,
                                          height: 1.2,
                                          letterSpacing: -0.2,
                                        ),
                                      ),
                                    ),
                                    const SizedBox(width: 8),
                                    Text(
                                      time,
                                      style: const TextStyle(
                                        fontSize: 11,
                                        fontWeight: FontWeight.w500,
                                        color: FleetAlertsColors.secondary,
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 5),
                                Text(
                                  description,
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(
                                    fontSize: 13,
                                    height: 1.35,
                                    color: FleetAlertsColors.secondary,
                                  ),
                                ),
                                const SizedBox(height: 11),
                                Wrap(
                                  spacing: 6,
                                  runSpacing: 6,
                                  children: [
                                    _MetaPill(
                                      icon: Icons.directions_car_filled_rounded,
                                      label: vehicle,
                                    ),
                                    _MetaPill(
                                      icon: Icons.location_on_rounded,
                                      label: location,
                                    ),
                                  ],
                                ),
                              ],
                            ),
                          ),
                          const Padding(
                            padding: EdgeInsets.only(left: 2, top: 6),
                            child: Icon(
                              Icons.chevron_right_rounded,
                              color: Color(0xFFB8C4CE),
                              size: 22,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _MetaPill extends StatelessWidget {
  const _MetaPill({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: FleetAlertsColors.pillFill,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 13, color: FleetAlertsColors.secondary),
          const SizedBox(width: 5),
          ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 140),
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontSize: 11.5,
                fontWeight: FontWeight.w600,
                color: FleetAlertsColors.secondary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
