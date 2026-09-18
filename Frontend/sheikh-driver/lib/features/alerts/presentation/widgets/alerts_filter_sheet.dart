import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/constants/app_theme.dart';
import '../../domain/alert_display.dart';
import '../alerts_notifier.dart';

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
      return const SizedBox(height: 200, child: Center(child: CircularProgressIndicator()));
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
                      'Advanced filters',
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
              const Text('Severity',
                  style: TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                children: [
                  for (final s in const ['critical', 'high', 'medium', 'low'])
                    ChoiceChip(
                      label: Text(s[0].toUpperCase() + s.substring(1)),
                      selected: state.severityFilter == s,
                      onSelected: (on) =>
                          notifier.setSeverity(on ? s : null),
                    ),
                ],
              ),
              const SizedBox(height: 16),
              const Text('Status',
                  style: TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                children: [
                  for (final s in const [
                    'unread',
                    'read',
                    'acknowledged',
                    'resolved',
                    'archived',
                  ])
                    ChoiceChip(
                      label: Text(s[0].toUpperCase() + s.substring(1)),
                      selected: (s == 'unread' &&
                              state.readStateFilter == 'unread') ||
                          (s == 'read' && state.readStateFilter == 'read') ||
                          state.statusFilter == s,
                      onSelected: (on) =>
                          notifier.setLifecycle(on ? s : null),
                    ),
                ],
              ),
              const SizedBox(height: 16),
              const Text('Category',
                  style: TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  for (final c in AlertCategoryX.pillOrder)
                    ChoiceChip(
                      label: Text(c.label),
                      selected: state.categoryFilter == c,
                      onSelected: (_) => notifier.setCategoryFilter(c),
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
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
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
