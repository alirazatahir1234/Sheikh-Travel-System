import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import '../data/sos_api.dart';

class SosScreen extends ConsumerStatefulWidget {
  const SosScreen({super.key});

  @override
  ConsumerState<SosScreen> createState() => _SosScreenState();
}

class _SosScreenState extends ConsumerState<SosScreen> {
  bool _sending = false;
  SosSubmitResult? _last;
  Timer? _poll;
  String? _error;

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> _triggerSos() async {
    if (_sending) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Send SOS?'),
        content: const Text(
          'This alerts admin, fleet managers, and the nearest verified team with your live location.',
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: Colors.red),
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Send SOS'),
          ),
        ],
      ),
    );
    if (ok != true) return;

    setState(() {
      _sending = true;
      _error = null;
    });

    try {
      double? lat;
      double? lng;
      final perm = await Geolocator.requestPermission();
      if (perm == LocationPermission.always || perm == LocationPermission.whileInUse) {
        final pos = await Geolocator.getCurrentPosition();
        lat = pos.latitude;
        lng = pos.longitude;
      }

      final result = await ref.read(sosApiProvider).sendSos(latitude: lat, longitude: lng);
      if (!mounted) return;
      setState(() {
        _last = result;
        _sending = false;
      });
      _startPolling(result.id);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _sending = false;
        _error = e.toString();
      });
    }
  }

  void _startPolling(int sosId) {
    _poll?.cancel();
    _poll = Timer.periodic(const Duration(seconds: 15), (_) async {
      try {
        final st = await ref.read(sosApiProvider).fetchStatus(sosId);
        if (!mounted) return;
        setState(() {
          _last = SosSubmitResult(
            id: st.id,
            createdAt: _last?.createdAt ?? DateTime.now(),
            confirmationMessage: st.confirmationMessage ?? _last?.confirmationMessage,
            dispatchMode: _last?.dispatchMode,
            assignedTeamName: st.assignedTeamName ?? _last?.assignedTeamName,
            etaMinutes: st.etaMinutes ?? _last?.etaMinutes,
          );
        });
        if (st.status == 'Resolved') _poll?.cancel();
      } catch (_) {}
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Emergency SOS')),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'Press only in a real emergency. Help is dispatched automatically with your GPS speed and recent route.',
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 32),
            Center(
              child: Semantics(
                button: true,
                label: 'Send SOS',
                child: Material(
                  color: Colors.red.shade700,
                  shape: const CircleBorder(),
                  elevation: 6,
                  child: InkWell(
                    customBorder: const CircleBorder(),
                    onTap: _sending ? null : _triggerSos,
                    child: SizedBox(
                      width: 160,
                      height: 160,
                      child: Center(
                        child: _sending
                            ? const CircularProgressIndicator(color: Colors.white)
                            : const Text(
                                'SOS',
                                style: TextStyle(
                                  color: Colors.white,
                                  fontSize: 36,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                      ),
                    ),
                  ),
                ),
              ),
            ),
            if (_error != null) ...[
              const SizedBox(height: 24),
              Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
            if (_last != null) ...[
              const SizedBox(height: 32),
              Card(
                color: Colors.green.shade50,
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Help is on the way', style: TextStyle(fontWeight: FontWeight.bold)),
                      if (_last!.confirmationMessage != null) Text(_last!.confirmationMessage!),
                      if (_last!.assignedTeamName != null)
                        Text('Team: ${_last!.assignedTeamName}'),
                      if (_last!.etaMinutes != null) Text('ETA: ~${_last!.etaMinutes} min'),
                    ],
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
