import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/config/app_config.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../core/providers/locale_provider.dart';
import '../../../core/providers/theme_provider.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../features/settings/domain/settings_access.dart';
import '../../../features/settings/services/app_version_service.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../domain/driver_profile_model.dart';
import '../domain/profile_access.dart';
import 'profile_notifier.dart';

const _profileBodyBg = Color(0xFFF2F6F8);
const _profileBodyBgDark = Color(0xFF0E141B);
const _statusPillBg = Color(0xFFD1FAE5);
const _statusPillFg = Color(0xFF065F46);

class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profileAsync = ref.watch(profileProvider);
    final session = ref.watch(fleetSessionProvider);
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bodyBg = isDark ? _profileBodyBgDark : _profileBodyBg;

    return Scaffold(
      backgroundColor: bodyBg,
      body: profileAsync.when(
        loading: () => const Column(
          children: [
            _ProfileAppHeader(),
            Expanded(child: Center(child: SgBrandLoader())),
          ],
        ),
        error: (e, _) => Column(
          children: [
            const _ProfileAppHeader(),
            Expanded(
              child: Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(
                        Icons.person_off_outlined,
                        size: 48,
                        color: context.sg.textSecondary,
                      ),
                      const SizedBox(height: 12),
                      Text(
                        ErrorHandler.message(e),
                        textAlign: TextAlign.center,
                        style: TextStyle(color: context.sg.textSecondary),
                      ),
                      const SizedBox(height: 16),
                      SgPrimaryButton(
                        label: 'Retry',
                        onPressed: () =>
                            ref.read(profileProvider.notifier).refresh(),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
        data: (profile) => RefreshIndicator(
          color: AppColors.primary,
          onRefresh: () => ref.read(profileProvider.notifier).refresh(),
          child: CustomScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            slivers: [
              const SliverToBoxAdapter(child: _ProfileAppHeader()),
              SliverPadding(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
                sliver: SliverList(
                  delegate: SliverChildListDelegate([
                    _ProfileHeaderCard(
                      profile: profile,
                      session: session,
                      onEdit: () => _showPersonalInformation(
                        context,
                        profile,
                        session,
                      ),
                    ),
                    if (ProfileAccess.canSeeActiveOrganization(session)) ...[
                      const SizedBox(height: 10),
                      _ActiveOrganizationCard(
                        session: session!,
                        canSwitch: ProfileAccess.canSwitchOrganization(session),
                      ),
                    ],
                    if (_hasAccountOverviewRows(profile, session)) ...[
                      const SizedBox(height: 10),
                      _AccountOverviewCard(
                        profile: profile,
                        session: session,
                      ),
                    ],
                    if (ProfileAccess.canSeeAssignedVehicle(session) &&
                        _hasAssignedVehicle(profile)) ...[
                      const SizedBox(height: 10),
                      _AssignedVehicleCard(profile: profile),
                    ],
                    const SizedBox(height: 10),
                    _AccountPreferencesCard(
                      session: session,
                      onPersonalInfo: () => _showPersonalInformation(
                        context,
                        profile,
                        session,
                      ),
                      onAppSettings: () => context.push('/settings'),
                      onSecurity: () =>
                          context.push('/settings/security-permissions'),
                      onNotifications: () => context.push('/notifications'),
                      onLanguageAppearance: () =>
                          _showLanguageAppearanceSheet(context, ref),
                    ),
                    if (ProfileAccess.canSeeDriverOperations(session)) ...[
                      const SizedBox(height: 10),
                      _DriverOperationsCard(session: session),
                    ],
                    const SizedBox(height: 10),
                    _SupportAboutCard(
                      onHelp: () => context.push('/help-support'),
                      onPrivacy: () => context.push('/legal/privacy'),
                      onTerms: () => context.push('/legal/terms'),
                      onCheckUpdates: () async {
                        final dio = ref.read(dioProvider);
                        await AppVersionService.checkAndPrompt(context, dio);
                      },
                      showEnvironment:
                          SettingsAccess.canSeeEnvironmentLabel(session),
                    ),
                    const SizedBox(height: 16),
                    SafeArea(
                      top: false,
                      child: _SignOutButton(
                        onPressed: () => _signOut(context, ref),
                      ),
                    ),
                  ]),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static bool _hasAssignedVehicle(DriverProfile profile) {
    final name = profile.currentVehicleName?.trim();
    final plate = profile.currentVehiclePlate?.trim();
    return (name != null && name.isNotEmpty) ||
        (plate != null && plate.isNotEmpty);
  }

  static bool _hasAccountOverviewRows(
    DriverProfile profile,
    FleetSession? session,
  ) {
    if (profile.statusName.trim().isNotEmpty) return true;
    if (profile.lastLoginAt != null) return true;
    final security = session?.companyContext?.security;
    if (security != null &&
        (security.mfaRequired ||
            security.passwordExpired ||
            security.passwordDaysRemaining != null)) {
      return true;
    }
    return false;
  }

  Future<void> _signOut(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Sign out'),
        content: const Text('Are you sure you want to sign out?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            style: FilledButton.styleFrom(backgroundColor: AppColors.error),
            child: const Text('Sign out'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    await ref.read(authRepositoryProvider).logout();
    if (context.mounted) context.go('/login');
  }

  void _showPersonalInformation(
    BuildContext context,
    DriverProfile profile,
    FleetSession? session,
  ) {
    final company = session?.companyContext;
    final platformRole = ProfileAccess.platformRoleLabel(session);
    final orgRole = ProfileAccess.organizationRoleLabel(session);

    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (_) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 28),
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Personal Information',
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                    color: context.sg.textPrimary,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  'Details from your authenticated account.',
                  style: TextStyle(
                    fontSize: 12,
                    color: context.sg.textSecondary,
                  ),
                ),
                const SizedBox(height: 16),
                _InfoLine('Full name', profile.fullName),
                if (profile.email != null && profile.email!.trim().isNotEmpty)
                  _InfoLine('Email', profile.email!),
                if (profile.phone.trim().isNotEmpty)
                  _InfoLine('Phone', profile.phone),
                _InfoLine('Account status', profile.statusName),
                if (platformRole != null)
                  _InfoLine('Platform role', platformRole),
                if (company != null && company.companyName.trim().isNotEmpty)
                  _InfoLine('Organization', company.companyName),
                if (orgRole != null) _InfoLine('Organization role', orgRole),
                if (company?.branchName != null)
                  _InfoLine('Branch', company!.branchName!),
                if (company?.departmentName != null)
                  _InfoLine('Department', company!.departmentName!),
                if (company?.jobTitle != null &&
                    company!.jobTitle!.trim().isNotEmpty)
                  _InfoLine('Job title', company.jobTitle!),
                if (!profile.isStaffProfile) ...[
                  if (profile.licenseNumber.trim().isNotEmpty &&
                      profile.licenseNumber != '—')
                    _InfoLine('License', profile.licenseNumber),
                  if (profile.driverCode.trim().isNotEmpty)
                    _InfoLine('Driver ID', profile.driverCode),
                  if (profile.currentVehicleName != null)
                    _InfoLine('Vehicle', profile.currentVehicleName!),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _showLanguageAppearanceSheet(BuildContext context, WidgetRef ref) {
    final isDark = ref.read(darkModeProvider);
    final localeCode = ref.read(localeCodeProvider);

    showModalBottomSheet<void>(
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
                    isDark
                        ? Icons.dark_mode_outlined
                        : Icons.light_mode_outlined,
                    color: AppColors.primary,
                  ),
                  title: const Text('Dark mode'),
                  subtitle: const Text('Appearance'),
                  value: isDark,
                  activeThumbColor: AppColors.primary,
                  activeTrackColor: AppColors.primaryLight,
                  onChanged: (v) {
                    ref.read(darkModeProvider.notifier).state = v;
                    ref.read(prefsBoxProvider).put('darkMode', v);
                    Navigator.pop(ctx);
                  },
                ),
                ListTile(
                  leading: Icon(
                    localeCode == 'en'
                        ? Icons.radio_button_checked
                        : Icons.radio_button_off,
                    color: AppColors.primary,
                  ),
                  title: const Text('English'),
                  onTap: () {
                    setAppLocale(ref, 'en');
                    Navigator.pop(ctx);
                  },
                ),
                ListTile(
                  leading: Icon(
                    localeCode == 'ar'
                        ? Icons.radio_button_checked
                        : Icons.radio_button_off,
                    color: AppColors.primary,
                  ),
                  title: const Text('العربية'),
                  onTap: () {
                    setAppLocale(ref, 'ar');
                    Navigator.pop(ctx);
                  },
                ),
                ListTile(
                  leading: const Icon(
                    Icons.settings_outlined,
                    color: AppColors.primary,
                  ),
                  title: const Text('Open App Settings'),
                  trailing: const Icon(Icons.chevron_right_rounded),
                  onTap: () {
                    Navigator.pop(ctx);
                    context.push('/settings');
                  },
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

// ── Header ─────────────────────────────────────────────────────────────

class _ProfileAppHeader extends StatelessWidget {
  const _ProfileAppHeader();

  @override
  Widget build(BuildContext context) {
    final top = MediaQuery.paddingOf(context).top;
    return Container(
      width: double.infinity,
      color: AppColors.navy,
      padding: EdgeInsets.fromLTRB(20, top + 14, 20, 18),
      child: const Text(
        'Profile',
        style: TextStyle(
          color: Colors.white,
          fontSize: 22,
          fontWeight: FontWeight.w800,
          letterSpacing: -0.2,
        ),
      ),
    );
  }
}

// ── Profile header ─────────────────────────────────────────────────────

class _ProfileHeaderCard extends StatelessWidget {
  const _ProfileHeaderCard({
    required this.profile,
    required this.session,
    required this.onEdit,
  });

  final DriverProfile profile;
  final FleetSession? session;
  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    // Platform role only in this card — org role lives under Active Organization.
    final platformRole = ProfileAccess.platformRoleLabel(session);
    final email = profile.email?.trim();
    final displayName = profile.fullName.isNotEmpty
        ? profile.fullName
        : (session?.displayName ?? 'User');
    final photo = profile.photoUrl ??
        session?.companyContext?.currentUser?.avatarUrl;
    final hasPhoto = photo != null && photo.trim().isNotEmpty;
    final status = profile.statusName.trim();

    return Semantics(
      container: true,
      label: 'Profile header for $displayName'
          '${platformRole != null ? ', platform role $platformRole' : ''}',
      child: _ProfileCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Semantics(
                  label: hasPhoto
                      ? 'Profile photo'
                      : 'Avatar initials ${_initials(displayName)}',
                  child: CircleAvatar(
                    radius: 32,
                    backgroundColor: AppColors.primary.withValues(alpha: 0.14),
                    backgroundImage:
                        hasPhoto ? NetworkImage(photo.trim()) : null,
                    child: !hasPhoto
                        ? Text(
                            _initials(displayName),
                            style: const TextStyle(
                              fontSize: 20,
                              fontWeight: FontWeight.w700,
                              color: AppColors.primary,
                            ),
                          )
                        : null,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        displayName,
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                          color: p.textPrimary,
                          height: 1.25,
                        ),
                      ),
                      if (email != null && email.isNotEmpty) ...[
                        const SizedBox(height: 3),
                        Text(
                          email,
                          style: TextStyle(
                            fontSize: 13,
                            color: p.textSecondary,
                            height: 1.3,
                          ),
                        ),
                      ] else if (profile.phone.trim().isNotEmpty) ...[
                        const SizedBox(height: 3),
                        Text(
                          profile.phone,
                          style: TextStyle(
                            fontSize: 13,
                            color: p.textSecondary,
                            height: 1.3,
                          ),
                        ),
                      ],
                      if (status.isNotEmpty) ...[
                        const SizedBox(height: 8),
                        _StatusPill(status),
                      ],
                    ],
                  ),
                ),
              ],
            ),
            if (platformRole != null) ...[
              const SizedBox(height: 10),
              _MetaRow(
                label: 'Platform role',
                value: platformRole,
                isDark: isDark,
              ),
            ],
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              height: 44,
              child: OutlinedButton.icon(
                onPressed: onEdit,
                icon: const Icon(Icons.edit_outlined, size: 18),
                label: const Text('Edit Profile'),
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColors.primary,
                  side: BorderSide(
                    color: AppColors.primary.withValues(alpha: 0.45),
                  ),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(AppRadii.md),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+'));
    if (parts.length >= 2 && parts[0].isNotEmpty && parts[1].isNotEmpty) {
      return '${parts[0][0]}${parts[1][0]}'.toUpperCase();
    }
    return name.isNotEmpty ? name[0].toUpperCase() : 'U';
  }
}

class _StatusPill extends StatelessWidget {
  const _StatusPill(this.label);
  final String label;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final active = label.toLowerCase().contains('active') ||
        label.toLowerCase().contains('available');
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: active
            ? (isDark
                ? AppColors.success.withValues(alpha: 0.18)
                : _statusPillBg)
            : context.sg.chipBg,
        borderRadius: BorderRadius.circular(AppRadii.pill),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w700,
          color: active
              ? (isDark ? AppColors.emerald : _statusPillFg)
              : context.sg.textSecondary,
        ),
      ),
    );
  }
}

class _MetaRow extends StatelessWidget {
  const _MetaRow({
    required this.label,
    required this.value,
    required this.isDark,
  });

  final String label;
  final String value;
  final bool isDark;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: isDark
            ? AppColors.primary.withValues(alpha: 0.10)
            : AppColors.primary.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(12),
      ),
      child: RichText(
        text: TextSpan(
          style: TextStyle(fontSize: 13, color: context.sg.textPrimary),
          children: [
            TextSpan(
              text: '$label: ',
              style: TextStyle(
                fontWeight: FontWeight.w600,
                color: context.sg.textSecondary,
              ),
            ),
            TextSpan(
              text: value,
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
          ],
        ),
      ),
    );
  }
}

// ── Active organization ────────────────────────────────────────────────

class _ActiveOrganizationCard extends StatelessWidget {
  const _ActiveOrganizationCard({
    required this.session,
    required this.canSwitch,
  });

  final FleetSession session;
  final bool canSwitch;

  @override
  Widget build(BuildContext context) {
    final ctx = session.companyContext!;
    final orgRole = ProfileAccess.organizationRoleLabel(session);
    final companyStatus = ctx.subscription?.status.trim();
    final logo = ctx.logoUrl?.trim();

    return _ProfileCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardHeader(
            title: 'Active Organization',
            icon: Icons.apartment_outlined,
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              CircleAvatar(
                radius: 26,
                backgroundColor: AppColors.primary.withValues(alpha: 0.12),
                backgroundImage:
                    logo != null && logo.isNotEmpty ? NetworkImage(logo) : null,
                child: logo == null || logo.isEmpty
                    ? const Icon(
                        Icons.business_outlined,
                        color: AppColors.primary,
                      )
                    : null,
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      ctx.companyName.isNotEmpty
                          ? ctx.companyName
                          : 'Organization #${ctx.companyId}',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                        color: context.sg.textPrimary,
                      ),
                    ),
                    if (orgRole != null) ...[
                      const SizedBox(height: 4),
                      Text(
                        'Organization role: $orgRole',
                        style: TextStyle(
                          fontSize: 13,
                          color: context.sg.textSecondary,
                          height: 1.3,
                        ),
                      ),
                    ],
                    if (companyStatus != null && companyStatus.isNotEmpty) ...[
                      const SizedBox(height: 8),
                      StatusBadge(
                        companyStatus,
                        color: AppColors.primary,
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),
          // Hidden unless ProfileAccess.canSwitchOrganization is true (mobile API).
          if (canSwitch) ...[
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: OutlinedButton.icon(
                onPressed: null,
                icon: const Icon(Icons.swap_horiz_rounded, size: 18),
                label: const Text('Switch Organization'),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

// ── Account overview ───────────────────────────────────────────────────

class _AccountOverviewCard extends StatelessWidget {
  const _AccountOverviewCard({
    required this.profile,
    required this.session,
  });

  final DriverProfile profile;
  final FleetSession? session;

  @override
  Widget build(BuildContext context) {
    final security = session?.companyContext?.security;
    final rows = <_OverviewRowData>[
      _OverviewRowData(
        icon: Icons.verified_user_outlined,
        label: 'Account status',
        value: profile.statusName,
      ),
      if (profile.lastLoginAt != null)
        _OverviewRowData(
          icon: Icons.login_rounded,
          label: 'Last login',
          value: _formatDateTime(profile.lastLoginAt!),
        ),
      if (security != null && security.mfaRequired)
        const _OverviewRowData(
          icon: Icons.shield_outlined,
          label: 'Security verification',
          value: 'MFA required',
        )
      else if (security != null && security.passwordExpired)
        const _OverviewRowData(
          icon: Icons.shield_outlined,
          label: 'Security verification',
          value: 'Password expired',
        )
      else if (security != null && security.passwordDaysRemaining != null)
        _OverviewRowData(
          icon: Icons.shield_outlined,
          label: 'Security verification',
          value: 'Password expires in ${security.passwordDaysRemaining}d',
        ),
    ];

    return _ProfileCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardHeader(
            title: 'Account Overview',
            icon: Icons.dashboard_outlined,
          ),
          const SizedBox(height: 8),
          for (var i = 0; i < rows.length; i++) ...[
            _OverviewRow(data: rows[i]),
            if (i < rows.length - 1)
              Divider(height: 1, color: context.sg.divider),
          ],
        ],
      ),
    );
  }

  String _formatDateTime(DateTime dt) {
    final local = dt.toLocal();
    final y = local.year.toString().padLeft(4, '0');
    final m = local.month.toString().padLeft(2, '0');
    final d = local.day.toString().padLeft(2, '0');
    final hh = local.hour.toString().padLeft(2, '0');
    final mm = local.minute.toString().padLeft(2, '0');
    return '$y-$m-$d $hh:$mm';
  }
}

class _OverviewRowData {
  const _OverviewRowData({
    required this.icon,
    required this.label,
    required this.value,
  });
  final IconData icon;
  final String label;
  final String value;
}

class _OverviewRow extends StatelessWidget {
  const _OverviewRow({required this.data});
  final _OverviewRowData data;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 10),
      child: Row(
        children: [
          Icon(data.icon, size: 20, color: AppColors.primary),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              data.label,
              style: TextStyle(
                fontSize: 13,
                color: context.sg.textSecondary,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
          Flexible(
            child: Text(
              data.value,
              textAlign: TextAlign.end,
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w700,
                color: context.sg.textPrimary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ── Assigned vehicle ───────────────────────────────────────────────────

class _AssignedVehicleCard extends StatelessWidget {
  const _AssignedVehicleCard({required this.profile});
  final DriverProfile profile;

  @override
  Widget build(BuildContext context) {
    final name = profile.currentVehicleName?.trim();
    final plate = profile.currentVehiclePlate?.trim();
    final title = (name != null && name.isNotEmpty) ? name : 'Assigned vehicle';
    final subtitle = [
      if (plate != null && plate.isNotEmpty) plate,
      if (profile.branchName != null && profile.branchName!.trim().isNotEmpty)
        profile.branchName!.trim(),
    ].join(' · ');

    return _ProfileCard(
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.10),
              borderRadius: BorderRadius.circular(12),
              border: Border.all(
                color: AppColors.primary.withValues(alpha: 0.22),
              ),
            ),
            child: const Icon(
              Icons.directions_bus_outlined,
              color: AppColors.primary,
            ),
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
                    fontWeight: FontWeight.w700,
                    color: context.sg.textPrimary,
                  ),
                ),
                if (subtitle.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: TextStyle(
                      fontSize: 12,
                      color: context.sg.textSecondary,
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// ── Account & preferences ──────────────────────────────────────────────

class _AccountPreferencesCard extends StatelessWidget {
  const _AccountPreferencesCard({
    required this.session,
    required this.onPersonalInfo,
    required this.onAppSettings,
    required this.onSecurity,
    required this.onNotifications,
    required this.onLanguageAppearance,
  });

  final FleetSession? session;
  final VoidCallback onPersonalInfo;
  final VoidCallback onAppSettings;
  final VoidCallback onSecurity;
  final VoidCallback onNotifications;
  final VoidCallback onLanguageAppearance;

  @override
  Widget build(BuildContext context) {
    return _ProfileCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardHeader(
            title: 'Account & Preferences',
            icon: Icons.tune_outlined,
          ),
          const SizedBox(height: 4),
          if (ProfileAccess.canSeePersonalInformation(session))
            _NavRow(
              icon: Icons.badge_outlined,
              title: 'Personal Information',
              subtitle: 'Name, contact and role details',
              onTap: onPersonalInfo,
            ),
          if (ProfileAccess.canSeeAppSettings(session))
            _NavRow(
              icon: Icons.settings_outlined,
              title: 'App Settings',
              subtitle: 'Tracking, sync and preferences',
              onTap: onAppSettings,
            ),
          if (ProfileAccess.canSeeSecurityPrivacy(session))
            _NavRow(
              icon: Icons.shield_outlined,
              title: 'Security & Privacy',
              subtitle: 'Roles, permissions and device security',
              onTap: onSecurity,
            ),
          if (ProfileAccess.canSeeNotifications(session))
            _NavRow(
              icon: Icons.notifications_outlined,
              title: 'Notifications',
              subtitle: 'Alerts and notification center',
              onTap: onNotifications,
            ),
          if (ProfileAccess.canSeeLanguageAppearance(session))
            _NavRow(
              icon: Icons.palette_outlined,
              title: 'Language and Appearance',
              subtitle: 'Theme and language',
              onTap: onLanguageAppearance,
              dense: true,
            ),
        ],
      ),
    );
  }
}

// ── Driver operations (permission gated) ───────────────────────────────

class _DriverOperationsCard extends StatelessWidget {
  const _DriverOperationsCard({required this.session});
  final FleetSession? session;

  @override
  Widget build(BuildContext context) {
    final items = <Widget>[];

    if (SettingsAccess.canSeeAttendance(session)) {
      items.add(
        _NavRow(
          icon: Icons.fingerprint,
          title: 'Attendance',
          subtitle: 'Check-in and history',
          onTap: () => context.push('/attendance'),
        ),
      );
    }
    if (ProfileAccess.canSeeFuelHistory(session)) {
      items.add(
        _NavRow(
          icon: Icons.local_gas_station_outlined,
          title: 'Fuel History',
          onTap: () => context.push('/fuel'),
        ),
      );
    }
    if (ProfileAccess.canSeeEarnings(session)) {
      items.add(
        _NavRow(
          icon: Icons.payments_outlined,
          title: 'Earnings',
          onTap: () => context.push('/earnings'),
        ),
      );
    }
    if (ProfileAccess.canSeeDocuments(session)) {
      items.add(
        _NavRow(
          icon: Icons.folder_outlined,
          title: 'Documents',
          onTap: () => context.push('/documents'),
        ),
      );
    }
    if (ProfileAccess.canSeeInspection(session)) {
      items.add(
        _NavRow(
          icon: Icons.fact_check_outlined,
          title: 'Inspection',
          onTap: () => context.push('/inspection'),
        ),
      );
    }
    if (SettingsAccess.canSeeActivityTimeline(session)) {
      items.add(
        _NavRow(
          icon: Icons.history_outlined,
          title: 'Timeline',
          onTap: () => context.push('/timeline'),
          dense: true,
        ),
      );
    }

    if (items.isEmpty) return const SizedBox.shrink();

    return _ProfileCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardHeader(
            title: 'Operations',
            icon: Icons.work_outline_rounded,
          ),
          const SizedBox(height: 4),
          ...items,
        ],
      ),
    );
  }
}

// ── Support & about ────────────────────────────────────────────────────

class _SupportAboutCard extends StatelessWidget {
  const _SupportAboutCard({
    required this.onHelp,
    required this.onPrivacy,
    required this.onTerms,
    required this.onCheckUpdates,
    required this.showEnvironment,
  });

  final VoidCallback onHelp;
  final VoidCallback onPrivacy;
  final VoidCallback onTerms;
  final VoidCallback onCheckUpdates;
  final bool showEnvironment;

  @override
  Widget build(BuildContext context) {
    return _ProfileCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _CardHeader(
            title: 'Support & About',
            icon: Icons.headset_mic_outlined,
          ),
          const SizedBox(height: 4),
          _NavRow(
            icon: Icons.headset_mic_outlined,
            title: 'Help & Support',
            subtitle: 'Contact support or report an issue',
            onTap: onHelp,
          ),
          _NavRow(
            icon: Icons.privacy_tip_outlined,
            title: 'Privacy',
            subtitle: 'How we collect and use your data',
            onTap: onPrivacy,
          ),
          _NavRow(
            icon: Icons.description_outlined,
            title: 'Terms',
            subtitle: 'App usage terms',
            onTap: onTerms,
          ),
          FutureBuilder<String>(
            future: AppVersionService.displayVersion(),
            builder: (context, snap) {
              final version = snap.data ?? AppVersionService.currentVersion;
              return _NavRow(
                icon: Icons.info_outline_rounded,
                title: 'App Version',
                trailing: Text(
                  version,
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
              style: TextStyle(fontSize: 11, color: context.sg.textMuted),
            ),
          ],
        ],
      ),
    );
  }
}

// ── Shared chrome ──────────────────────────────────────────────────────

class _ProfileCard extends StatelessWidget {
  const _ProfileCard({required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 8),
      decoration: BoxDecoration(
        color: context.sg.card,
        borderRadius: BorderRadius.circular(AppRadii.lg),
        boxShadow: isDark ? null : AppShadows.card,
        border: isDark
            ? Border.all(color: context.sg.border.withValues(alpha: 0.55))
            : null,
      ),
      child: child,
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

class _NavRow extends StatelessWidget {
  const _NavRow({
    required this.icon,
    required this.title,
    required this.onTap,
    this.subtitle,
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

class _SignOutButton extends StatelessWidget {
  const _SignOutButton({required this.onPressed});
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      height: 52,
      child: OutlinedButton.icon(
        onPressed: onPressed,
        icon: const Icon(Icons.logout_rounded, color: AppColors.error, size: 20),
        label: const Text(
          'Sign Out',
          style: TextStyle(
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

class _InfoLine extends StatelessWidget {
  const _InfoLine(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 120,
            child: Text(
              label,
              style: TextStyle(color: context.sg.textSecondary, fontSize: 13),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: TextStyle(
                fontWeight: FontWeight.w600,
                color: context.sg.textPrimary,
                fontSize: 13,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
