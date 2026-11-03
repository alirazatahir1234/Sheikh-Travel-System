import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/navigation/fleet_nav_config.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/notifications/presentation/notifications_notifier.dart';
import '../../../features/ai/presentation/widgets/ai_assistant_fab.dart';
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

    final loc = GoRouterState.of(context).uri.path;
    // Immersive GPS trip history / playback — hide shell tab bar.
    final hideBottomNav = RegExp(r'^/fleet/vehicles/\d+/history').hasMatch(loc);
    final showAiFab =
        (session?.canSeeAiTab ?? false) &&
            !loc.startsWith('/ai') &&
            !hideBottomNav;

    return Scaffold(
      body: Column(
        children: [
          const OfflineStatusBanner(),
          Expanded(child: child),
        ],
      ),
      floatingActionButton: showAiFab ? const AiAssistantFab() : null,
      floatingActionButtonLocation: FloatingActionButtonLocation.endFloat,
      bottomNavigationBar: tabs.isEmpty || hideBottomNav
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
    final unread = ref.watch(unreadNotificationsCountProvider).valueOrNull ?? 0;

    final p = context.sg;
    return Container(
      decoration: BoxDecoration(
        color: p.navBar,
        boxShadow: AppShadows.nav,
        border: Border(top: BorderSide(color: p.border.withValues(alpha: 0.7))),
      ),
      child: SafeArea(
        child: BottomNavigationBar(
          currentIndex: currentIndex,
          type: BottomNavigationBarType.fixed,
          backgroundColor: p.navBar,
          onTap: (i) {
            final target = tabs[i].path;
            final loc = GoRouterState.of(context).uri.path;
            // Re-tapping the active tab pops to its root (e.g. driver detail →
            // drivers list). Never use a bare `go` that can bounce nested
            // `/more/drivers/:id` routes onto the More hub.
            if (loc == target || loc.startsWith('$target/')) {
              if (loc != target) context.go(target);
              return;
            }
            context.go(target);
          },
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
    final showBadge =
        unread > 0 && (tab.id == 'more' || tab.id == 'notifications');
    final icon = Icon(
      active ? tab.activeIcon : tab.icon,
      size: 22,
      color: active ? Colors.white : null,
    );

    // Stack + Opacity (not Material Badge / conditional Positioned) — toggling
    // Badge widgets mid-frame dirties ParentData and loops
    // semantics.parentDataDirty assertions.
    final isOverflowCount = unread > 99;
    return SizedBox(
      width: 40,
      height: 32,
      child: Stack(
        clipBehavior: Clip.none,
        alignment: Alignment.center,
        children: [
          if (active)
            Container(
              width: 36,
              height: 28,
              decoration: BoxDecoration(
                color: AppColors.primary,
                borderRadius: BorderRadius.circular(10),
              ),
            ),
          icon,
          Positioned(
            // Sit outside the glyph so "99+" never covers the More icon.
            right: -6,
            top: -6,
            child: IgnorePointer(
              child: Opacity(
                opacity: showBadge ? 1 : 0,
                child: Container(
                  constraints: BoxConstraints(
                    minWidth: isOverflowCount ? 22 : 16,
                  ),
                  padding: EdgeInsets.symmetric(
                    horizontal: isOverflowCount ? 3 : 4,
                    vertical: 1,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.error,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    isOverflowCount ? '99+' : '$unread',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: isOverflowCount ? 9 : 10,
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
