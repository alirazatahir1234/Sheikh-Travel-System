import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';
import '../fleet_vehicle_filters.dart';

/// Bottom sheet: Status / Ignition / Signal / Driver / Alerts + Clear | Apply.
Future<FleetVehicleFilters?> showFleetVehicleFilterSheet(
  BuildContext context, {
  required FleetVehicleFilters initial,
}) {
  return showModalBottomSheet<FleetVehicleFilters>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.white,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(18)),
    ),
    builder: (ctx) => _FleetFilterSheet(initial: initial),
  );
}

class _FleetFilterSheet extends StatefulWidget {
  const _FleetFilterSheet({required this.initial});

  final FleetVehicleFilters initial;

  @override
  State<_FleetFilterSheet> createState() => _FleetFilterSheetState();
}

class _FleetFilterSheetState extends State<_FleetFilterSheet> {
  late FleetVehicleFilters _draft;

  @override
  void initState() {
    super.initState();
    _draft = widget.initial;
  }

  @override
  Widget build(BuildContext context) {
    final bottom = MediaQuery.paddingOf(context).bottom;
    return Padding(
      padding: EdgeInsets.fromLTRB(16, 10, 16, 12 + bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Center(
            child: Container(
              width: 40,
              height: 4,
              decoration: BoxDecoration(
                color: AppColors.border,
                borderRadius: BorderRadius.circular(4),
              ),
            ),
          ),
          const SizedBox(height: 12),
          const Text(
            'Filter Vehicles',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 14),
          ConstrainedBox(
            constraints: BoxConstraints(
              maxHeight: MediaQuery.sizeOf(context).height * 0.62,
            ),
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _Section(
                    title: 'Status',
                    child: Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        for (final o in FleetStatusFilterOption.values)
                          _Chip(
                            label: _statusLabel(o),
                            selected: _draft.status == o,
                            onTap: () => setState(
                              () => _draft = _draft.copyWith(status: o),
                            ),
                          ),
                      ],
                    ),
                  ),
                  _Section(
                    title: 'Ignition',
                    child: Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        for (final o in FleetIgnitionFilter.values)
                          _Chip(
                            label: _ignitionLabel(o),
                            selected: _draft.ignition == o,
                            onTap: () => setState(
                              () => _draft = _draft.copyWith(ignition: o),
                            ),
                          ),
                      ],
                    ),
                  ),
                  _Section(
                    title: 'GPS Signal',
                    child: Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        for (final o in FleetSignalFilter.values)
                          _Chip(
                            label: _signalLabel(o),
                            selected: _draft.signal == o,
                            onTap: () => setState(
                              () => _draft = _draft.copyWith(signal: o),
                            ),
                          ),
                      ],
                    ),
                  ),
                  _Section(
                    title: 'Driver',
                    child: Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        for (final o in FleetDriverFilter.values)
                          _Chip(
                            label: _driverLabel(o),
                            selected: _draft.driver == o,
                            onTap: () => setState(
                              () => _draft = _draft.copyWith(driver: o),
                            ),
                          ),
                      ],
                    ),
                  ),
                  _Section(
                    title: 'Alerts',
                    child: Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        for (final o in FleetAlertFilter.values)
                          _Chip(
                            label: _alertLabel(o),
                            selected: _draft.alerts == o,
                            onTap: () => setState(
                              () => _draft = _draft.copyWith(alerts: o),
                            ),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: () => setState(() => _draft = FleetVehicleFilters.empty),
                  child: const Text('Clear all'),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: FilledButton(
                  onPressed: () => Navigator.pop(context, _draft),
                  child: const Text('Apply'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  String _statusLabel(FleetStatusFilterOption o) => switch (o) {
        FleetStatusFilterOption.all => 'All',
        FleetStatusFilterOption.online => 'Online',
        FleetStatusFilterOption.moving => 'Moving',
        FleetStatusFilterOption.idle => 'Idle',
        FleetStatusFilterOption.parked => 'Parked',
        FleetStatusFilterOption.offline => 'Offline',
      };

  String _ignitionLabel(FleetIgnitionFilter o) => switch (o) {
        FleetIgnitionFilter.all => 'All',
        FleetIgnitionFilter.on => 'ON',
        FleetIgnitionFilter.off => 'OFF',
        FleetIgnitionFilter.unknown => 'Unknown',
      };

  String _signalLabel(FleetSignalFilter o) => switch (o) {
        FleetSignalFilter.all => 'All',
        FleetSignalFilter.good => 'Good',
        FleetSignalFilter.weak => 'Weak',
        FleetSignalFilter.offline => 'Offline',
      };

  String _driverLabel(FleetDriverFilter o) => switch (o) {
        FleetDriverFilter.all => 'All',
        FleetDriverFilter.assigned => 'Assigned',
        FleetDriverFilter.unassigned => 'Unassigned',
      };

  String _alertLabel(FleetAlertFilter o) => switch (o) {
        FleetAlertFilter.all => 'All',
        FleetAlertFilter.withAlerts => 'With alerts',
        FleetAlertFilter.noAlerts => 'No alerts',
      };
}

class _Section extends StatelessWidget {
  const _Section({required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w700,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 8),
          child,
        ],
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AppRadii.pill),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        decoration: BoxDecoration(
          color: selected
              ? AppColors.primary.withValues(alpha: 0.12)
              : AppColors.surface,
          borderRadius: BorderRadius.circular(AppRadii.pill),
          border: Border.all(
            color: selected ? AppColors.primary : AppColors.border,
          ),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: selected ? AppColors.primary : AppColors.textPrimary,
          ),
        ),
      ),
    );
  }
}
