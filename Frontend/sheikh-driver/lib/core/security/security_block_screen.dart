import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../config/app_config.dart';
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
    final isEmulatorBlock =
        report.isEmulator && AppConfig.blockEmulators;
    final title = isEmulatorBlock
        ? 'Emulator not allowed'
        : 'Device not allowed';
    final body = isEmulatorBlock
        ? 'Release builds block emulators for security. '
            'Use a debug build for QA on the emulator '
            '(`./scripts/run_android_emu.sh`), or install this release on a physical device.'
        : 'SheikhGo Tracking cannot run on this device because a security check failed.';

    return Scaffold(
      backgroundColor: const Color(0xFF141820),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(28),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Spacer(),
              Icon(
                isEmulatorBlock
                    ? Icons.phone_android_outlined
                    : Icons.security,
                size: 64,
                color: AppColors.error,
              ),
              const SizedBox(height: 20),
              Text(
                title,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 22,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 12),
              Text(
                body,
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.white70, fontSize: 14),
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
              if (kDebugMode) ...[
                const SizedBox(height: 16),
                Text(
                  'Debug tip: blockEmulators=${AppConfig.blockEmulators} '
                  'ENV=${AppConfig.environmentLabel}',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Colors.white38, fontSize: 11),
                ),
              ],
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
