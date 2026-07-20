import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/notifications/presentation/notifications_notifier.dart';
import '../../../features/offline/presentation/offline_status_banner.dart';
import '../../../core/offline/offline_sync_service.dart';

class AppShell extends ConsumerWidget {
  const AppShell({super.key, required this.child});
  final Widget child;

  static const _tabs = [
    _Tab(icon: Icons.home_outlined, activeIcon: Icons.home_rounded, label: 'Home', path: '/dashboard'),
    _Tab(icon: Icons.route_outlined, activeIcon: Icons.route_rounded, label: 'Trips', path: '/trips'),
    _Tab(icon: Icons.my_location_outlined, activeIcon: Icons.my_location, label: 'Live', path: '/live'),
    _Tab(icon: Icons.notifications_outlined, activeIcon: Icons.notifications_rounded, label: 'Alerts', path: '/notifications'),
    _Tab(icon: Icons.person_outline_rounded, activeIcon: Icons.person_rounded, label: 'Profile', path: '/profile'),
  ];

  int _currentIndex(BuildContext context) {
    final loc = GoRouterState.of(context).uri.path;
    for (var i = 0; i < _tabs.length; i++) {
      if (loc.startsWith(_tabs[i].path)) return i;
    }
    return 0;
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final idx = _currentIndex(context);
    final unread = ref.watch(unreadNotificationsCountProvider).valueOrNull ?? 0;
    ref.watch(offlineSyncProvider);
    ref.watch(offlinePendingCountProvider);

    return Scaffold(
      body: Column(
        children: [
          const OfflineStatusBanner(),
          Expanded(child: child),
        ],
      ),
      bottomNavigationBar: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.06),
              blurRadius: 16,
              offset: const Offset(0, -4),
            ),
          ],
        ),
        child: SafeArea(
          child: BottomNavigationBar(
            currentIndex: idx,
            onTap: (i) => context.go(_tabs[i].path),
            items: [
              for (var i = 0; i < _tabs.length; i++)
                BottomNavigationBarItem(
                  icon: i == 3 && unread > 0
                      ? Badge(
                          backgroundColor: AppColors.error,
                          label: Text(unread > 99 ? '99+' : '$unread'),
                          child: Icon(_tabs[i].icon),
                        )
                      : Icon(_tabs[i].icon),
                  activeIcon: i == 3 && unread > 0
                      ? Badge(
                          backgroundColor: AppColors.error,
                          label: Text(unread > 99 ? '99+' : '$unread'),
                          child: Icon(_tabs[i].activeIcon),
                        )
                      : Icon(_tabs[i].activeIcon),
                  label: _tabs[i].label,
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Tab {
  const _Tab({
    required this.icon,
    required this.activeIcon,
    required this.label,
    required this.path,
  });
  final IconData icon;
  final IconData activeIcon;
  final String label;
  final String path;
}
