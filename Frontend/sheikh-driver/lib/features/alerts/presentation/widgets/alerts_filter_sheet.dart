import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/constants/app_theme.dart';
import '../alerts_notifier.dart';
import '../../../../shared/widgets/sg_brand_loader.dart';

Future<void> showAlertsFilterSheet(BuildContext context, WidgetRef ref) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.white,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
    ),
    builder: (_) => const AlertsFilterSheet(),
  );
}

class AlertsFilterSheet extends ConsumerWidget {
  const AlertsFilterSheet({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(alertsProvider).valueOrNull;
    if (state == null) {
      return const SizedBox(height: 200, child: Center(child: SgBrandLoader()));
    }
    final notifier = ref.read(alertsProvider.notifier);

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Center(
                child: Container(
                  width: 40,
                  height: 4,
                  decoration: BoxDecoration(
                    color: AppColors.border,
                    borderRadius: BorderRadius.circular(99),
                  ),
                ),
              ),
              const SizedBox(height: 14),
              Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Filters',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
                  TextButton(
                    onPressed: () async {
                      await notifier.clearAdvancedFilters();
                      if (context.mounted) Navigator.pop(context);
                    },
                    child: const Text('Reset'),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              const Text('Alert type',
                  style: TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (final e in const [
                    (null, 'Any'),
                    ('vehicle_offline', 'Offline'),
                    ('ignition_on', 'Ignition'),
                    ('speed_exceeded', 'Overspeed'),
                    ('low_battery', 'Low battery'),
                    ('power_cut', 'Device'),
                    ('sos', 'SOS'),
                    ('geofence_enter', 'Geofence'),
                  ])
                    ChoiceChip(
                      label: Text(e.$2),
                      selected: state.eventTypeFilter == e.$1 ||
                          (e.$1 == null && state.eventTypeFilter == null),
                      onSelected: (_) => notifier.setEventType(e.$1),
                    ),
                ],
              ),
              const SizedBox(height: 16),
              const Text('Date range',
                  style: TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                children: [
                  for (final p in const [
                    'today',
                    'yesterday',
                    'last7',
                    'last30',
                    'all',
                  ])
                    ChoiceChip(
                      label: Text(_dateLabel(p)),
                      selected: state.datePreset == p,
                      onSelected: (_) => notifier.setDatePreset(p),
                    ),
                ],
              ),
              const SizedBox(height: 16),
              const Text('Sort',
                  style: TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                children: [
                  ChoiceChip(
                    label: const Text('Newest first'),
                    selected: state.sort == FleetAlertSort.newest,
                    onSelected: (_) =>
                        notifier.setSort(FleetAlertSort.newest),
                  ),
                  ChoiceChip(
                    label: const Text('Oldest first'),
                    selected: state.sort == FleetAlertSort.oldest,
                    onSelected: (_) =>
                        notifier.setSort(FleetAlertSort.oldest),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              SwitchListTile.adaptive(
                contentPadding: EdgeInsets.zero,
                title: const Text('Unacknowledged only'),
                value: state.unacknowledgedOnly,
                activeTrackColor: AppColors.primary,
                onChanged: (v) => notifier.setUnacknowledgedOnly(v),
              ),
              const SizedBox(height: 12),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.primary,
                  ),
                  onPressed: () => Navigator.pop(context),
                  child: const Text('Apply'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static String _dateLabel(String p) => switch (p) {
        'today' => 'Today',
        'yesterday' => 'Yesterday',
        'last7' => 'Last 7',
        'last30' => 'Last 30',
        'all' => 'All time',
        _ => p,
      };
}
