import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/config/app_config.dart';
import '../../../core/security/security_block_screen.dart';

class SecurityStatusScreen extends ConsumerWidget {
  const SecurityStatusScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(deviceIntegrityProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Security status')),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('$e')),
        data: (r) => ListView(
          padding: const EdgeInsets.all(16),
          children: [
            _StatusCard(
              ok: !r.hasBlockingIssue,
              title: r.hasBlockingIssue ? 'Blocked on this device' : 'Device OK',
              subtitle: r.hasBlockingIssue
                  ? 'Production policy prevents use until issues are resolved'
                  : 'Integrity checks passed for current policy',
            ),
            const SizedBox(height: 12),
            _FlagTile('Emulator / simulator', r.isEmulator),
            _FlagTile('Rooted (Android)', r.isRooted),
            _FlagTile('Jailbroken (iOS)', r.isJailbroken),
            _FlagTile('Tamper signals', r.isTampered),
            _FlagTile('TLS pinning configured', r.pinningConfigured, invert: true),
            const SizedBox(height: 16),
            const Text('Device',
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
            const SizedBox(height: 8),
            _Info('Platform', r.platform),
            _Info('Model', r.model),
            _Info('OS', r.osVersion),
            _Info('App', r.appVersion),
            _Info('Package', r.packageName),
            _Info('Installer', r.installerStore ?? '—'),
            _Info('Device id', r.deviceId),
            _Info('Environment', AppConfig.environment.name),
            if (r.issues.isNotEmpty) ...[
              const SizedBox(height: 16),
              const Text('Findings',
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
              const SizedBox(height: 8),
              ...r.issues.map(
                (i) => ListTile(
                  dense: true,
                  leading: const Icon(Icons.warning_amber, color: AppColors.warning),
                  title: Text(i, style: const TextStyle(fontSize: 13)),
                ),
              ),
            ],
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: () => ref.invalidate(deviceIntegrityProvider),
              icon: const Icon(Icons.refresh),
              label: const Text('Re-run checks'),
            ),
          ],
        ),
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  const _StatusCard({
    required this.ok,
    required this.title,
    required this.subtitle,
  });
  final bool ok;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final color = ok ? AppColors.success : AppColors.error;
    return Card(
      color: color.withValues(alpha: 0.08),
      child: ListTile(
        leading: Icon(ok ? Icons.verified_user : Icons.gpp_bad, color: color),
        title: Text(title, style: TextStyle(fontWeight: FontWeight.w700, color: color)),
        subtitle: Text(subtitle),
      ),
    );
  }
}

class _FlagTile extends StatelessWidget {
  const _FlagTile(this.label, this.flag, {this.invert = false});
  final String label;
  final bool flag;
  final bool invert;

  @override
  Widget build(BuildContext context) {
    final bad = invert ? !flag : flag;
    return ListTile(
      dense: true,
      title: Text(label),
      trailing: Icon(
        bad ? Icons.cancel : Icons.check_circle,
        color: bad ? AppColors.error : AppColors.success,
      ),
    );
  }
}

class _Info extends StatelessWidget {
  const _Info(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 100,
            child: Text(label,
                style: const TextStyle(color: AppColors.textSecondary, fontSize: 13)),
          ),
          Expanded(
            child: Text(value,
                style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
          ),
        ],
      ),
    );
  }
}
