import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../domain/settings_access.dart';

/// Role-leveled Security & Permissions summary (read-only from session).
class SecurityPermissionsScreen extends ConsumerWidget {
  const SecurityPermissionsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final p = context.sg;
    final session = ref.watch(fleetSessionProvider);
    if (session == null) {
      return Scaffold(
        appBar: AppBar(title: Text(l10n.securityPermissions)),
        body: Center(child: Text(l10n.notAvailable)),
      );
    }

    final roles = FleetRole.orderByPrivilege(session.roles);
    final labels =
        session.companyContext?.permissionCategoryLabels ?? const <String>[];
    final showDiagnostics = SettingsAccess.canSeeSecurityDiagnostics(session);

    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(title: Text(l10n.securityPermissions)),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 32),
        children: [
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: p.card,
              borderRadius: BorderRadius.circular(AppRadii.md),
              border: Border.all(color: p.border.withValues(alpha: 0.7)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  session.displayName,
                  style: TextStyle(
                    fontSize: 17,
                    fontWeight: FontWeight.w700,
                    color: p.textPrimary,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  session.companyName ?? 'Company #${session.companyId}',
                  style: TextStyle(color: p.textSecondary, fontSize: 13),
                ),
                const SizedBox(height: 8),
                Text(
                  l10n.securityLevelHint(_levelLabel(session)),
                  style: TextStyle(fontSize: 12, color: p.textMuted),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          Text(
            l10n.rolesSection.toUpperCase(),
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w700,
              letterSpacing: 1.1,
              color: p.textSecondary,
            ),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final role in roles)
                Chip(
                  label: Text(role),
                  backgroundColor: AppColors.accent.withValues(alpha: 0.12),
                  side: BorderSide.none,
                  labelStyle: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: AppColors.accent,
                  ),
                ),
            ],
          ),
          if (labels.isNotEmpty) ...[
            const SizedBox(height: 20),
            Text(
              l10n.permissionCategories.toUpperCase(),
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w700,
                letterSpacing: 1.1,
                color: p.textSecondary,
              ),
            ),
            const SizedBox(height: 8),
            ...labels.map(
              (label) => ListTile(
                dense: true,
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.check_circle_outline,
                    color: AppColors.emerald, size: 20),
                title: Text(label, style: TextStyle(color: p.textPrimary)),
              ),
            ),
          ],
          const SizedBox(height: 12),
          Text(
            l10n.securityPermissionsFootnote,
            style: TextStyle(fontSize: 12, height: 1.35, color: p.textMuted),
          ),
          if (showDiagnostics) ...[
            const SizedBox(height: 24),
            ListTile(
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(AppRadii.md),
                side: BorderSide(color: p.border),
              ),
              tileColor: p.card,
              leading: const Icon(Icons.security, color: AppColors.navy),
              title: Text(l10n.securityDiagnostics),
              subtitle: Text(l10n.securityDiagnosticsSubtitle),
              trailing: const Icon(Icons.chevron_right),
              onTap: () => context.push('/security'),
            ),
          ],
        ],
      ),
    );
  }

  String _levelLabel(FleetSession session) {
    if (session.hasRole(FleetRole.superAdmin)) return 'Platform';
    if (session.hasRole(FleetRole.tenantAdmin) ||
        session.hasRole(FleetRole.owner)) {
      return 'Company';
    }
    if (session.hasAnyRole(const [
      FleetRole.fleetManager,
      FleetRole.driverManager,
      FleetRole.dispatcher,
      FleetRole.gpsOperator,
    ])) {
      return 'Operations';
    }
    if (session.isDriverSession) return 'Driver';
    return 'Standard';
  }
}
