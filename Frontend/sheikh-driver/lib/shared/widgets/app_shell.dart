import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/navigation/fleet_nav_config.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/notifications/presentation/notifications_notifier.dart';
import '../../../features/offline/presentation/offline_status_banner.dart';
import '../../../core/offline/offline_sync_service.dart';

class AppShell extends ConsumerWidget {
  const AppShell({super.key, required this.child});
  final Widget child;

  int _currentIndex(List<FleetNavTab> tabs, BuildContext context) {
    final loc = GoRouterState.of(context).uri.path;
    if (tabs.isEmpty) return 0;
    return FleetNavConfig.indexForLocation(tabs, loc).clamp(0, tabs.length - 1);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(fleetSessionProvider);
    final tabs = FleetNavConfig.visibleTabs(session);
    final idx = tabs.isEmpty
        ? 0
        : _currentIndex(tabs, context).clamp(0, tabs.length - 1);

    // Keep sync services alive without rebuilding the whole shell on every tick.
    ref.listen(offlineSyncProvider, (_, __) {});
    ref.listen(offlinePendingCountProvider, (_, __) {});

    return Scaffold(
      body: Column(
        children: [
          const OfflineStatusBanner(),
          Expanded(child: child),
        ],
      ),
      floatingActionButton: session != null && session.canSeeAiTab
          ? FloatingActionButton.extended(
              onPressed: () => context.push('/ai'),
              icon: const Icon(Icons.auto_awesome_rounded),
              label: const Text('AI'),
              backgroundColor: AppColors.primary,
              foregroundColor: Colors.white,
            )
          : null,
      bottomNavigationBar: tabs.isEmpty
          ? null
          : _ShellBottomBar(tabs: tabs, currentIndex: idx),
    );
  }
}

/// Isolated so unread badge updates rebuild only the tab bar, not the shell body.
class _ShellBottomBar extends ConsumerWidget {
  const _ShellBottomBar({
    required this.tabs,
    required this.currentIndex,
  });

  final List<FleetNavTab> tabs;
  final int currentIndex;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread =
        ref.watch(unreadNotificationsCountProvider).valueOrNull ?? 0;

    return Container(
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
          currentIndex: currentIndex,
          type: BottomNavigationBarType.fixed,
          onTap: (i) => context.go(tabs[i].path),
          items: [
            for (final tab in tabs)
              BottomNavigationBarItem(
                icon: _TabIcon(tab: tab, unread: unread, active: false),
                activeIcon: _TabIcon(tab: tab, unread: unread, active: true),
                label: tab.label,
              ),
          ],
        ),
      ),
    );
  }
}

class _TabIcon extends StatelessWidget {
  const _TabIcon({
    required this.tab,
    required this.unread,
    required this.active,
  });

  final FleetNavTab tab;
  final int unread;
  final bool active;

  @override
  Widget build(BuildContext context) {
    final icon = Icon(active ? tab.activeIcon : tab.icon);
    final showBadge =
        unread > 0 && (tab.id == 'more' || tab.id == 'notifications');

    // Stack + Opacity (not Material Badge / conditional Positioned) — toggling
    // Badge widgets mid-frame dirties ParentData and loops
    // semantics.parentDataDirty assertions.
    return SizedBox(
      width: 28,
      height: 28,
      child: Stack(
        clipBehavior: Clip.none,
        alignment: Alignment.center,
        children: [
          icon,
          Positioned(
            right: -8,
            top: -4,
            child: IgnorePointer(
              child: Opacity(
                opacity: showBadge ? 1 : 0,
                child: Container(
                  constraints: const BoxConstraints(minWidth: 16),
                  padding:
                      const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
                  decoration: BoxDecoration(
                    color: AppColors.error,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    unread > 99 ? '99+' : '$unread',
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                      height: 1.2,
                    ),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
