import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';
import '../../domain/fleet_models.dart';
import '../fleet_vehicle_filters.dart';

Color fleetStatusColor(FleetTrackStatus status) => switch (status) {
      FleetTrackStatus.moving => const Color(0xFF2563EB),
      FleetTrackStatus.idle => const Color(0xFFF59E0B),
      FleetTrackStatus.parked => const Color(0xFF64748B),
      FleetTrackStatus.offline => const Color(0xFFDC2626),
      FleetTrackStatus.neverSeen => const Color(0xFF94A3B8),
      FleetTrackStatus.sos => AppColors.error,
    };

/// Connection (Online/Offline[/Unknown]) + activity — always reconciles with list.
class FleetKpiStrip extends StatelessWidget {
  const FleetKpiStrip({
    super.key,
    required this.kpis,
    required this.selected,
    required this.onSelect,
  });

  final GpsFleetStatusKpis kpis;
  final FleetStatusFilterOption selected;
  final ValueChanged<FleetStatusFilterOption> onSelect;

  @override
  Widget build(BuildContext context) {
    final connection = <_KpiChipData>[
      _KpiChipData(
        'Online',
        kpis.online,
        FleetStatusFilterOption.online,
        const Color(0xFF16A34A),
      ),
      _KpiChipData(
        'Offline',
        kpis.offline,
        FleetStatusFilterOption.offline,
        fleetStatusColor(FleetTrackStatus.offline),
      ),
      if (kpis.neverSeen > 0)
        _KpiChipData(
          'Unknown',
          kpis.neverSeen,
          FleetStatusFilterOption.offline,
          fleetStatusColor(FleetTrackStatus.neverSeen),
        ),
    ];

    final activity = <_KpiChipData>[
      _KpiChipData(
        'Moving',
        kpis.moving,
        FleetStatusFilterOption.moving,
        fleetStatusColor(FleetTrackStatus.moving),
      ),
      _KpiChipData(
        'Idle',
        kpis.idle,
        FleetStatusFilterOption.idle,
        fleetStatusColor(FleetTrackStatus.idle),
      ),
      _KpiChipData(
        'Parked',
        kpis.parked,
        FleetStatusFilterOption.parked,
        fleetStatusColor(FleetTrackStatus.parked),
      ),
    ];

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _KpiSectionLabel('Connection'),
          const SizedBox(height: 6),
          _KpiRow(chips: connection, selected: selected, onSelect: onSelect),
          const SizedBox(height: 10),
          const _KpiSectionLabel('Activity (online vehicles)'),
          const SizedBox(height: 6),
          _KpiRow(chips: activity, selected: selected, onSelect: onSelect),
          if (kpis.sos > 0) ...[
            const SizedBox(height: 10),
            _KpiRow(
              chips: [
                _KpiChipData(
                  'SOS',
                  kpis.sos,
                  FleetStatusFilterOption.online,
                  fleetStatusColor(FleetTrackStatus.sos),
                ),
              ],
              selected: selected,
              onSelect: onSelect,
            ),
          ],
        ],
      ),
    );
  }
}

class _KpiSectionLabel extends StatelessWidget {
  const _KpiSectionLabel(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        fontSize: 11,
        fontWeight: FontWeight.w700,
        color: AppColors.textMuted,
        letterSpacing: 0.2,
      ),
    );
  }
}

class _KpiRow extends StatelessWidget {
  const _KpiRow({
    required this.chips,
    required this.selected,
    required this.onSelect,
  });

  final List<_KpiChipData> chips;
  final FleetStatusFilterOption selected;
  final ValueChanged<FleetStatusFilterOption> onSelect;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        for (var i = 0; i < chips.length; i++) ...[
          if (i > 0) const SizedBox(width: 8),
          Expanded(
            child: _KpiChip(
              data: chips[i],
              active: chips[i].filter == selected,
              onTap: () => onSelect(chips[i].filter),
            ),
          ),
        ],
      ],
    );
  }
}

class _KpiChip extends StatelessWidget {
  const _KpiChip({
    required this.data,
    required this.active,
    required this.onTap,
  });

  final _KpiChipData data;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AppRadii.md),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
        decoration: BoxDecoration(
          color: active ? data.color.withValues(alpha: 0.14) : Colors.white,
          borderRadius: BorderRadius.circular(AppRadii.md),
          border: Border.all(
            color: active
                ? data.color.withValues(alpha: 0.45)
                : AppColors.border,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${data.value}',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w800,
                height: 1.1,
                color: data.color,
              ),
            ),
            const SizedBox(height: 2),
            Text(
              data.label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
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
  }
}

class _KpiChipData {
  const _KpiChipData(this.label, this.value, this.filter, this.color);
  final String label;
  final int value;
  final FleetStatusFilterOption filter;
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
          // Backend: Vehicles.Status IN (1,2) — fleet inventory, not GPS online.
          _MiniStat(label: 'In service', value: '${ops.activeVehicles}'),
          _MiniStat(label: 'Drivers on duty', value: '${ops.driversOnDuty}'),
          _MiniStat(label: 'Maintenance', value: '${ops.maintenanceDue}'),
          _MiniStat(
            label: 'Critical alerts',
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
            textAlign: TextAlign.center,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontSize: 10, color: AppColors.textMuted),
          ),
        ],
      ),
    );
  }
}
