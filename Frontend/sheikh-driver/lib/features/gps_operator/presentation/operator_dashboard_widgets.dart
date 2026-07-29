import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../alerts/domain/gps_alert_models.dart';
import '../../gps_operator/domain/operator_dashboard_models.dart';

class GpsExceptionKpiGrid extends StatelessWidget {
  const GpsExceptionKpiGrid({super.key, required this.summary});

  final GpsOperatorSummary summary;

  @override
  Widget build(BuildContext context) {
    final cells = [
      _Cell('Online', '${summary.online}', AppColors.success, '/fleet'),
      _Cell('Offline', '${summary.offline}', AppColors.error, '/fleet'),
      _Cell('Moving', '${summary.moving}', AppColors.primary, '/fleet/map'),
      _Cell('Idle', '${summary.idle}', AppColors.warning, '/fleet'),
      _Cell('Stopped', '${summary.parked}', AppColors.info, '/fleet'),
      _Cell('No signal', '${summary.noGpsSignal}', AppColors.textMuted, '/fleet'),
      _Cell('Low batt.', '${summary.lowBattery}', AppColors.warning, '/fleet'),
      _Cell('Ignition', '${summary.ignitionOn}', AppColors.accent, '/fleet'),
      _Cell('Overspeed', '${summary.overspeedAlertsToday}', AppColors.error, '/alerts'),
      _Cell('SOS', '${summary.sosAlertsToday}', AppColors.error, '/gps/incidents'),
      _Cell('Geofence', '${summary.geofenceAlertsToday}', AppColors.info, '/alerts'),
      _Cell('Trips today', '${summary.todaysTrips}', AppColors.primary, '/gps/trips'),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Fleet exceptions'),
        const SizedBox(height: 8),
        SizedBox(
          height: 108,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: cells.length,
            separatorBuilder: (_, __) => const SizedBox(width: 8),
            itemBuilder: (_, i) {
              final c = cells[i];
              return _KpiCard(cell: c);
            },
          ),
        ),
      ],
    );
  }
}

class _Cell {
  const _Cell(this.label, this.value, this.color, this.route);
  final String label;
  final String value;
  final Color color;
  final String route;
}

class _KpiCard extends StatelessWidget {
  const _KpiCard({required this.cell});
  final _Cell cell;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(AppRadii.md),
      child: InkWell(
        onTap: () => context.push(cell.route),
        borderRadius: BorderRadius.circular(AppRadii.md),
        child: Container(
          width: 96,
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.md),
            border: Border.all(color: AppColors.border),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                cell.value,
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w800,
                  color: cell.color,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                cell.label,
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class TrackerHealthCard extends StatelessWidget {
  const TrackerHealthCard({super.key, required this.summary});

  final GpsOperatorSummary summary;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SgSectionTitle('Tracker health'),
        const SizedBox(height: 8),
        SgCard(
          child: Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              _chip('Healthy', summary.trackerHealthy, AppColors.success),
              _chip('Offline', summary.trackerOffline, AppColors.error),
              _chip('Weak GSM', summary.weakGsm, AppColors.warning),
              _chip('No GPS', summary.noGpsSignal, AppColors.textMuted),
              _chip('Low battery', summary.lowBattery, AppColors.warning),
            ],
          ),
        ),
      ],
    );
  }

  Widget _chip(String label, int count, Color color) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(AppRadii.sm),
      ),
      child: Text(
        '$label · $count',
        style: TextStyle(
          fontWeight: FontWeight.w700,
          fontSize: 12,
          color: color,
        ),
      ),
    );
  }
}

class RecentGpsAlertsFeed extends StatelessWidget {
  const RecentGpsAlertsFeed({
    super.key,
    required this.alerts,
    this.onAcknowledge,
  });

  final List<GpsAlertEvent> alerts;
  final Future<void> Function(int id)? onAcknowledge;

  @override
  Widget build(BuildContext context) {
    final df = DateFormat('HH:mm');
    final recent = alerts.take(8).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Expanded(child: SgSectionTitle('Recent GPS alerts')),
            TextButton(
              onPressed: () => context.push('/alerts'),
              child: const Text('View all'),
            ),
          ],
        ),
        if (recent.isEmpty)
          const SgCard(
            child: Padding(
              padding: EdgeInsets.all(16),
              child: Text('No recent alerts'),
            ),
          )
        else
          ...recent.map((a) {
            return SgCard(
              margin: const EdgeInsets.only(bottom: 8),
              onTap: () => context.push('/alerts/${a.id}'),
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          a.eventType.replaceAll('_', ' '),
                          style: const TextStyle(fontWeight: FontWeight.w700),
                        ),
                        Text(
                          '${a.vehicleName ?? 'Vehicle'} · ${df.format(a.timestamp.toLocal())}',
                          style: const TextStyle(
                            fontSize: 12,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  ),
                  if (onAcknowledge != null && a.canAcknowledge)
                    FilledButton.tonal(
                      onPressed: () => onAcknowledge!(a.id),
                      child: const Text('ACK'),
                    ),
                ],
              ),
            );
          }),
      ],
    );
  }
}
