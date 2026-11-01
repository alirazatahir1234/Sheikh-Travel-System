import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/config/app_config.dart';
import '../../../core/constants/app_brand.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/offline/connectivity_provider.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../../../core/providers/locale_provider.dart';
import '../../../core/providers/theme_provider.dart';
import '../../../core/security/biometric_auth_service.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../features/dashboard/domain/dashboard_role.dart';
import '../../../features/notifications/presentation/notifications_notifier.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../../gps/services/background_gps_tracker.dart';
import '../../notifications/services/push_registration_service.dart';
import '../domain/settings_access.dart';
import '../services/app_version_service.dart';

/// Premium Settings body surface (mock parity).
const _settingsBodyBg = Color(0xFFF2F6F8);
const _settingsBodyBgDark = Color(0xFF0E141B);
const _statusPillBg = Color(0xFFD1FAE5);
const _statusPillFg = Color(0xFF065F46);
const _syncPanelBg = Color(0xFFE8F8F3);
const _syncPanelBgDark = Color(0xFF13241F);

class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final isDark = ref.watch(darkModeProvider);
    final localeCode = ref.watch(localeCodeProvider);
    final biometricOn = ref.watch(biometricLockEnabledProvider);
    final session = ref.watch(fleetSessionProvider);
    final online = ref.watch(isOnlineProvider);
    final lastSyncAt = ref.watch(lastSuccessfulSyncAtProvider);
    final syncStats = ref.watch(offlineSyncStatsProvider);
    final pushOn = ref.watch(pushNotificationsEnabledProvider);
    final unreadAsync = ref.watch(unreadNotificationsCountProvider);
    final unread = unreadAsync.valueOrNull ?? 0;
    final bodyBg = isDark ? _settingsBodyBgDark : _settingsBodyBg;
    final tracking = BackgroundGpsTracker.instance.isTracking;
    final languageLabel = localeCode == 'ar' ? 'العربية' : 'English';

    return Scaffold(
      backgroundColor: bodyBg,
      body: CustomScrollView(
        slivers: [
          SliverToBoxAdapter(
            child: _SettingsHeader(
              unreadCount: unread,
              onNotifications: () => context.push('/notifications'),
            ),
          ),
          SliverPadding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
            sliver: SliverList(
              delegate: SliverChildListDelegate([
                if (session != null &&
                    SettingsAccess.canSeeProfileAccount(session))
                  _ProfileCard(
                    session: session,
                    onTap: () => context.push('/profile'),
                  ),
                if (session != null &&
                    SettingsAccess.canSeeProfileAccount(session))
                  const SizedBox(height: 14),
                _QuickSettingsCard(
                  darkModeOn: isDark,
                  languageLabel: languageLabel,
                  notificationsOn: pushOn,
                  onDarkMode: () => _showThemeSheet(context, ref, isDark),
                  onLanguage: () =>
                      _showLanguageSheet(context, ref, localeCode),
                  onNotifications: () => _showAlertsSheet(context, ref),
                ),
                const SizedBox(height: 14),
                _AccountSecurityCard(
                  changePasswordTitle: l10n.changePassword,
                  biometricOn: biometricOn,
                  showSecurityCenter:
                      SettingsAccess.canSeeSecurityPermissions(session),
                  showDiagnostics:
                      SettingsAccess.canSeeSecurityDiagnostics(session),
                  diagnosticsTitle: l10n.securityDiagnostics,
                  diagnosticsSubtitle: l10n.securityDiagnosticsSubtitle,
                  onChangePassword: () =>
                      _showChangePasswordSheet(context, ref),
                  onBiometricChanged: (v) =>
                      _toggleBiometric(context, ref, v),
                  onSecurityCenter: () =>
                      context.push('/settings/security-permissions'),
                  onDiagnostics: () => context.push('/security'),
                ),
                if (SettingsAccess.canSeeSyncStatus(session)) ...[
                  const SizedBox(height: 14),
                  _TrackingSyncCard(
                    onlineLabel: l10n.online,
                    offlineLabel: l10n.offline,
                    online: online,
                    pending: syncStats.pending,
                    synced: syncStats.syncedLifetime,
                    failed: syncStats.failed,
                    lastSyncAt: lastSyncAt,
                    showProgress: syncStats.showProgress,
                    isSyncing: syncStats.isSyncing,
                    tracking: tracking,
                    showLocation:
                        SettingsAccess.canSeeLocationTracking(session),
                    showTimeline:
                        SettingsAccess.canSeeActivityTimeline(session),
                    showAttendance: SettingsAccess.canSeeAttendance(session),
                    onSyncTap: () => context.push('/offline-queue'),
                    onLocation: () => context.push('/settings/location'),
                    onTimeline: () => context.push('/timeline'),
                    onAttendance: () => context.push('/attendance'),
                  ),
                ],
                const SizedBox(height: 14),
                _SupportInfoCard(
                  privacyTitle: l10n.privacyPolicy,
                  termsTitle: l10n.termsOfService,
                  versionTitle: l10n.version,
                  showEnvironment:
                      SettingsAccess.canSeeEnvironmentLabel(session),
                  onHelp: () => context.push('/help-support'),
                  onPrivacy: () => context.push('/legal/privacy'),
                  onTerms: () => context.push('/legal/terms'),
                  onCheckUpdates: () async {
                    final dio = ref.read(dioProvider);
                    await AppVersionService.checkAndPrompt(context, dio);
                  },
                ),
                const SizedBox(height: 20),
                _SignOutButton(
                  label: l10n.signOut,
                  onPressed: () => _confirmLogout(context, ref),
                ),
              ]),
            ),
          ),
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
            issue?.message ??
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

  void _showThemeSheet(BuildContext context, WidgetRef ref, bool isDark) {
    final l10n = AppLocalizations.of(context);
    showModalBottomSheet(
      context: context,
      showDragHandle: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(8, 0, 8, 16),
            child: SwitchListTile(
              secondary: Icon(
                isDark ? Icons.dark_mode_outlined : Icons.light_mode_outlined,
                color: AppColors.primary,
              ),
              title: Text(l10n.darkMode),
              subtitle: Text(l10n.darkModeSubtitle),
              value: isDark,
              activeThumbColor: AppColors.primary,
              activeTrackColor: AppColors.primaryLight,
              onChanged: (v) {
                ref.read(darkModeProvider.notifier).state = v;
                ref.read(prefsBoxProvider).put('darkMode', v);
                Navigator.pop(ctx);
              },
            ),
          ),
        );
      },
    );
  }

  void _showLanguageSheet(
    BuildContext context,
    WidgetRef ref,
    String localeCode,
  ) {
    final l10n = AppLocalizations.of(context);
    showModalBottomSheet(
      context: context,
      showDragHandle: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) {
        final current = localeCode == 'ar' ? 'ar' : 'en';
        return SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ListTile(
                leading: Icon(
                  current == 'en'
                      ? Icons.radio_button_checked
                      : Icons.radio_button_off,
                  color: AppColors.primary,
                ),
                title: Text(l10n.languageEnglish),
                onTap: () {
                  setAppLocale(ref, 'en');
                  Navigator.pop(ctx);
                },
              ),
              ListTile(
                leading: Icon(
                  current == 'ar'
                      ? Icons.radio_button_checked
                      : Icons.radio_button_off,
                  color: AppColors.primary,
                ),
                title: Text(l10n.languageArabic),
                onTap: () {
                  setAppLocale(ref, 'ar');
                  Navigator.pop(ctx);
                },
              ),
              const SizedBox(height: 8),
            ],
          ),
        );
      },
    );
  }

  void _showAlertsSheet(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final pushOn = ref.read(pushNotificationsEnabledProvider);
    final pushIssue = ref.read(pushRegistrationIssueProvider);
    showModalBottomSheet(
      context: context,
      showDragHandle: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(8, 0, 8, 16),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                SwitchListTile(
                  secondary: Icon(
                    pushOn
                        ? Icons.notifications_active_outlined
                        : Icons.notifications_off_outlined,
                    color: pushOn ? AppColors.success : AppColors.warning,
                  ),
                  title: Text(l10n.pushNotifications),
                  subtitle: Text(
                    pushIssue?.message ??
                        (pushOn
                            ? l10n.pushNotificationsOn
                            : l10n.pushNotificationsOff),
                  ),
                  value: pushOn,
                  activeThumbColor: AppColors.primary,
                  activeTrackColor: AppColors.primaryLight,
                  onChanged: (v) async {
                    Navigator.pop(ctx);
                    await _togglePushNotifications(context, ref, v);
                  },
                ),
              ],
            ),
          ),
        );
      },
    );
  }

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

// ── Header ─────────────────────────────────────────────────────────────

class _SettingsHeader extends StatelessWidget {
  const _SettingsHeader({
    required this.unreadCount,
    required this.onNotifications,
  });

  final int unreadCount;
  final VoidCallback onNotifications;

  @override
  Widget build(BuildContext context) {
    final top = MediaQuery.paddingOf(context).top;
    return Container(
      width: double.infinity,
      color: AppColors.navy,
      padding: EdgeInsets.fromLTRB(20, top + 10, 12, 22),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(10),
                child: Image.asset(
                  AppBrand.logoAsset,
                  width: 36,
                  height: 36,
                  fit: BoxFit.cover,
                  errorBuilder: (_, __, ___) => Container(
                    width: 36,
                    height: 36,
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.25),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Icon(
                      Icons.directions_bus_filled_rounded,
                      color: Colors.white,
                      size: 20,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 10),
              const Expanded(
                child: Text(
                  AppBrand.productName,
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                    letterSpacing: -0.2,
                  ),
                ),
              ),
              IconButton(
                onPressed: onNotifications,
                tooltip: 'Notifications',
                icon: Badge(
                  isLabelVisible: unreadCount > 0,
                  label: Text(
                    unreadCount > 99 ? '99+' : '$unreadCount',
                    style: const TextStyle(fontSize: 10),
                  ),
                  child: const Icon(
                    Icons.notifications_outlined,
                    color: Colors.white,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 18),
          const Text(
            'Settings',
            style: TextStyle(
              color: Colors.white,
              fontSize: 28,
              fontWeight: FontWeight.w800,
              letterSpacing: -0.4,
              height: 1.15,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            'Manage your account, app and tracking preferences.',
            style: TextStyle(
              color: Color(0xFF9BB0C0),
              fontSize: 14,
              fontWeight: FontWeight.w500,
              height: 1.35,
            ),
          ),
        ],
      ),
    );
  }
}

class _ProfileCard extends StatelessWidget {
  const _ProfileCard({required this.session, required this.onTap});

  final FleetSession session;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final role = DashboardRoleX.fromNavRole(session.primaryNavRole).label;
    final company = (session.companyName?.trim().isNotEmpty ?? false)
        ? session.companyName!.trim()
        : AppBrand.productName;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadii.lg),
        child: Ink(
          decoration: BoxDecoration(
            color: context.sg.card,
            borderRadius: BorderRadius.circular(AppRadii.lg),
            boxShadow: isDark ? null : AppShadows.card,
            border: isDark
                ? Border.all(color: context.sg.border.withValues(alpha: 0.55))
                : null,
          ),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(14, 14, 10, 14),
            child: Row(
              children: [
                CircleAvatar(
                  radius: 28,
                  backgroundColor: AppColors.primary,
                  child: Text(
                    _initials(session.displayName),
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w800,
                      fontSize: 16,
                      letterSpacing: 0.4,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        session.displayName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: context.sg.textPrimary,
                          fontWeight: FontWeight.w700,
                          fontSize: 17,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        role,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: context.sg.textSecondary,
                          fontSize: 13,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                      Text(
                        company,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          color: context.sg.textMuted,
                          fontSize: 12,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 10,
                          vertical: 4,
                        ),
                        decoration: BoxDecoration(
                          color: _statusPillBg,
                          borderRadius: BorderRadius.circular(AppRadii.pill),
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Container(
                              width: 6,
                              height: 6,
                              decoration: const BoxDecoration(
                                color: _statusPillFg,
                                shape: BoxShape.circle,
                              ),
                            ),
                            const SizedBox(width: 6),
                            const Text(
                              'Active',
                              style: TextStyle(
                                color: _statusPillFg,
                                fontSize: 11,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                Icon(
                  Icons.chevron_right_rounded,
                  color: context.sg.textMuted,
                  size: 26,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  String _initials(String name) {
    final parts =
        name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.length >= 2) {
      return '${parts[0][0]}${parts[1][0]}'.toUpperCase();
    }
    return name.isNotEmpty ? name[0].toUpperCase() : '?';
  }
}

// ── Cards ──────────────────────────────────────────────────────────────

class _SettingsCard extends StatelessWidget {
  const _SettingsCard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: p.card,
        borderRadius: BorderRadius.circular(AppRadii.lg),
        boxShadow: isDark ? null : AppShadows.card,
        border: isDark
            ? Border.all(color: p.border.withValues(alpha: 0.55))
            : null,
      ),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
        child: child,
      ),
    );
  }
}

class _CardHeader extends StatelessWidget {
  const _CardHeader({required this.title, this.icon});

  final String title;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        if (icon != null) ...[
          Icon(icon, size: 18, color: AppColors.primary),
          const SizedBox(width: 8),
        ],
        Expanded(
          child: Text(
            title,
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: context.sg.textPrimary,
            ),
          ),
        ),
      ],
    );
  }
}

class _QuickSettingsCard extends StatelessWidget {
  const _QuickSettingsCard({
    required this.darkModeOn,
    required this.languageLabel,
    required this.notificationsOn,
    required this.onDarkMode,
    required this.onLanguage,
    required this.onNotifications,
  });

  final bool darkModeOn;
  final String languageLabel;
  final bool notificationsOn;
  final VoidCallback onDarkMode;
  final VoidCallback onLanguage;
  final VoidCallback onNotifications;

  @override
  Widget build(BuildContext context) {
    return _SettingsCard(
      child: Column(
        children: [
          const _CardHeader(
            title: 'Quick Settings',
            icon: Icons.bolt_outlined,
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: _QuickChip(
                  icon: Icons.dark_mode_outlined,
                  label: 'Dark Mode',
                  status: darkModeOn ? 'On' : 'Off',
                  onTap: onDarkMode,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _QuickChip(
                  icon: Icons.language_outlined,
                  label: 'Language',
                  status: languageLabel,
                  onTap: onLanguage,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: _QuickChip(
                  icon: Icons.notifications_outlined,
                  label: 'Notifications',
                  status: notificationsOn ? 'On' : 'Off',
                  onTap: onNotifications,
                ),
              ),
            ],
          ),
          const SizedBox(height: 4),
        ],
      ),
    );
  }
}

class _QuickChip extends StatelessWidget {
  const _QuickChip({
    required this.icon,
    required this.label,
    required this.status,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final String status;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(14),
        child: Ink(
          decoration: BoxDecoration(
            color: isDark
                ? AppColors.primary.withValues(alpha: 0.14)
                : const Color(0xFFE6F7F4),
            borderRadius: BorderRadius.circular(14),
          ),
          child: ConstrainedBox(
            constraints: const BoxConstraints(minHeight: 88),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 6),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(icon, color: AppColors.primary, size: 22),
                  const SizedBox(height: 6),
                  Text(
                    label,
                    textAlign: TextAlign.center,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w600,
                      color: context.sg.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    status,
                    textAlign: TextAlign.center,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      color: AppColors.primary,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _AccountSecurityCard extends StatelessWidget {
  const _AccountSecurityCard({
    required this.changePasswordTitle,
    required this.biometricOn,
    required this.showSecurityCenter,
    required this.showDiagnostics,
    required this.diagnosticsTitle,
    required this.diagnosticsSubtitle,
    required this.onChangePassword,
    required this.onBiometricChanged,
    required this.onSecurityCenter,
    required this.onDiagnostics,
  });

  final String changePasswordTitle;
  final bool biometricOn;
  final bool showSecurityCenter;
  final bool showDiagnostics;
  final String diagnosticsTitle;
  final String diagnosticsSubtitle;
  final VoidCallback onChangePassword;
  final ValueChanged<bool> onBiometricChanged;
  final VoidCallback onSecurityCenter;
  final VoidCallback onDiagnostics;

  @override
  Widget build(BuildContext context) {
    return _SettingsCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardHeader(
            title: 'Account & Security',
            icon: Icons.shield_outlined,
          ),
          const SizedBox(height: 8),
          _NavRow(
            icon: Icons.lock_outline_rounded,
            title: changePasswordTitle,
            subtitle: 'Update your account password',
            onTap: onChangePassword,
          ),
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Row(
              children: [
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: AppColors.primary.withValues(alpha: 0.10),
                    borderRadius: BorderRadius.circular(11),
                    border: Border.all(
                      color: AppColors.primary.withValues(alpha: 0.22),
                    ),
                  ),
                  child: const Icon(
                    Icons.fingerprint_rounded,
                    color: AppColors.primary,
                    size: 20,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Biometric Lock',
                        style: TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                          color: context.sg.textPrimary,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        'Use Face ID / fingerprint to open app',
                        style: TextStyle(
                          fontSize: 12,
                          color: context.sg.textSecondary,
                          height: 1.25,
                        ),
                      ),
                    ],
                  ),
                ),
                Switch.adaptive(
                  value: biometricOn,
                  activeThumbColor: AppColors.primary,
                  onChanged: onBiometricChanged,
                ),
              ],
            ),
          ),
          if (showSecurityCenter)
            _NavRow(
              icon: Icons.verified_user_outlined,
              title: 'Security Center',
              subtitle: 'Device, app and account security',
              onTap: onSecurityCenter,
            ),
          if (showDiagnostics)
            _NavRow(
              icon: Icons.security_outlined,
              title: diagnosticsTitle,
              subtitle: diagnosticsSubtitle,
              onTap: onDiagnostics,
            ),
        ],
      ),
    );
  }
}

class _TrackingSyncCard extends StatelessWidget {
  const _TrackingSyncCard({
    required this.onlineLabel,
    required this.offlineLabel,
    required this.online,
    required this.pending,
    required this.synced,
    required this.failed,
    required this.lastSyncAt,
    required this.showProgress,
    required this.isSyncing,
    required this.tracking,
    required this.showLocation,
    required this.showTimeline,
    required this.showAttendance,
    required this.onSyncTap,
    required this.onLocation,
    required this.onTimeline,
    required this.onAttendance,
  });

  final String onlineLabel;
  final String offlineLabel;
  final bool online;
  final int pending;
  final int synced;
  final int failed;
  final DateTime? lastSyncAt;
  final bool showProgress;
  final bool isSyncing;
  final bool tracking;
  final bool showLocation;
  final bool showTimeline;
  final bool showAttendance;
  final VoidCallback onSyncTap;
  final VoidCallback onLocation;
  final VoidCallback onTimeline;
  final VoidCallback onAttendance;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final totalActive = pending + failed;
    final progress = !showProgress
        ? null
        : totalActive <= 0
            ? 1.0
            : (synced / (synced + totalActive)).clamp(0.08, 0.95).toDouble();

    return _SettingsCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardHeader(
            title: 'Tracking & Synchronization',
            icon: Icons.place_outlined,
          ),
          const SizedBox(height: 12),
          Material(
            color: Colors.transparent,
            child: InkWell(
              onTap: onSyncTap,
              borderRadius: BorderRadius.circular(14),
              child: Ink(
                decoration: BoxDecoration(
                  color: isDark ? _syncPanelBgDark : _syncPanelBg,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(14, 14, 14, 12),
                  child: Column(
                    children: [
                      Row(
                        children: [
                          Icon(
                            online
                                ? Icons.wifi_tethering_rounded
                                : Icons.wifi_off_rounded,
                            size: 20,
                            color: online
                                ? AppColors.success
                                : AppColors.warning,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  'Device Connectivity',
                                  style: TextStyle(
                                    fontSize: 14,
                                    fontWeight: FontWeight.w700,
                                    color: context.sg.textPrimary,
                                  ),
                                ),
                                Text(
                                  'Synchronization status and data queue',
                                  style: TextStyle(
                                    fontSize: 11,
                                    color: context.sg.textSecondary,
                                  ),
                                ),
                              ],
                            ),
                          ),
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 10,
                              vertical: 4,
                            ),
                            decoration: BoxDecoration(
                              color: online
                                  ? _statusPillBg
                                  : AppColors.warning.withValues(alpha: 0.18),
                              borderRadius:
                                  BorderRadius.circular(AppRadii.pill),
                            ),
                            child: Text(
                              online ? onlineLabel : offlineLabel,
                              style: TextStyle(
                                color: online
                                    ? _statusPillFg
                                    : AppColors.warning,
                                fontSize: 11,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 14),
                      Row(
                        children: [
                          Expanded(
                            child: _SyncMetric(
                              icon: Icons.sync_rounded,
                              iconColor: AppColors.primary,
                              value: '$pending',
                              label: 'Pending',
                            ),
                          ),
                          Expanded(
                            child: _SyncMetric(
                              icon: Icons.check_circle_outline_rounded,
                              iconColor: AppColors.success,
                              value: '$synced',
                              label: 'Synced',
                            ),
                          ),
                          Expanded(
                            child: _SyncMetric(
                              icon: Icons.error_outline_rounded,
                              iconColor: AppColors.error,
                              value: '$failed',
                              label: 'Failed',
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          Icon(
                            Icons.check_circle_rounded,
                            size: 16,
                            color: online
                                ? AppColors.success
                                : context.sg.textMuted,
                          ),
                          const SizedBox(width: 6),
                          Expanded(
                            child: Text(
                              'Last successful sync: ${_relativeSyncLabel(lastSyncAt)}',
                              style: TextStyle(
                                fontSize: 12,
                                fontWeight: FontWeight.w500,
                                color: context.sg.textSecondary,
                              ),
                            ),
                          ),
                        ],
                      ),
                      if (showProgress && progress != null) ...[
                        const SizedBox(height: 12),
                        ClipRRect(
                          borderRadius: BorderRadius.circular(8),
                          child: LinearProgressIndicator(
                            value: isSyncing ? null : progress,
                            minHeight: 8,
                            backgroundColor: isDark
                                ? Colors.white.withValues(alpha: 0.08)
                                : AppColors.primary.withValues(alpha: 0.15),
                            color: AppColors.primary,
                          ),
                        ),
                        const SizedBox(height: 6),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: Text(
                            isSyncing
                                ? 'Syncing…'
                                : 'Synchronization in progress',
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w500,
                              color: AppColors.primary.withValues(alpha: 0.85),
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ),
          ),
          if (showLocation || showTimeline || showAttendance)
            const SizedBox(height: 6),
          if (showLocation)
            _NavRow(
              icon: Icons.location_on_outlined,
              title: 'Location & GPS',
              subtitle: tracking
                  ? 'Tracking active'
                  : 'Permissions and tracking status',
              onTap: onLocation,
            ),
          if (showTimeline)
            _NavRow(
              icon: Icons.history_rounded,
              title: 'Activity Timeline',
              subtitle: 'Trips, routes and activity history',
              onTap: onTimeline,
            ),
          if (showAttendance)
            _NavRow(
              icon: Icons.access_time_outlined,
              title: 'Attendance',
              subtitle: 'Clock in / clock out and work hours',
              onTap: onAttendance,
            ),
        ],
      ),
    );
  }

  String _relativeSyncLabel(DateTime? at) {
    if (at == null) return 'Never';
    final diff = DateTime.now().difference(at.toLocal());
    if (diff.inSeconds < 60) return 'Just now';
    if (diff.inMinutes < 60) return '${diff.inMinutes} min ago';
    if (diff.inHours < 24) return '${diff.inHours}h ago';
    return '${diff.inDays}d ago';
  }
}

class _SyncMetric extends StatelessWidget {
  const _SyncMetric({
    required this.icon,
    required this.iconColor,
    required this.value,
    required this.label,
  });

  final IconData icon;
  final Color iconColor;
  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Icon(icon, color: iconColor, size: 20),
        const SizedBox(height: 4),
        Text(
          value,
          style: TextStyle(
            fontSize: 20,
            fontWeight: FontWeight.w800,
            color: context.sg.textPrimary,
          ),
        ),
        Text(
          label,
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w500,
            color: context.sg.textSecondary,
          ),
        ),
      ],
    );
  }
}

class _SupportInfoCard extends StatelessWidget {
  const _SupportInfoCard({
    required this.privacyTitle,
    required this.termsTitle,
    required this.versionTitle,
    required this.showEnvironment,
    required this.onHelp,
    required this.onPrivacy,
    required this.onTerms,
    required this.onCheckUpdates,
  });

  final String privacyTitle;
  final String termsTitle;
  final String versionTitle;
  final bool showEnvironment;
  final VoidCallback onHelp;
  final VoidCallback onPrivacy;
  final VoidCallback onTerms;
  final VoidCallback onCheckUpdates;

  @override
  Widget build(BuildContext context) {
    return _SettingsCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardHeader(
            title: 'Support & Information',
            icon: Icons.headset_mic_outlined,
          ),
          const SizedBox(height: 4),
          _NavRow(
            icon: Icons.headset_mic_outlined,
            title: 'Help & Support',
            subtitle: 'Get help, contact support or report an issue',
            onTap: onHelp,
          ),
          _NavRow(
            icon: Icons.privacy_tip_outlined,
            title: privacyTitle,
            subtitle: 'How we collect and use your data',
            onTap: onPrivacy,
          ),
          _NavRow(
            icon: Icons.description_outlined,
            title: termsTitle,
            subtitle: 'App usage terms and user responsibilities',
            onTap: onTerms,
          ),
          FutureBuilder<String>(
            future: AppVersionService.displayVersion(),
            builder: (context, snap) {
              final version = snap.data ?? AppVersionService.currentVersion;
              return _NavRow(
                icon: Icons.info_outline_rounded,
                title: versionTitle,
                subtitle: null,
                trailing: Text(
                  '$version (SheikhGo Nexus)',
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: context.sg.textSecondary,
                  ),
                ),
                onTap: onCheckUpdates,
              );
            },
          ),
          _NavRow(
            icon: Icons.system_update_alt_outlined,
            title: 'Check for Updates',
            subtitle: 'Look for a newer app version',
            onTap: onCheckUpdates,
            dense: true,
          ),
          if (showEnvironment) ...[
            const SizedBox(height: 4),
            Text(
              'Environment: ${AppConfig.environmentLabel}',
              style: TextStyle(
                fontSize: 11,
                color: context.sg.textMuted,
              ),
            ),
          ],
          const SizedBox(height: 4),
        ],
      ),
    );
  }
}

class _SignOutButton extends StatelessWidget {
  const _SignOutButton({required this.label, required this.onPressed});

  final String label;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      height: 52,
      child: OutlinedButton.icon(
        onPressed: onPressed,
        icon: const Icon(Icons.logout_rounded, color: AppColors.error, size: 20),
        label: Text(
          label,
          style: const TextStyle(
            color: AppColors.error,
            fontWeight: FontWeight.w700,
            fontSize: 15,
          ),
        ),
        style: OutlinedButton.styleFrom(
          backgroundColor: context.sg.card,
          side: const BorderSide(color: AppColors.error, width: 1.2),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadii.md),
          ),
        ),
      ),
    );
  }
}

class _NavRow extends StatelessWidget {
  const _NavRow({
    required this.icon,
    required this.title,
    this.subtitle,
    required this.onTap,
    this.dense = false,
    this.trailing,
  });

  final IconData icon;
  final String title;
  final String? subtitle;
  final VoidCallback onTap;
  final bool dense;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Padding(
        padding: EdgeInsets.symmetric(vertical: dense ? 10 : 12),
        child: Row(
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.10),
                borderRadius: BorderRadius.circular(11),
                border: Border.all(
                  color: AppColors.primary.withValues(alpha: 0.22),
                ),
              ),
              child: Icon(icon, color: AppColors.primary, size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                      color: p.textPrimary,
                    ),
                  ),
                  if (subtitle != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      subtitle!,
                      style: TextStyle(
                        fontSize: 12,
                        color: p.textSecondary,
                        height: 1.25,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            if (trailing != null) ...[
              trailing!,
              const SizedBox(width: 4),
            ],
            Icon(Icons.chevron_right_rounded, color: p.textMuted, size: 22),
          ],
        ),
      ),
    );
  }
}

// ── Change password sheet (unchanged API) ──────────────────────────────

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
          SnackBar(
            content: Text(e.toString()),
            backgroundColor: AppColors.error,
          ),
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
            Text(
              'Change Password',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w700,
                color: context.sg.textPrimary,
              ),
            ),
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
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Text(
                        'Update Password',
                        style: TextStyle(fontWeight: FontWeight.w600),
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
