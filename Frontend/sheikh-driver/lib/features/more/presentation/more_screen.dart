import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/navigation/fleet_nav_config.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../auth/data/auth_repository.dart';

class MoreScreen extends ConsumerWidget {
  const MoreScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(fleetSessionProvider);
    if (session == null) {
      return const Scaffold(body: Center(child: SgBrandLoader()));
    }

    final entries = FleetNavConfig.visibleMoreEntries(session);
    final operatorShell = session.isGpsOperator;

    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(title: const Text('More')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        children: [
          Text(
            'Hello, ${session.displayName.split(' ').first}',
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                  color: context.sg.textPrimary,
                  fontWeight: FontWeight.w800,
                ),
          ),
          const SizedBox(height: 4),
          Text(
            [
              if (session.companyName != null &&
                  session.companyName!.isNotEmpty)
                session.companyName!,
              if (session.companyContext?.branchName != null)
                session.companyContext!.branchName!,
              if (operatorShell)
                'GPS operations'
              else if (session.isOwnerTracking)
                'Owner tracking'
              else if (session.isDriverOnly)
                'Driver'
              else
                'Fleet operations',
            ].join(' · '),
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: context.sg.textSecondary,
                ),
          ),
          const SizedBox(height: 20),
          GridView.builder(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 3,
              mainAxisSpacing: 12,
              crossAxisSpacing: 12,
              childAspectRatio: 0.92,
            ),
            itemCount: entries.length,
            itemBuilder: (context, index) {
              final entry = entries[index];
              return _MoreTile(
                entry: entry,
                onTap: () {
                  if (entry.comingSoonSprint != null) {
                    _showComingSoon(context, entry);
                    return;
                  }
                  if (FleetNavConfig.isShellRoute(entry.route)) {
                    context.go(entry.route);
                  } else {
                    context.push(entry.route);
                  }
                },
              );
            },
          ),
        ],
      ),
    );
  }

  void _showComingSoon(BuildContext context, MoreMenuEntry entry) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (ctx) => Padding(
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(entry.icon, size: 40, color: AppColors.primary),
            const SizedBox(height: 12),
            Text(
              entry.label,
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w700,
                color: ctx.sg.textPrimary,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'This feature is coming soon.',
              style: TextStyle(color: ctx.sg.textSecondary),
            ),
          ],
        ),
      ),
    );
  }
}

class _MoreTile extends StatelessWidget {
  const _MoreTile({required this.entry, required this.onTap});

  final MoreMenuEntry entry;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Material(
      color: p.card,
      borderRadius: BorderRadius.circular(AppRadii.lg),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadii.lg),
        child: Ink(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.lg),
            border: Border.all(color: p.border.withValues(alpha: 0.7)),
          ),
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Container(
                  width: 44,
                  height: 44,
                  decoration: BoxDecoration(
                    color: AppColors.primary.withValues(alpha: 0.10),
                    borderRadius: BorderRadius.circular(AppRadii.md),
                  ),
                  child: Icon(entry.icon, color: AppColors.primary, size: 24),
                ),
                const SizedBox(height: 8),
                Text(
                  entry.label,
                  textAlign: TextAlign.center,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: p.textPrimary,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class ComingSoonScreen extends StatelessWidget {
  const ComingSoonScreen({
    super.key,
    required this.title,
    this.sprintLabel,
    this.icon = Icons.construction_outlined,
  });

  final String title;
  final String? sprintLabel;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(title: Text(title)),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: SgCard(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(icon, size: 48, color: AppColors.primary),
                const SizedBox(height: 12),
                Text(
                  title,
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 8),
                Text(
                  'This feature is coming soon.',
                  style: TextStyle(color: context.sg.textSecondary),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
