import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../domain/alert_display.dart';
import '../../domain/gps_alert_models.dart';

class TelematicsAlertCard extends StatelessWidget {
  const TelematicsAlertCard({
    super.key,
    required this.alert,
    required this.onOpen,
    required this.onAck,
    this.onCall,
    this.isDemo = false,
  });

  final GpsAlertEvent alert;
  final VoidCallback onOpen;
  final VoidCallback? onAck;
  final VoidCallback? onCall;
  final bool isDemo;

  Color get _rail {
    final s = alertDisplaySeverity(alert);
    return switch (s) {
      AlertDisplaySeverity.critical => AppColors.error,
      AlertDisplaySeverity.warning => AppColors.warning,
      AlertDisplaySeverity.resolved => AppColors.success,
      AlertDisplaySeverity.info => AppColors.info,
    };
  }

  @override
  Widget build(BuildContext context) {
    final title = alertTitle(alert);
    final desc = alert.message.trim().isNotEmpty
        ? alert.message.trim()
        : alertDescription(alert);
    final limit = parseSpeedLimitFromMessage(alert.message);
    final statusLabel = alert.isOpen
        ? (alert.isAcknowledged ? 'ACKNOWLEDGED' : 'UNRESOLVED')
        : alert.status.toUpperCase();
    final statusColor = alert.isOpen && !alert.isAcknowledged
        ? AppColors.error
        : AppColors.textSecondary;

    return SgCard(
      margin: const EdgeInsets.only(bottom: 10),
      padding: EdgeInsets.zero,
      onTap: onOpen,
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              width: 5,
              decoration: BoxDecoration(
                color: _rail,
                borderRadius: const BorderRadius.only(
                  topLeft: Radius.circular(AppRadii.md),
                  bottomLeft: Radius.circular(AppRadii.md),
                ),
              ),
            ),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 12, 12, 10),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Container(
                          width: 36,
                          height: 36,
                          decoration: BoxDecoration(
                            color: _rail.withValues(alpha: 0.12),
                            shape: BoxShape.circle,
                          ),
                          child: Icon(alertCategoryIcon(alert),
                              color: _rail, size: 18),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                alert.speed > 0 &&
                                        normalizeAlertEventType(
                                                alert.eventType) ==
                                            'speed_exceeded'
                                    ? '$title: ${alert.speed.toStringAsFixed(0)} km/h'
                                    : title,
                                style: const TextStyle(
                                  fontWeight: FontWeight.w800,
                                  fontSize: 14,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                desc,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: AppColors.textSecondary,
                                  fontSize: 12,
                                  height: 1.3,
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(width: 6),
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.end,
                          children: [
                            Text(
                              formatAlertRelativeTime(alert.timestamp),
                              style: const TextStyle(
                                fontSize: 11,
                                color: AppColors.textMuted,
                              ),
                            ),
                            if (alert.isUnread && alert.isOpen)
                              const Padding(
                                padding: EdgeInsets.only(top: 6),
                                child: Icon(Icons.circle,
                                    size: 8, color: AppColors.error),
                              ),
                            if (isDemo)
                              const Padding(
                                padding: EdgeInsets.only(top: 4),
                                child: Text(
                                  'DEMO',
                                  style: TextStyle(
                                    fontSize: 9,
                                    fontWeight: FontWeight.w800,
                                    color: AppColors.primary,
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ],
                    ),
                    const SizedBox(height: 10),
                    Row(
                      children: [
                        const Icon(Icons.local_shipping_outlined,
                            size: 14, color: AppColors.textMuted),
                        const SizedBox(width: 4),
                        Expanded(
                          child: Text(
                            alert.vehicleName ?? 'Vehicle #${alert.vehicleId}',
                            style: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        if (alert.driverName != null) ...[
                          const SizedBox(width: 8),
                          CircleAvatar(
                            radius: 10,
                            backgroundColor:
                                AppColors.primary.withValues(alpha: 0.15),
                            child: Text(
                              alert.driverName!.trim().isEmpty
                                  ? '?'
                                  : alert.driverName!.trim()[0].toUpperCase(),
                              style: const TextStyle(
                                fontSize: 10,
                                fontWeight: FontWeight.w800,
                                color: AppColors.primary,
                              ),
                            ),
                          ),
                          const SizedBox(width: 4),
                          Flexible(
                            child: Text(
                              alert.driverName!,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(fontSize: 12),
                            ),
                          ),
                          if (onCall != null)
                            IconButton(
                              visualDensity: VisualDensity.compact,
                              padding: EdgeInsets.zero,
                              constraints: const BoxConstraints(
                                minWidth: 28,
                                minHeight: 28,
                              ),
                              onPressed: onCall,
                              icon: const Icon(Icons.phone_in_talk_rounded,
                                  size: 16, color: AppColors.success),
                            ),
                        ],
                      ],
                    ),
                    if (alert.geofenceName != null ||
                        alert.latitude != 0 ||
                        alert.longitude != 0) ...[
                      const SizedBox(height: 6),
                      Row(
                        children: [
                          const Icon(Icons.location_on_outlined,
                              size: 14, color: AppColors.textMuted),
                          const SizedBox(width: 4),
                          Expanded(
                            child: Text(
                              alert.geofenceName ??
                                  '${alert.latitude.toStringAsFixed(4)}, ${alert.longitude.toStringAsFixed(4)}',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                fontSize: 11,
                                color: AppColors.textMuted,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                    const SizedBox(height: 10),
                    Row(
                      children: [
                        if (alert.speed > 0)
                          StatusBadge(
                            limit != null
                                ? '${alert.speed.toStringAsFixed(0)} / ${limit.toStringAsFixed(0)} km/h'
                                : '${alert.speed.toStringAsFixed(0)} km/h',
                            color: _rail,
                          ),
                        if (alert.speed > 0) const SizedBox(width: 6),
                        StatusBadge(statusLabel, color: statusColor),
                        const Spacer(),
                        if (onAck != null &&
                            (alert.canAcknowledge || isDemo) &&
                            !alert.isAcknowledged)
                          FilledButton.tonal(
                            onPressed: onAck,
                            style: FilledButton.styleFrom(
                              visualDensity: VisualDensity.compact,
                              padding: const EdgeInsets.symmetric(
                                  horizontal: 12, vertical: 6),
                            ),
                            child: const Text('Ack'),
                          ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
