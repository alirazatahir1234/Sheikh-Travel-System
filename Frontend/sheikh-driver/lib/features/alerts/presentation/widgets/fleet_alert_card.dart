import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';
import '../../domain/alert_display.dart';

/// Compact Fleet Alerts list card matching the product mockup.
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
        ? AppColors.success
        : switch (bucket) {
            AlertDisplaySeverity.critical => AppColors.error,
            AlertDisplaySeverity.warning => AppColors.warning,
            AlertDisplaySeverity.info => const Color(0xFF3B82F6),
            AlertDisplaySeverity.resolved => AppColors.success,
          };

    final iconBg = showAsResolved
        ? AppColors.success.withValues(alpha: 0.14)
        : stripeColor.withValues(alpha: 0.12);
    final iconColor = showAsResolved ? AppColors.success : stripeColor;
    final icon = showAsResolved
        ? Icons.check_rounded
        : switch (bucket) {
            AlertDisplaySeverity.critical => Icons.error_rounded,
            AlertDisplaySeverity.warning => Icons.warning_amber_rounded,
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
      padding: const EdgeInsets.only(bottom: 10),
      child: Material(
        color: Colors.white,
        elevation: 0,
        borderRadius: BorderRadius.circular(18),
        shadowColor: Colors.black.withValues(alpha: 0.08),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(18),
          child: Ink(
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(18),
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.05),
                  blurRadius: 10,
                  offset: const Offset(0, 3),
                ),
              ],
            ),
            child: IntrinsicHeight(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Container(
                    width: 5,
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
                      padding: const EdgeInsets.fromLTRB(12, 12, 8, 12),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Container(
                            width: 36,
                            height: 36,
                            decoration: BoxDecoration(
                              color: iconBg,
                              shape: BoxShape.circle,
                            ),
                            child: Icon(icon, color: iconColor, size: 18),
                          ),
                          const SizedBox(width: 10),
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
                                          fontSize: 15,
                                          fontWeight: FontWeight.w800,
                                          color: Color(0xFF0B253B),
                                          height: 1.2,
                                        ),
                                      ),
                                    ),
                                    const SizedBox(width: 8),
                                    Text(
                                      time,
                                      style: const TextStyle(
                                        fontSize: 11,
                                        fontWeight: FontWeight.w500,
                                        color: AppColors.textSecondary,
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  description,
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(
                                    fontSize: 13,
                                    height: 1.3,
                                    color: AppColors.textSecondary,
                                  ),
                                ),
                                const SizedBox(height: 10),
                                Wrap(
                                  spacing: 6,
                                  runSpacing: 6,
                                  children: [
                                    _MetaPill(
                                      icon: Icons.directions_car_outlined,
                                      label: vehicle,
                                    ),
                                    _MetaPill(
                                      icon: Icons.location_on_outlined,
                                      label: location,
                                    ),
                                  ],
                                ),
                              ],
                            ),
                          ),
                          const Padding(
                            padding: EdgeInsets.only(left: 2, top: 8),
                            child: Icon(
                              Icons.chevron_right_rounded,
                              color: AppColors.textMuted,
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
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
      decoration: BoxDecoration(
        color: const Color(0xFFF1F5F9),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 13, color: AppColors.textSecondary),
          const SizedBox(width: 4),
          ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 140),
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w600,
                color: Color(0xFF0B253B),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
