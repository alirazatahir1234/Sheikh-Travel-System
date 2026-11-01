import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/fatigue_api.dart';
import '../../../core/api/dio_client.dart';

final fatigueApiProvider = Provider<FatigueApi>((ref) {
  return FatigueApi(ref.watch(dioProvider));
});

class FatigueResponseScreen extends ConsumerStatefulWidget {
  const FatigueResponseScreen({super.key, this.eventId});

  final int? eventId;

  @override
  ConsumerState<FatigueResponseScreen> createState() =>
      _FatigueResponseScreenState();
}

class _FatigueResponseScreenState extends ConsumerState<FatigueResponseScreen> {
  FatigueActivePrompt? _prompt;
  Timer? _timer;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _load();
    _timer =
        Timer.periodic(const Duration(seconds: 2), (_) => _load(silent: true));
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _load({bool silent = false}) async {
    try {
      final p = await ref.read(fatigueApiProvider).fetchActivePrompt();
      if (!mounted) return;
      setState(() => _prompt = p);
    } catch (_) {
      if (!silent && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not load fatigue prompt')),
        );
      }
    }
  }

  Future<void> _ack() async {
    final id = widget.eventId ?? _prompt?.id;
    if (id == null) return;
    setState(() => _busy = true);
    try {
      await ref.read(fatigueApiProvider).acknowledge(id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Acknowledged — stay alert')),
        );
        Navigator.of(context).pop(true);
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = _prompt;
    return Scaffold(
      appBar: AppBar(title: const Text('Fatigue alert')),
      body: Padding(
        padding: const EdgeInsets.all(20),
        child: p == null
            ? const Center(child: Text('No active fatigue prompt'))
            : Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Icon(Icons.bedtime,
                      size: 56, color: Theme.of(context).colorScheme.error),
                  const SizedBox(height: 12),
                  Text(p.promptTitle,
                      style: Theme.of(context).textTheme.headlineSmall),
                  const SizedBox(height: 8),
                  Text(p.promptBody),
                  const SizedBox(height: 8),
                  Text('Respond within ${p.secondsRemaining}s',
                      style: Theme.of(context).textTheme.labelLarge),
                  const Spacer(),
                  FilledButton.icon(
                    onPressed: _busy ? null : _ack,
                    icon: const Icon(Icons.check_circle),
                    label: const Text('I am alert'),
                  ),
                ],
              ),
      ),
    );
  }
}
