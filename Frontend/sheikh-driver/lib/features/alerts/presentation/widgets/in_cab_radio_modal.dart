import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../../../core/constants/app_theme.dart';

/// Demo-only in-cab radio / dispatch call sheet (no real audio).
Future<void> showInCabRadioModal(
  BuildContext context, {
  required String driverName,
  String? vehicleLabel,
}) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    backgroundColor: const Color(0xFF0F172A),
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
    ),
    builder: (_) => InCabRadioModal(
      driverName: driverName,
      vehicleLabel: vehicleLabel,
    ),
  );
}

class InCabRadioModal extends StatefulWidget {
  const InCabRadioModal({
    super.key,
    required this.driverName,
    this.vehicleLabel,
  });

  final String driverName;
  final String? vehicleLabel;

  @override
  State<InCabRadioModal> createState() => _InCabRadioModalState();
}

class _InCabRadioModalState extends State<InCabRadioModal> {
  late final Stopwatch _watch;
  Timer? _ticker;
  bool _muted = false;
  bool _speaker = true;
  String? _lastPrompt;

  @override
  void initState() {
    super.initState();
    _watch = Stopwatch()..start();
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }

  String get _elapsed {
    final s = _watch.elapsed.inSeconds;
    final m = (s ~/ 60).toString().padLeft(2, '0');
    final r = (s % 60).toString().padLeft(2, '0');
    return '$m:$r';
  }

  void _sendPrompt(String text) {
    HapticFeedback.mediumImpact();
    setState(() => _lastPrompt = text);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Demo: sent “$text” to cabin console'),
        duration: const Duration(seconds: 2),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final initial = widget.driverName.trim().isEmpty
        ? 'D'
        : widget.driverName.trim()[0].toUpperCase();

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(24, 16, 24, 28),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 40,
              height: 4,
              decoration: BoxDecoration(
                color: Colors.white24,
                borderRadius: BorderRadius.circular(99),
              ),
            ),
            const SizedBox(height: 8),
            const Text(
              'IN-CAB RADIO · DEMO',
              style: TextStyle(
                color: Colors.white54,
                fontSize: 11,
                fontWeight: FontWeight.w700,
                letterSpacing: 1.1,
              ),
            ),
            const SizedBox(height: 20),
            CircleAvatar(
              radius: 40,
              backgroundColor: AppColors.primary.withValues(alpha: 0.3),
              child: Text(
                initial,
                style: const TextStyle(
                  fontSize: 28,
                  fontWeight: FontWeight.w800,
                  color: Colors.white,
                ),
              ),
            ),
            const SizedBox(height: 12),
            Text(
              widget.driverName,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 20,
                fontWeight: FontWeight.w800,
              ),
            ),
            if (widget.vehicleLabel != null)
              Text(
                widget.vehicleLabel!,
                style: const TextStyle(color: Colors.white70, fontSize: 13),
              ),
            const SizedBox(height: 8),
            Text(
              _elapsed,
              style: const TextStyle(
                color: AppColors.primaryLight,
                fontSize: 28,
                fontWeight: FontWeight.w700,
                fontFeatures: [FontFeature.tabularFigures()],
              ),
            ),
            const SizedBox(height: 20),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                _RoundToggle(
                  icon: _muted ? Icons.mic_off_rounded : Icons.mic_rounded,
                  label: _muted ? 'Unmute' : 'Mute',
                  active: _muted,
                  onTap: () => setState(() => _muted = !_muted),
                ),
                const SizedBox(width: 28),
                _RoundToggle(
                  icon: _speaker
                      ? Icons.volume_up_rounded
                      : Icons.volume_off_rounded,
                  label: 'Speaker',
                  active: _speaker,
                  onTap: () => setState(() => _speaker = !_speaker),
                ),
              ],
            ),
            const SizedBox(height: 20),
            Align(
              alignment: Alignment.centerLeft,
              child: Text(
                'Cabin prompts',
                style: TextStyle(
                  color: Colors.white.withValues(alpha: 0.7),
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final p in const [
                  'Slow down',
                  'Return to route',
                  'Pull over safely',
                  'Acknowledge alert',
                ])
                  ActionChip(
                    label: Text(p),
                    onPressed: () => _sendPrompt(p),
                    backgroundColor: Colors.white12,
                    labelStyle: const TextStyle(color: Colors.white),
                  ),
              ],
            ),
            if (_lastPrompt != null) ...[
              const SizedBox(height: 12),
              Text(
                'Last sent: $_lastPrompt',
                style: const TextStyle(color: AppColors.primaryLight, fontSize: 12),
              ),
            ],
            const SizedBox(height: 24),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.error,
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(vertical: 14),
                ),
                onPressed: () => Navigator.pop(context),
                child: const Text('End call'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _RoundToggle extends StatelessWidget {
  const _RoundToggle({
    required this.icon,
    required this.label,
    required this.active,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        InkWell(
          onTap: onTap,
          customBorder: const CircleBorder(),
          child: CircleAvatar(
            radius: 28,
            backgroundColor: active ? Colors.white24 : Colors.white12,
            child: Icon(icon, color: Colors.white),
          ),
        ),
        const SizedBox(height: 6),
        Text(label, style: const TextStyle(color: Colors.white70, fontSize: 12)),
      ],
    );
  }
}
