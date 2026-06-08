import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/auth_api_client.dart';
import '../../core/constants/app_theme.dart';
import '../../core/navigation/menu_config.dart';
import '../../core/navigation/navigation_provider.dart';
import '../../core/services/session_storage.dart';
import '../auth/login_screen.dart';
import 'module_content_screen.dart';
import 'widgets/app_sidebar.dart';

class AppShell extends ConsumerWidget {
  const AppShell({super.key});

  static const _sidebarWidth = 280.0;
  static const _breakpoint = 900.0;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final routeId = ref.watch(navigationProvider).selectedRouteId;
    final pageTitle = MenuConfig.labelFor(routeId);
    final width = MediaQuery.sizeOf(context).width;
    final useDrawer = width < _breakpoint;

    return Scaffold(
      backgroundColor: AppColors.contentBg,
      appBar: useDrawer
          ? AppBar(
              backgroundColor: Colors.white,
              foregroundColor: AppColors.text,
              elevation: 0,
              scrolledUnderElevation: 0.5,
              title: Text(
                pageTitle,
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  fontSize: 18,
                ),
              ),
              actions: [
                IconButton(
                  tooltip: 'Sign out',
                  onPressed: () => _logout(context, ref),
                  icon: const Icon(Icons.logout),
                ),
              ],
            )
          : null,
      drawer: useDrawer
          ? Drawer(
              width: _sidebarWidth,
              backgroundColor: AppColors.sidebarBg,
              child: AppSidebar(
                onItemSelected: () => Navigator.of(context).pop(),
              ),
            )
          : null,
      body: useDrawer
          ? const ModuleContentScreen()
          : Row(
              children: [
                SizedBox(
                  width: _sidebarWidth,
                  child: const AppSidebar(),
                ),
                Expanded(
                  child: Column(
                    children: [
                      _TopBar(
                        title: pageTitle,
                        onLogout: () => _logout(context, ref),
                      ),
                      const Expanded(child: ModuleContentScreen()),
                    ],
                  ),
                ),
              ],
            ),
    );
  }

  Future<void> _logout(BuildContext context, WidgetRef ref) async {
    final session = ref.read(sessionProvider).valueOrNull;
    if (session != null) {
      await ref.read(authApiProvider).logout(session.refreshToken);
    }
    await ref.read(sessionProvider.notifier).setSession(null);
    if (!context.mounted) return;
    Navigator.of(context).pushReplacement(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({
    required this.title,
    required this.onLogout,
  });

  final String title;
  final VoidCallback onLogout;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 56,
      padding: const EdgeInsets.symmetric(horizontal: 20),
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(bottom: BorderSide(color: AppColors.border)),
      ),
      child: Row(
        children: [
          Text(
            title,
            style: const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w600,
              color: AppColors.text,
            ),
          ),
          const Spacer(),
          IconButton(
            tooltip: 'Sign out',
            onPressed: onLogout,
            icon: const Icon(Icons.logout, color: AppColors.textMuted),
          ),
        ],
      ),
    );
  }
}
