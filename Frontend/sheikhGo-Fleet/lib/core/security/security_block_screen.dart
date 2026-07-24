import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../constants/app_theme.dart';
import 'device_integrity_service.dart';

final deviceIntegrityProvider =
    FutureProvider<DeviceIntegrityReport>((ref) async {
  return DeviceIntegrityService.instance.evaluate(force: true);
});

/// Full-screen block when a production build detects a compromised device.
class SecurityBlockScreen extends ConsumerWidget {
  const SecurityBlockScreen({super.key, required this.report});
  final DeviceIntegrityReport report;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      backgroundColor: const Color(0xFF141820),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(28),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Spacer(),
              const Icon(Icons.security, size: 64, color: AppColors.error),
              const SizedBox(height: 20),
              const Text(
                'Device not allowed',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 22,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 12),
              const Text(
                'SheikhGo Fleet cannot run on this device because a security check failed.',
                textAlign: TextAlign.center,
                style: TextStyle(color: Colors.white70, fontSize: 14),
              ),
              const SizedBox(height: 24),
              ...report.issues.map(
                (i) => Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: Row(
                    children: [
                      const Icon(Icons.warning_amber_rounded,
                          color: AppColors.warning, size: 18),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(i,
                            style: const TextStyle(
                                color: Colors.white70, fontSize: 13)),
                      ),
                    ],
                  ),
                ),
              ),
              const Spacer(),
              OutlinedButton(
                onPressed: () =>
                    ref.invalidate(deviceIntegrityProvider),
                child: const Text('Recheck'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
