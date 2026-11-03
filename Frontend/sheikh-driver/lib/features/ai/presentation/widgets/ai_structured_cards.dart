import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/constants/app_theme.dart';
import '../../domain/ai_assistant_theme.dart';
import '../../domain/ai_structured_payload.dart';

class AiFleetSummaryCard extends StatelessWidget {
  const AiFleetSummaryCard({super.key, required this.summary});

  final AiFleetStatusSummary summary;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Material(
      color: p.card,
      elevation: 0,
      borderRadius: BorderRadius.circular(AppRadii.lg),
      child: Container(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(AppRadii.lg),
          border: Border.all(color: p.border.withValues(alpha: 0.85)),
          boxShadow: AppShadows.card,
        ),
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Icon(Icons.directions_car_rounded,
                    size: 20, color: AiAssistantColors.heroBottom),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Fleet summary',
                    style: TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 15,
                      color: p.textPrimary,
                    ),
                  ),
                ),
                _Badge(label: '${summary.total} vehicles'),
              ],
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Expanded(
                  child: _MetricTile(
                    label: 'Online',
                    value: '${summary.online}',
                    icon: Icons.wifi_rounded,
                    color: AiAssistantColors.online,
                  ),
                ),
                Expanded(
                  child: _MetricTile(
                    label: 'Offline',
                    value: '${summary.offline}',
                    icon: Icons.wifi_off_rounded,
                    color: p.textMuted,
                  ),
                ),
                Expanded(
                  child: _MetricTile(
                    label: 'Moving',
                    value: '${summary.moving}',
                    icon: Icons.play_arrow_rounded,
                    color: AiAssistantColors.moving,
                  ),
                ),
                Expanded(
                  child: _MetricTile(
                    label: 'Idle',
                    value: '${summary.idle}',
                    icon: Icons.pause_rounded,
                    color: AiAssistantColors.idle,
                  ),
                ),
              ],
            ),
            if (summary.overallMessage != null &&
                summary.overallMessage!.trim().isNotEmpty) ...[
              const SizedBox(height: 12),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.check_circle_rounded,
                      size: 18, color: AiAssistantColors.online),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      summary.overallMessage!,
                      style: TextStyle(
                        fontSize: 12,
                        height: 1.35,
                        color: p.textSecondary,
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class AiAlertInsightCard extends StatelessWidget {
  const AiAlertInsightCard({super.key, required this.alert});

  final AiAssistantAlertCard alert;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final critical =
        alert.severity.toLowerCase().contains('crit') ||
            alert.severity.toLowerCase().contains('high');
    return Material(
      color: p.card,
      borderRadius: BorderRadius.circular(AppRadii.lg),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadii.lg),
        onTap: () => context.push('/alerts'),
        child: Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.lg),
            border: Border.all(color: p.border.withValues(alpha: 0.85)),
            boxShadow: AppShadows.card,
          ),
          padding: const EdgeInsets.fromLTRB(14, 12, 10, 12),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Wrap(
                      spacing: 8,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      children: [
                        Text(
                          alert.title,
                          style: TextStyle(
                            fontWeight: FontWeight.w800,
                            fontSize: 14,
                            color: p.textPrimary,
                          ),
                        ),
                        _Badge(
                          label: alert.severity,
                          tone: critical
                              ? AiAssistantColors.critical
                              : AiAssistantColors.idle,
                        ),
                      ],
                    ),
                    if (alert.vehicleLabel != null) ...[
                      const SizedBox(height: 6),
                      Text.rich(
                        TextSpan(
                          text: 'Vehicle: ',
                          style: TextStyle(
                            fontSize: 12,
                            color: p.textSecondary,
                          ),
                          children: [
                            TextSpan(
                              text: alert.vehicleLabel,
                              style: const TextStyle(
                                color: AppColors.info,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                    if (alert.location != null) ...[
                      const SizedBox(height: 2),
                      Text(
                        alert.location!,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(fontSize: 12, color: p.textSecondary),
                      ),
                    ],
                    if (alert.timeLabel != null) ...[
                      const SizedBox(height: 2),
                      Text(
                        alert.timeLabel!,
                        style: TextStyle(fontSize: 11, color: p.textMuted),
                      ),
                    ],
                  ],
                ),
              ),
              Icon(Icons.chevron_right_rounded, color: p.textMuted),
            ],
          ),
        ),
      ),
    );
  }
}

class _Badge extends StatelessWidget {
  const _Badge({required this.label, this.tone});

  final String label;
  final Color? tone;

  @override
  Widget build(BuildContext context) {
    final c = tone ?? AiAssistantColors.heroBottom;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: c.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(AppRadii.pill),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w700,
          color: c,
        ),
      ),
    );
  }
}

class _MetricTile extends StatelessWidget {
  const _MetricTile({
    required this.label,
    required this.value,
    required this.icon,
    required this.color,
  });

  final String label;
  final String value;
  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Column(
      children: [
        Icon(icon, size: 18, color: color),
        const SizedBox(height: 4),
        Text(
          value,
          style: TextStyle(
            fontWeight: FontWeight.w800,
            fontSize: 16,
            color: p.textPrimary,
          ),
        ),
        Text(
          label,
          style: TextStyle(fontSize: 10, color: p.textSecondary),
        ),
      ],
    );
  }
}
