import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../domain/driver_profile_model.dart';
import 'profile_notifier.dart';

class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

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

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final profileAsync = ref.watch(profileProvider);
    final session = ref.watch(fleetSessionProvider);

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Profile'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout_rounded, color: AppColors.error),
            tooltip: 'Sign out',
            onPressed: () => _signOut(context, ref),
          ),
        ],
      ),
      body: profileAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.person_off_outlined,
                  size: 48, color: AppColors.textSecondary),
              const SizedBox(height: 12),
              Text(e.toString(), textAlign: TextAlign.center),
              const SizedBox(height: 16),
              SgPrimaryButton(
                label: 'Retry',
                onPressed: () => ref.read(profileProvider.notifier).refresh(),
              ),
            ],
          ),
        ),
        data: (profile) => RefreshIndicator(
          color: AppColors.primary,
          onRefresh: () => ref.read(profileProvider.notifier).refresh(),
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
            children: [
              _ProfileHeader(profile: profile, company: session?.companyContext),
              if (session?.companyContext != null) ...[
                const SizedBox(height: 12),
                _CompanyContextCard(contextData: session!.companyContext!),
              ],
              const SizedBox(height: 20),
              SgCard(
                padding: EdgeInsets.zero,
                child: Column(
                  children: [
                    _MenuTile(
                      icon: Icons.badge_outlined,
                      label: 'Profile Info',
                      onTap: () => _showInfoSheet(
                        context,
                        profile,
                        session?.companyContext,
                      ),
                    ),
                    if (!profile.isStaffProfile) ...[
                      const Divider(height: 1),
                      _MenuTile(
                        icon: Icons.fingerprint,
                        label: 'Attendance',
                        onTap: () => context.push('/attendance'),
                      ),
                      const Divider(height: 1),
                      _MenuTile(
                        icon: Icons.local_gas_station_outlined,
                        label: 'Fuel History',
                        onTap: () => context.push('/fuel'),
                      ),
                      const Divider(height: 1),
                      _MenuTile(
                        icon: Icons.payments_outlined,
                        label: 'Earnings',
                        onTap: () => context.push('/earnings'),
                      ),
                      const Divider(height: 1),
                      _MenuTile(
                        icon: Icons.folder_outlined,
                        label: 'Documents',
                        onTap: () => context.push('/documents'),
                      ),
                      const Divider(height: 1),
                      _MenuTile(
                        icon: Icons.fact_check_outlined,
                        label: 'Inspection',
                        onTap: () => context.push('/inspection'),
                      ),
                      const Divider(height: 1),
                      _MenuTile(
                        icon: Icons.history_outlined,
                        label: 'Timeline',
                        onTap: () => context.push('/timeline'),
                      ),
                    ],
                    const Divider(height: 1),
                    _MenuTile(
                      icon: Icons.settings_outlined,
                      label: 'Settings',
                      onTap: () => context.push('/settings'),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),
              SgDangerOutlineButton(
                label: 'Sign Out',
                onPressed: () => _signOut(context, ref),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showInfoSheet(
    BuildContext context,
    DriverProfile profile,
    CompanyContext? company,
  ) {
    showModalBottomSheet(
      context: context,
      showDragHandle: true,
      builder: (_) => Padding(
        padding: const EdgeInsets.fromLTRB(20, 0, 20, 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Profile Info',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 16),
            if (company != null && company.companyName.isNotEmpty)
              _InfoLine('Company', company.companyName),
            if (company?.branchName != null)
              _InfoLine('Branch', company!.branchName!),
            if (company?.departmentName != null)
              _InfoLine('Department', company!.departmentName!),
            if (profile.phone.isNotEmpty) _InfoLine('Phone', profile.phone),
            if (profile.email != null) _InfoLine('Email', profile.email!),
            if (profile.isStaffProfile) ...[
              if (profile.roleLabel != null)
                _InfoLine('Role', profile.roleLabel!),
            ] else ...[
              _InfoLine('License', profile.licenseNumber),
              if (profile.currentVehicleName != null)
                _InfoLine('Vehicle', profile.currentVehicleName!),
              if (profile.branchName != null)
                _InfoLine('Branch', profile.branchName!),
            ],
          ],
        ),
      ),
    );
  }
}

class _CompanyContextCard extends StatelessWidget {
  const _CompanyContextCard({required this.contextData});
  final CompanyContext contextData;

  @override
  Widget build(BuildContext context) {
    final parts = <String>[
      if (contextData.branchName != null) contextData.branchName!,
      if (contextData.departmentName != null) contextData.departmentName!,
    ];
    final moduleLabels = contextData.moduleDisplayLabels;
    return SgCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              CircleAvatar(
                radius: 22,
                backgroundColor: AppColors.primary.withValues(alpha: 0.12),
                backgroundImage: contextData.logoUrl != null
                    ? NetworkImage(contextData.logoUrl!)
                    : null,
                child: contextData.logoUrl == null
                    ? const Icon(Icons.business_outlined,
                        color: AppColors.primary)
                    : null,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      contextData.companyName,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    if (parts.isNotEmpty) ...[
                      const SizedBox(height: 2),
                      Text(
                        parts.join(' · '),
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),
          if (moduleLabels.isNotEmpty) ...[
            const SizedBox(height: 12),
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                for (final label in moduleLabels.take(8))
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(AppRadii.sm),
                    ),
                    child: Text(
                      label,
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: AppColors.primary,
                      ),
                    ),
                  ),
              ],
            ),
          ],
          if (contextData.featureDisplayLabels.isNotEmpty) ...[
            const SizedBox(height: 12),
            const Text(
              'Enabled Features',
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w700,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: 6),
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                for (final label in contextData.featureDisplayLabels.take(8))
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.chipBg,
                      borderRadius: BorderRadius.circular(AppRadii.sm),
                      border: Border.all(color: AppColors.border),
                    ),
                    child: Text(
                      label,
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ),
              ],
            ),
          ],
          if (contextData.subscription != null) ...[
            const SizedBox(height: 14),
            const Divider(height: 1),
            const SizedBox(height: 12),
            _SubscriptionSummary(subscription: contextData.subscription!),
          ],
        ],
      ),
    );
  }
}

class _SubscriptionSummary extends StatelessWidget {
  const _SubscriptionSummary({required this.subscription});
  final CompanySubscription subscription;

  @override
  Widget build(BuildContext context) {
    final df = DateFormat('dd MMM yyyy');
    final expiry = subscription.endDate != null
        ? df.format(subscription.endDate!.toLocal())
        : '—';
    final limitBits = <String>[
      if (subscription.maxUsers != null)
        'Users ${subscription.usedUsers}/${subscription.maxUsers}',
      if (subscription.maxDrivers != null)
        'Drivers ${subscription.usedDrivers}/${subscription.maxDrivers}',
      if (subscription.maxVehicles != null)
        'Vehicles ${subscription.usedVehicles}/${subscription.maxVehicles}',
      if (subscription.storageQuotaGb != null)
        'Storage ${subscription.storageQuotaGb} GB',
      if (subscription.aiCredits != null)
        'AI ${subscription.aiCredits} credits',
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                subscription.displayPlanName,
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
            ),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
              decoration: BoxDecoration(
                color: AppColors.success.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(AppRadii.sm),
              ),
              child: Text(
                subscription.status,
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                  color: AppColors.success,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 4),
        Text(
          'Expires $expiry',
          style: const TextStyle(
            fontSize: 12,
            color: AppColors.textSecondary,
          ),
        ),
        if (limitBits.isNotEmpty) ...[
          const SizedBox(height: 4),
          Text(
            limitBits.join(' · '),
            style: const TextStyle(
              fontSize: 11,
              color: AppColors.textSecondary,
            ),
          ),
        ],
        if (subscription.licensedModuleCodes.isNotEmpty) ...[
          const SizedBox(height: 8),
          Wrap(
            spacing: 6,
            runSpacing: 6,
            children: [
              for (final code in subscription.licensedModuleCodes.take(8))
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: AppColors.chipBg,
                    borderRadius: BorderRadius.circular(AppRadii.sm),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Text(
                    code,
                    style: const TextStyle(
                      fontSize: 10,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary,
                      fontFamily: 'monospace',
                    ),
                  ),
                ),
            ],
          ),
        ],
      ],
    );
  }
}

class _ProfileHeader extends StatelessWidget {
  const _ProfileHeader({required this.profile, this.company});
  final DriverProfile profile;
  final CompanyContext? company;

  @override
  Widget build(BuildContext context) {
    return SgCard(
      child: Row(
        children: [
          CircleAvatar(
            radius: 36,
            backgroundColor: AppColors.primary.withValues(alpha: 0.12),
            backgroundImage: profile.photoUrl != null
                ? NetworkImage(profile.photoUrl!)
                : null,
            child: profile.photoUrl == null
                ? Text(
                    _initials(profile.fullName),
                    style: const TextStyle(
                      fontSize: 24,
                      fontWeight: FontWeight.w700,
                      color: AppColors.primary,
                    ),
                  )
                : null,
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  profile.fullName,
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  profile.isStaffProfile
                      ? (profile.roleLabel ?? 'Staff')
                      : 'Driver ID: ${profile.driverCode}',
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 13,
                  ),
                ),
                if (company?.companyName != null &&
                    company!.companyName.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(
                    company!.companyName,
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: AppColors.primary,
                    ),
                  ),
                ],
                const SizedBox(height: 8),
                StatusBadge(profile.statusName),
              ],
            ),
          ),
        ],
      ),
    );
  }

  String _initials(String name) {
    final parts = name.trim().split(' ');
    if (parts.length >= 2) {
      return '${parts[0][0]}${parts[1][0]}'.toUpperCase();
    }
    return name.isNotEmpty ? name[0].toUpperCase() : 'D';
  }
}

class _MenuTile extends StatelessWidget {
  const _MenuTile({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      onTap: onTap,
      leading: Container(
        width: 36,
        height: 36,
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(AppRadii.sm),
        ),
        child: Icon(icon, color: AppColors.primary, size: 20),
      ),
      title: Text(
        label,
        style: const TextStyle(
          fontWeight: FontWeight.w600,
          color: AppColors.textPrimary,
        ),
      ),
      trailing: const Icon(
        Icons.chevron_right_rounded,
        color: AppColors.textMuted,
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
        children: [
          SizedBox(
            width: 90,
            child: Text(
              label,
              style: const TextStyle(color: AppColors.textSecondary),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}
