import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';
import '../../domain/fleet_models.dart';

Color fleetStatusColor(FleetTrackStatus status) => switch (status) {
      FleetTrackStatus.moving => const Color(0xFF16A34A),
      FleetTrackStatus.idle => const Color(0xFFCA8A04),
      FleetTrackStatus.parked => const Color(0xFF2563EB),
      FleetTrackStatus.offline => AppColors.textMuted,
      FleetTrackStatus.neverSeen => const Color(0xFF94A3B8),
      FleetTrackStatus.sos => AppColors.error,
    };

class FleetKpiStrip extends StatelessWidget {
  const FleetKpiStrip({
    super.key,
    required this.kpis,
    required this.selected,
    required this.onSelect,
  });

  final GpsFleetStatusKpis kpis;
  final FleetTrackStatus? selected;
  final ValueChanged<FleetTrackStatus?> onSelect;

  @override
  Widget build(BuildContext context) {
    final chips = <_KpiChipData>[
      _KpiChipData('Online', kpis.online, null, AppColors.primary),
      _KpiChipData(
          'Moving', kpis.moving, FleetTrackStatus.moving, fleetStatusColor(FleetTrackStatus.moving)),
      _KpiChipData(
          'Idle', kpis.idle, FleetTrackStatus.idle, fleetStatusColor(FleetTrackStatus.idle)),
      _KpiChipData(
          'Parked', kpis.parked, FleetTrackStatus.parked, fleetStatusColor(FleetTrackStatus.parked)),
      _KpiChipData(
          'Offline', kpis.offline, FleetTrackStatus.offline, fleetStatusColor(FleetTrackStatus.offline)),
      if (kpis.sos > 0)
        _KpiChipData(
            'SOS', kpis.sos, FleetTrackStatus.sos, fleetStatusColor(FleetTrackStatus.sos)),
    ];

    return SizedBox(
      height: 80,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        itemCount: chips.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (context, i) {
          final chip = chips[i];
          final active = chip.filter == selected ||
              (chip.filter == null && selected == null && chip.label == 'Online');
          // Online is summary-only; tapping clears filter
          final isOnlineSummary = chip.filter == null;
          return InkWell(
            onTap: () {
              if (isOnlineSummary) {
                onSelect(null);
              } else {
                onSelect(chip.filter);
              }
            },
            borderRadius: BorderRadius.circular(AppRadii.md),
            child: Container(
              width: 88,
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              decoration: BoxDecoration(
                color: active
                    ? chip.color.withValues(alpha: 0.14)
                    : Colors.white,
                borderRadius: BorderRadius.circular(AppRadii.md),
                border: Border.all(
                  color: active
                      ? chip.color.withValues(alpha: 0.45)
                      : AppColors.border,
                ),
              ),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${chip.value}',
                    style: TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.w800,
                      height: 1.1,
                      color: chip.color,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    chip.label,
                    style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      height: 1.1,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

class _KpiChipData {
  const _KpiChipData(this.label, this.value, this.filter, this.color);
  final String label;
  final int value;
  final FleetTrackStatus? filter;
  final Color color;
}

class FleetOpsSummaryRow extends StatelessWidget {
  const FleetOpsSummaryRow({super.key, required this.ops});
  final FleetOpsDashboard ops;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
      child: Row(
        children: [
          _MiniStat(label: 'Active', value: '${ops.activeVehicles}'),
          _MiniStat(label: 'On duty', value: '${ops.driversOnDuty}'),
          _MiniStat(label: 'Maint.', value: '${ops.maintenanceDue}'),
          _MiniStat(
            label: 'Alerts',
            value: '${ops.complianceAlerts}',
            warn: ops.complianceAlerts > 0,
          ),
        ],
      ),
    );
  }
}

class _MiniStat extends StatelessWidget {
  const _MiniStat({
    required this.label,
    required this.value,
    this.warn = false,
  });
  final String label;
  final String value;
  final bool warn;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        children: [
          Text(
            value,
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: warn ? AppColors.error : AppColors.textPrimary,
            ),
          ),
          Text(
            label,
            style: const TextStyle(fontSize: 11, color: AppColors.textMuted),
          ),
        ],
      ),
    );
  }
}
