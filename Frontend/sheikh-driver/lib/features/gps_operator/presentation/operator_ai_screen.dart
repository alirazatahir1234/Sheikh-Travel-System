import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/operator_insights_api.dart';

const _prompts = [
  'Show all vehicles offline more than 15 minutes',
  'Show overspeed vehicles right now',
  'Summarize today\'s GPS alerts',
  'Which vehicles have low battery?',
  'List geofence violations today',
];

class OperatorAiScreen extends ConsumerStatefulWidget {
  const OperatorAiScreen({super.key});

  @override
  ConsumerState<OperatorAiScreen> createState() => _OperatorAiScreenState();
}

class _OperatorAiScreenState extends ConsumerState<OperatorAiScreen> {
  OperatorInsightResult? _result;
  bool _loading = false;
  String? _error;

  Future<void> _run(String query) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final r = await ref.read(operatorInsightsApiProvider).fetch(query);
      if (mounted) setState(() => _result = r);
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('AI insights')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          const Text(
            'Scoped fleet queries run on the server. Open Copilot for free-form chat.',
            style: TextStyle(color: AppColors.textSecondary),
          ),
          const SizedBox(height: 16),
          ..._prompts.map(
            (p) => Card(
              child: ListTile(
                leading: const Icon(Icons.auto_awesome_outlined),
                title: Text(p),
                trailing: const Icon(Icons.chevron_right),
                onTap: () => _run(p),
              ),
            ),
          ),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: () => context.push('/ai'),
            icon: const Icon(Icons.chat_outlined),
            label: const Text('Open AI Copilot'),
          ),
          if (_loading) ...[
            const SizedBox(height: 24),
            const Center(child: CircularProgressIndicator()),
          ],
          if (_error != null) ...[
            const SizedBox(height: 16),
            Text(_error!, style: const TextStyle(color: AppColors.error)),
          ],
          if (_result != null) ...[
            const SizedBox(height: 20),
            SgSectionTitle(_result!.title),
            const SizedBox(height: 8),
            SgCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(_result!.summary),
                  if (_result!.bullets.isNotEmpty) ...[
                    const SizedBox(height: 12),
                    for (final b in _result!.bullets)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 4),
                        child: Text('• $b'),
                      ),
                  ],
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}
