import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/constants/app_theme.dart';
import '../../core/navigation/menu_config.dart';
import '../../core/navigation/nav_models.dart';
import '../../core/navigation/navigation_provider.dart';
import '../../core/services/session_storage.dart';

class ModuleContentScreen extends ConsumerWidget {
  const ModuleContentScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final routeId = ref.watch(navigationProvider).selectedRouteId;
    final label = MenuConfig.labelFor(routeId);
    final session = ref.watch(sessionProvider).valueOrNull;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: AppColors.text,
                ),
          ),
          const SizedBox(height: 8),
          Text(
            'Module screen placeholder — connect to API when ready.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: AppColors.textMuted,
                ),
          ),
          const SizedBox(height: 24),
          _ModuleCard(
            title: 'Signed in as',
            value: session?.fullName ?? 'Guest',
          ),
          if (session?.email != null)
            _ModuleCard(title: 'Email', value: session!.email!),
          if (session?.roles.isNotEmpty ?? false)
            _ModuleCard(title: 'Role', value: session!.roles.join(', ')),
          _ModuleCard(title: 'Route ID', value: routeId.name),
        ],
      ),
    );
  }
}

class _ModuleCard extends StatelessWidget {
  const _ModuleCard({required this.title, required this.value});

  final String title;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              color: AppColors.textMuted,
              fontSize: 12,
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            value,
            style: const TextStyle(
              color: AppColors.text,
              fontSize: 16,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

NavRouteId defaultRouteForMenu(ResolvedMenu menu) =>
    menu.isDriverLayout ? NavRouteId.myTrips : NavRouteId.dashboard;
