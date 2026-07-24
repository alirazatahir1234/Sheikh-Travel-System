import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
import '../constants/app_theme.dart';
import '../providers/locale_provider.dart';
import 'biometric_auth_service.dart';
import '../../features/auth/data/auth_repository.dart';

/// Full-screen gate shown when a session exists and biometric lock is enabled.
class AppLockScreen extends ConsumerStatefulWidget {
  const AppLockScreen({super.key});

  @override
  ConsumerState<AppLockScreen> createState() => _AppLockScreenState();
}

class _AppLockScreenState extends ConsumerState<AppLockScreen> {
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _unlock());
  }

  Future<void> _unlock() async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    final l10n = AppLocalizations.of(context);
    final ok = await ref.read(biometricAuthServiceProvider).authenticate(
          reason: l10n.unlockSubtitle,
        );
    if (!mounted) return;
    if (ok) {
      ref.read(appUnlockedProvider.notifier).state = true;
    } else {
      setState(() => _error = l10n.biometricEnableFailed);
    }
    setState(() => _busy = false);
  }

  Future<void> _signOut() async {
    await ref.read(authRepositoryProvider).logout();
    ref.read(appUnlockedProvider.notifier).state = true;
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return Scaffold(
      backgroundColor: AppColors.surface,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(
                Icons.fingerprint_rounded,
                size: 72,
                color: AppColors.primary.withValues(alpha: 0.9),
              ),
              const SizedBox(height: 24),
              Text(
                l10n.unlockTitle,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                l10n.unlockSubtitle,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 14,
                  color: AppColors.textSecondary,
                ),
              ),
              if (_error != null) ...[
                const SizedBox(height: 12),
                Text(
                  _error!,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: AppColors.error, fontSize: 13),
                ),
              ],
              const SizedBox(height: 32),
              FilledButton.icon(
                onPressed: _busy ? null : _unlock,
                icon: _busy
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.lock_open_rounded),
                label: Text(l10n.unlockAction),
              ),
              const SizedBox(height: 12),
              TextButton(
                onPressed: _busy ? null : _signOut,
                child: Text(l10n.unlockSignOut),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Observes lifecycle and locks the app after returning from background.
class AppLockLifecycle extends ConsumerStatefulWidget {
  const AppLockLifecycle({super.key, required this.child});
  final Widget child;

  @override
  ConsumerState<AppLockLifecycle> createState() => _AppLockLifecycleState();
}

class _AppLockLifecycleState extends ConsumerState<AppLockLifecycle>
    with WidgetsBindingObserver {
  DateTime? _pausedAt;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    final enabled = ref.read(biometricLockEnabledProvider);
    final session = ref.read(fleetSessionProvider);
    if (!enabled || session == null) return;

    if (state == AppLifecycleState.paused ||
        state == AppLifecycleState.inactive) {
      _pausedAt ??= DateTime.now();
      return;
    }

    if (state == AppLifecycleState.resumed) {
      final pausedAt = _pausedAt;
      _pausedAt = null;
      if (pausedAt == null) return;
      final away = DateTime.now().difference(pausedAt);
      if (away >= const Duration(seconds: 15)) {
        ref.read(appUnlockedProvider.notifier).state = false;
      }
    }
  }

  @override
  Widget build(BuildContext context) => widget.child;
}
