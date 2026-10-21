import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/config/app_config.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../../../core/providers/locale_provider.dart';
import '../../../core/providers/theme_provider.dart';
import '../../../core/security/biometric_auth_service.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../gps/services/background_gps_tracker.dart';
import '../../notifications/services/push_registration_service.dart';
import '../services/app_version_service.dart';

class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final isDark = ref.watch(darkModeProvider);
    final localeCode = ref.watch(localeCodeProvider);
    final biometricOn = ref.watch(biometricLockEnabledProvider);

    return Scaffold(
      appBar: AppBar(title: Text(l10n.settings)),
      body: ListView(
        padding: const EdgeInsets.symmetric(vertical: 12),
        children: [
          // ── Account ──────────────────────────
          _SectionHeader(l10n.account),
          _SettingTile(
            icon: Icons.lock_outline,
            iconColor: AppColors.primary,
            title: 'Change Password',
            onTap: () => _showChangePasswordSheet(context, ref),
          ),

          // ── Appearance ────────────────────────
          _SectionHeader(l10n.appearance),
          SwitchListTile(
            secondary: const _IconBox(
              icon: Icons.dark_mode_outlined,
              color: AppColors.primary,
            ),
            title: Text(l10n.darkMode,
                style: TextStyle(
                    fontWeight: FontWeight.w500, color: context.sg.textPrimary)),
            subtitle: Text(l10n.darkModeSubtitle,
                style: TextStyle(
                    fontSize: 12, color: context.sg.textSecondary)),
            value: isDark,
            activeThumbColor: AppColors.primary,
            activeTrackColor: AppColors.primaryLight,
            onChanged: (v) {
              ref.read(darkModeProvider.notifier).state = v;
              ref.read(prefsBoxProvider).put('darkMode', v);
            },
          ),
          ListTile(
            leading: const _IconBox(
              icon: Icons.language_outlined,
              color: AppColors.accent,
            ),
            title: Text(l10n.language,
                style: TextStyle(
                    fontWeight: FontWeight.w500, color: context.sg.textPrimary)),
            subtitle: Text(l10n.languageSubtitle,
                style: TextStyle(
                    fontSize: 12, color: context.sg.textSecondary)),
            trailing: DropdownButton<String>(
              value: localeCode == 'ar' ? 'ar' : 'en',
              underline: const SizedBox.shrink(),
              items: [
                DropdownMenuItem(
                    value: 'en', child: Text(l10n.languageEnglish)),
                DropdownMenuItem(
                    value: 'ar', child: Text(l10n.languageArabic)),
              ],
              onChanged: (v) {
                if (v != null) setAppLocale(ref, v);
              },
            ),
          ),

          // ── Security ──────────────────────────
          _SectionHeader(l10n.security),
          Builder(
            builder: (context) {
              final pushOn = ref.watch(pushNotificationsEnabledProvider);
              final pushIssue = ref.watch(pushRegistrationIssueProvider);
              final subtitle = pushIssue ??
                  (pushOn
                      ? 'Trip alerts and dispatch messages'
                      : 'Off — you will not receive push alerts');
              return Column(
                children: [
                  SwitchListTile(
                    secondary: _IconBox(
                      icon: pushOn
                          ? Icons.notifications_active_outlined
                          : Icons.notifications_off_outlined,
                      color: pushOn ? AppColors.success : AppColors.warning,
                    ),
                    title: Text(
                      'Push notifications',
                      style: TextStyle(
                        fontWeight: FontWeight.w500,
                        color: context.sg.textPrimary,
                      ),
                    ),
                    subtitle: Text(
                      subtitle,
                      style: TextStyle(
                        fontSize: 12,
                        color: pushIssue != null
                            ? AppColors.warning
                            : context.sg.textSecondary,
                      ),
                    ),
                    value: pushOn,
                    activeThumbColor: AppColors.primary,
                    activeTrackColor: AppColors.primaryLight,
                    onChanged: (v) => _togglePushNotifications(context, ref, v),
                  ),
                  if (pushIssue != null)
                    Align(
                      alignment: Alignment.centerRight,
                      child: TextButton(
                        onPressed: () {
                          PushRegistrationService.instance.clearHint();
                          ref
                              .read(pushRegistrationIssueProvider.notifier)
                              .state = null;
                        },
                        child: const Text('Dismiss warning'),
                      ),
                    ),
                ],
              );
            },
          ),
          SwitchListTile(
            secondary: const _IconBox(
              icon: Icons.fingerprint_rounded,
              color: AppColors.success,
            ),
            title: Text(l10n.biometricLock,
                style: TextStyle(
                    fontWeight: FontWeight.w500, color: context.sg.textPrimary)),
            subtitle: Text(l10n.biometricLockSubtitle,
                style: TextStyle(
                    fontSize: 12, color: context.sg.textSecondary)),
            value: biometricOn,
            activeThumbColor: AppColors.primary,
            activeTrackColor: AppColors.primaryLight,
            onChanged: (v) => _toggleBiometric(context, ref, v),
          ),
          _SettingTile(
            icon: Icons.security,
            iconColor: AppColors.primary,
            title: 'Security status',
            subtitle: 'Root, jailbreak, emulator, pinning',
            onTap: () => context.push('/security'),
          ),

          // ── Activity ──────────────────────────
          _SectionHeader(l10n.activity),
          _SettingTile(
            icon: Icons.cloud_sync_outlined,
            iconColor: AppColors.warning,
            title: l10n.offlineQueue,
            subtitle: l10n.offlineQueueSubtitle,
            onTap: () => context.push('/offline-queue'),
          ),
          _SettingTile(
            icon: Icons.gps_fixed,
            iconColor: AppColors.primary,
            title: 'Background GPS',
            subtitle: 'Stop live tracking / open live map',
            onTap: () async {
              final tracking = BackgroundGpsTracker.instance.isTracking;
              if (tracking) {
                final stop = await showDialog<bool>(
                  context: context,
                  builder: (dialogContext) => AlertDialog(
                    title: const Text('Stop background GPS?'),
                    content: const Text(
                      'Dispatch will stop receiving live location until you start tracking again.',
                    ),
                    actions: [
                      TextButton(
                        onPressed: () =>
                            Navigator.of(dialogContext).pop(false),
                        child: Text(l10n.cancel),
                      ),
                      FilledButton(
                        onPressed: () =>
                            Navigator.of(dialogContext).pop(true),
                        child: const Text('Stop'),
                      ),
                    ],
                  ),
                );
                if (stop == true) {
                  await BackgroundGpsTracker.instance.stop();
                  if (context.mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Background GPS stopped')),
                    );
                  }
                }
              } else {
                context.push('/live');
              }
            },
          ),
          _SettingTile(
            icon: Icons.history,
            iconColor: AppColors.accent,
            title: 'Activity Timeline',
            subtitle: 'View your trip and attendance history',
            onTap: () => context.push('/timeline'),
          ),
          _SettingTile(
            icon: Icons.fingerprint,
            iconColor: AppColors.success,
            title: 'Attendance',
            subtitle: 'Clock in / clock out',
            onTap: () => context.push('/attendance'),
          ),

          // ── App ───────────────────────────────
          _SectionHeader(l10n.appSection),
          _SettingTile(
            icon: Icons.privacy_tip_outlined,
            iconColor: AppColors.accent,
            title: 'Privacy Policy',
            onTap: () => context.push('/legal/privacy'),
          ),
          _SettingTile(
            icon: Icons.gavel_outlined,
            iconColor: context.sg.textSecondary,
            title: 'Terms of Service',
            onTap: () => context.push('/legal/terms'),
          ),
          _SettingTile(
            icon: Icons.info_outline,
            iconColor: context.sg.textSecondary,
            title: 'Version',
            trailing: FutureBuilder<String>(
              future: AppVersionService.displayVersion(),
              builder: (context, snap) => Text(
                snap.data ?? AppVersionService.currentVersion,
                style: TextStyle(
                    color: context.sg.textSecondary, fontSize: 13),
              ),
            ),
          ),
          _SettingTile(
            icon: Icons.dns_outlined,
            iconColor: AppColors.textMuted,
            title: l10n.environmentLabel(AppConfig.environment.name),
          ),
          _SettingTile(
            icon: Icons.system_update_outlined,
            iconColor: AppColors.accent,
            title: 'Check for Updates',
            onTap: () async {
              final dio = ref.read(dioProvider);
              await AppVersionService.checkAndPrompt(context, dio);
            },
          ),

          const SizedBox(height: 24),

          // ── Danger ───────────────────────────
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: OutlinedButton.icon(
              onPressed: () => _confirmLogout(context, ref),
              icon: const Icon(Icons.logout, color: AppColors.error),
              label: Text(l10n.signOut,
                  style: const TextStyle(color: AppColors.error)),
              style: OutlinedButton.styleFrom(
                side: const BorderSide(color: AppColors.error),
                padding: const EdgeInsets.symmetric(vertical: 14),
              ),
            ),
          ),
          const SizedBox(height: 24),
        ],
      ),
    );
  }

  Future<void> _togglePushNotifications(
    BuildContext context,
    WidgetRef ref,
    bool enable,
  ) async {
    final dio = ref.read(dioProvider);
    final session = ref.read(authRepositoryProvider).session;
    final appName = session != null &&
            (session.isDriverOnly || session.authMode == AuthMode.driver)
        ? 'driver'
        : 'fleet';

    if (!enable) {
      await PushRegistrationService.instance.disableFromSettings();
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Push notifications turned off')),
        );
      }
      return;
    }

    final ok = await PushRegistrationService.instance.enableFromSettings(
      dio: dio,
      appName: appName,
    );
    if (!context.mounted) return;
    if (ok) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Push notifications enabled'),
          backgroundColor: AppColors.success,
        ),
      );
    } else {
      final issue = ref.read(pushRegistrationIssueProvider);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            issue ??
                'Could not enable notifications. Allow permission in system Settings, '
                    'or add android/app/google-services.json and rebuild.',
          ),
          backgroundColor: AppColors.error,
          duration: const Duration(seconds: 6),
        ),
      );
    }
  }

  Future<void> _toggleBiometric(
    BuildContext context,
    WidgetRef ref,
    bool enable,
  ) async {
    final l10n = AppLocalizations.of(context);
    if (!enable) {
      await setBiometricLockEnabled(ref, false);
      return;
    }
    final bio = ref.read(biometricAuthServiceProvider);
    final supported = await bio.isSupported();
    if (!supported) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(l10n.biometricUnavailable)),
        );
      }
      return;
    }
    final ok = await bio.authenticate(reason: l10n.biometricLockSubtitle);
    if (!ok) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(l10n.biometricEnableFailed)),
        );
      }
      return;
    }
    await setBiometricLockEnabled(ref, true);
  }

  // ── Change Password ─────────────────────────────
  void _showChangePasswordSheet(BuildContext context, WidgetRef ref) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => _ChangePasswordSheet(ref: ref),
    );
  }

  // ── Logout ───────────────────────────────────────
  Future<void> _confirmLogout(BuildContext context, WidgetRef ref) async {
    final l10n = AppLocalizations.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.signOutConfirmTitle),
        content: Text(l10n.signOutConfirmBody),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            style: FilledButton.styleFrom(backgroundColor: AppColors.error),
            child: Text(l10n.signOut),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      await ref.read(authRepositoryProvider).logout();
      if (context.mounted) context.go('/login');
    }
  }
}

// ── Change Password bottom sheet ─────────────────────────
class _ChangePasswordSheet extends StatefulWidget {
  const _ChangePasswordSheet({required this.ref});
  final WidgetRef ref;

  @override
  State<_ChangePasswordSheet> createState() => _ChangePasswordSheetState();
}

class _ChangePasswordSheetState extends State<_ChangePasswordSheet> {
  final _formKey = GlobalKey<FormState>();
  final _currentCtrl = TextEditingController();
  final _newCtrl = TextEditingController();
  final _confirmCtrl = TextEditingController();
  bool _loading = false;

  @override
  void dispose() {
    _currentCtrl.dispose();
    _newCtrl.dispose();
    _confirmCtrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _loading = true);
    try {
      await widget.ref.read(dioProvider).put(
        ApiEndpoints.changePassword,
        data: {
          'currentPassword': _currentCtrl.text,
          'newPassword': _newCtrl.text,
        },
      );
      if (mounted) {
        Navigator.pop(context);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Password changed successfully'),
            backgroundColor: AppColors.success,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.toString()), backgroundColor: AppColors.error),
        );
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final bottom = MediaQuery.of(context).viewInsets.bottom;
    return Padding(
      padding: EdgeInsets.fromLTRB(24, 20, 24, 24 + bottom),
      child: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Handle
            Center(
              child: Container(
                width: 36,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.divider,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: 16),
            Text('Change Password',
                style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                    color: context.sg.textPrimary)),
            const SizedBox(height: 20),
            SgSheetTextFormField(
              controller: _currentCtrl,
              label: 'Current Password',
              obscureText: true,
              keyboardType: TextInputType.visiblePassword,
              prefixIcon: Icons.lock_outline_rounded,
              validator: (v) =>
                  (v == null || v.isEmpty) ? 'Enter current password' : null,
            ),
            const SizedBox(height: 12),
            SgSheetTextFormField(
              controller: _newCtrl,
              label: 'New Password',
              obscureText: true,
              keyboardType: TextInputType.visiblePassword,
              prefixIcon: Icons.lock_outline_rounded,
              validator: (v) {
                if (v == null || v.isEmpty) return 'Enter new password';
                if (v.length < 8) return 'Minimum 8 characters';
                return null;
              },
            ),
            const SizedBox(height: 12),
            SgSheetTextFormField(
              controller: _confirmCtrl,
              label: 'Confirm New Password',
              obscureText: true,
              keyboardType: TextInputType.visiblePassword,
              prefixIcon: Icons.lock_outline_rounded,
              validator: (v) =>
                  v != _newCtrl.text ? 'Passwords do not match' : null,
            ),
            const SizedBox(height: 20),
            SizedBox(
              width: double.infinity,
              height: 48,
              child: FilledButton(
                onPressed: _loading ? null : _submit,
                child: _loading
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(
                            strokeWidth: 2, color: Colors.white),
                      )
                    : const Text('Update Password',
                        style: TextStyle(fontWeight: FontWeight.w600)),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ── Supporting widgets ────────────────────────────────────
class _SectionHeader extends StatelessWidget {
  const _SectionHeader(this.title);
  final String title;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(16, 20, 16, 6),
        child: Text(
          title.toUpperCase(),
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w700,
            color: context.sg.textSecondary,
            letterSpacing: 1.2,
          ),
        ),
      );
}

class _SettingTile extends StatelessWidget {
  const _SettingTile({
    required this.icon,
    required this.iconColor,
    required this.title,
    this.subtitle,
    this.trailing,
    this.onTap,
  });

  final IconData icon;
  final Color iconColor;
  final String title;
  final String? subtitle;
  final Widget? trailing;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => ListTile(
        leading: Container(
          width: 36,
          height: 36,
          decoration: BoxDecoration(
            color: iconColor.withValues(alpha: 0.1),
            borderRadius: BorderRadius.circular(8),
          ),
          child: Icon(icon, color: iconColor, size: 20),
        ),
        title: Text(title,
            style: TextStyle(
                fontWeight: FontWeight.w500, color: context.sg.textPrimary)),
        subtitle: subtitle != null
            ? Text(subtitle!,
                style:
                    TextStyle(fontSize: 12, color: context.sg.textSecondary))
            : null,
        trailing: trailing ?? (onTap != null ? const Icon(Icons.chevron_right) : null),
        onTap: onTap,
      );
}

class _IconBox extends StatelessWidget {
  const _IconBox({required this.icon, required this.color});
  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
        width: 36,
        height: 36,
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(8),
        ),
        child: Icon(icon, color: color, size: 20),
      );
}
