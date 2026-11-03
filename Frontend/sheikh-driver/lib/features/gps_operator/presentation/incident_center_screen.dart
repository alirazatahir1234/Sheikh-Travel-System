import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../alerts/presentation/alerts_notifier.dart';

const _incidentTypes = [
  ('SOS', 'sos'),
  ('Tow', 'tow'),
  ('Power cut', 'power_cut'),
  ('Low battery', 'low_battery'),
  ('Offline', 'gps_offline'),
  ('Overspeed', 'overspeed'),
];

class IncidentCenterScreen extends ConsumerWidget {
  const IncidentCenterScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(title: const Text('Incident center')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            'Jump to live alert queues filtered by incident type.',
            style: TextStyle(color: context.sg.textSecondary),
          ),
          const SizedBox(height: 16),
          ..._incidentTypes.map(
            (t) => Card(
              child: ListTile(
                leading: const Icon(Icons.emergency_outlined),
                title: Text(t.$1),
                trailing: const Icon(Icons.chevron_right),
                onTap: () {
                  ref.read(alertsProvider.notifier).setEventType(t.$2);
                  context.push('/alerts');
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}
