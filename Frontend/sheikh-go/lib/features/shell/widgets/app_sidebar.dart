import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/constants/app_theme.dart';
import '../../../core/models/auth_session.dart';
import '../../../core/navigation/nav_models.dart';
import '../../../core/navigation/navigation_provider.dart';
import '../../../core/navigation/tenant_type.dart';
import '../../../core/services/session_storage.dart';

class AppSidebar extends ConsumerWidget {
  const AppSidebar({
    super.key,
    this.onItemSelected,
  });

  final VoidCallback? onItemSelected;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionProvider).valueOrNull;
    final menu = ref.watch(resolvedMenuProvider);
    final navState = ref.watch(navigationProvider);
    final tenantType = ref.watch(tenantTypeProvider);
    final notifier = ref.read(navigationProvider.notifier);

    return Container(
      color: AppColors.sidebarBg,
      child: Column(
        children: [
          _SidebarHeader(session: session, tenantType: tenantType),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
              children: [
                if (menu.isDriverLayout)
                  ...menu.standaloneItems.map(
                    (item) => _SidebarNavTile(
                      item: item,
                      selected: navState.selectedRouteId == item.id,
                      onTap: () {
                        notifier.selectRoute(item.id);
                        onItemSelected?.call();
                      },
                    ),
                  )
                else
                  ...menu.groups.expand((group) sync* {
                    if (!group.collapsible && group.items.length == 1) {
                      final item = group.items.first;
                      yield _SidebarNavTile(
                        item: item,
                        selected: navState.selectedRouteId == item.id,
                        onTap: () {
                          notifier.selectRoute(item.id);
                          onItemSelected?.call();
                        },
                      );
                      return;
                    }

                    if (!group.collapsible) {
                      for (final item in group.items) {
                        yield _SidebarNavTile(
                          item: item,
                          selected: navState.selectedRouteId == item.id,
                          onTap: () {
                            notifier.selectRoute(item.id);
                            onItemSelected?.call();
                          },
                        );
                      }
                      return;
                    }

                    final expanded =
                        navState.expandedGroupIds.contains(group.id);
                    yield _SidebarGroupHeader(
                      group: group,
                      expanded: expanded,
                      hasActiveChild: group.items
                          .any((i) => i.id == navState.selectedRouteId),
                      onTap: () => notifier.toggleGroup(group.id),
                    );
                    if (expanded) {
                      for (final item in group.items) {
                        yield _SidebarNavTile(
                          item: item,
                          selected: navState.selectedRouteId == item.id,
                          indented: true,
                          onTap: () {
                            notifier.selectRoute(item.id);
                            onItemSelected?.call();
                          },
                        );
                      }
                    }
                  }),
              ],
            ),
          ),
          const Divider(height: 1, color: AppColors.border),
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 12),
            child: Text(
              tenantType.accountTierLabel,
              style: const TextStyle(
                color: AppColors.textSoft,
                fontSize: 11,
                fontStyle: FontStyle.italic,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _SidebarHeader extends StatelessWidget {
  const _SidebarHeader({
    required this.session,
    required this.tenantType,
  });

  final AuthSession? session;
  final TenantType tenantType;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 20, 16, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: AppColors.primary,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: const Icon(Icons.directions_bus, color: Colors.white, size: 22),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Sheikh Travel',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                        color: AppColors.text,
                        letterSpacing: -0.2,
                      ),
                    ),
                    Text(
                      tenantType.productSubtitle,
                      style: const TextStyle(
                        fontSize: 10,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textMuted,
                        letterSpacing: 1.2,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: AppColors.sidebarProfileBg,
              borderRadius: BorderRadius.circular(14),
            ),
            child: Row(
              children: [
                CircleAvatar(
                  radius: 22,
                  backgroundColor: AppColors.primaryLight,
                  child: Text(
                    initials(session?.fullName),
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        session?.fullName ?? 'Guest',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w600,
                          color: AppColors.text,
                        ),
                      ),
                      Text(
                        roleLabel(session),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.textMuted,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SidebarGroupHeader extends StatelessWidget {
  const _SidebarGroupHeader({
    required this.group,
    required this.expanded,
    required this.hasActiveChild,
    required this.onTap,
  });

  final NavGroup group;
  final bool expanded;
  final bool hasActiveChild;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 4, bottom: 2),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(10),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(10),
              color: hasActiveChild && !expanded
                  ? AppColors.sidebarActiveBg.withValues(alpha: 0.5)
                  : null,
            ),
            child: Row(
              children: [
                Icon(
                  group.icon,
                  size: 20,
                  color: hasActiveChild
                      ? AppColors.sidebarActiveText
                      : AppColors.textMuted,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    group.label,
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: hasActiveChild
                          ? AppColors.sidebarActiveText
                          : AppColors.text,
                    ),
                  ),
                ),
                Icon(
                  expanded
                      ? Icons.keyboard_arrow_up
                      : Icons.keyboard_arrow_down,
                  size: 20,
                  color: AppColors.textMuted,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _SidebarNavTile extends StatelessWidget {
  const _SidebarNavTile({
    required this.item,
    required this.selected,
    required this.onTap,
    this.indented = false,
  });

  final NavItem item;
  final bool selected;
  final VoidCallback onTap;
  final bool indented;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(left: indented ? 18 : 0, bottom: 2),
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(10),
          child: Container(
            padding: EdgeInsets.symmetric(
              horizontal: indented ? 12 : 10,
              vertical: 9,
            ),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(10),
              color: selected ? AppColors.sidebarActiveBg : null,
              border: selected
                  ? Border.all(color: AppColors.primaryLight.withValues(alpha: 0.25))
                  : null,
            ),
            child: Row(
              children: [
                if (!indented)
                  Icon(
                    item.icon,
                    size: 20,
                    color: selected
                        ? AppColors.sidebarActiveText
                        : AppColors.textMuted,
                  ),
                if (!indented) const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    item.label,
                    style: TextStyle(
                      fontSize: indented ? 13.5 : 14,
                      fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
                      color: selected
                          ? AppColors.sidebarActiveText
                          : AppColors.textMuted,
                    ),
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
