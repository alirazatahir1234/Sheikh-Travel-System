import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../models/auth_session.dart';
import '../services/session_storage.dart';
import 'menu_config.dart';
import 'nav_models.dart';
import 'tenant_type.dart';

final tenantTypeProvider = Provider<TenantType>((ref) {
  final session = ref.watch(sessionProvider).valueOrNull;
  return resolveTenantType(roles: session?.roles ?? const []);
});

final resolvedMenuProvider = Provider<ResolvedMenu>((ref) {
  final session = ref.watch(sessionProvider).valueOrNull;
  final tenantType = ref.watch(tenantTypeProvider);
  return MenuConfig.resolve(
    tenantType: tenantType,
    roles: session?.roles ?? const [],
  );
});

class NavigationState {
  const NavigationState({
    required this.selectedRouteId,
    required this.expandedGroupIds,
  });

  final NavRouteId selectedRouteId;
  final Set<String> expandedGroupIds;

  NavigationState copyWith({
    NavRouteId? selectedRouteId,
    Set<String>? expandedGroupIds,
  }) {
    return NavigationState(
      selectedRouteId: selectedRouteId ?? this.selectedRouteId,
      expandedGroupIds: expandedGroupIds ?? this.expandedGroupIds,
    );
  }
}

class NavigationNotifier extends StateNotifier<NavigationState> {
  NavigationNotifier(ResolvedMenu menu)
      : super(
          NavigationState(
            selectedRouteId: menu.isDriverLayout
                ? NavRouteId.myTrips
                : NavRouteId.dashboard,
            expandedGroupIds: _defaultExpanded(menu),
          ),
        );

  static Set<String> _defaultExpanded(ResolvedMenu menu) {
    return menu.groups
        .where((g) => g.collapsible)
        .map((g) => g.id)
        .toSet();
  }

  void selectRoute(NavRouteId id) {
    state = state.copyWith(selectedRouteId: id);
  }

  void toggleGroup(String groupId) {
    final expanded = Set<String>.from(state.expandedGroupIds);
    if (expanded.contains(groupId)) {
      expanded.remove(groupId);
    } else {
      expanded.add(groupId);
    }
    state = state.copyWith(expandedGroupIds: expanded);
  }

  void resetForMenu(ResolvedMenu menu) {
    state = NavigationState(
      selectedRouteId:
          menu.isDriverLayout ? NavRouteId.myTrips : NavRouteId.dashboard,
      expandedGroupIds: _defaultExpanded(menu),
    );
  }
}

final navigationProvider =
    StateNotifierProvider<NavigationNotifier, NavigationState>((ref) {
  final menu = ref.watch(resolvedMenuProvider);
  final notifier = NavigationNotifier(menu);
  ref.listen(resolvedMenuProvider, (previous, next) {
    if (previous != next) {
      notifier.resetForMenu(next);
    }
  });
  return notifier;
});

String roleLabel(AuthSession? session) {
  if (session == null || session.roles.isEmpty) return 'Member';
  return session.roles.first;
}

String initials(String? fullName) {
  if (fullName == null || fullName.trim().isEmpty) return '?';
  return fullName
      .trim()
      .split(RegExp(r'\s+'))
      .where((part) => part.isNotEmpty)
      .map((part) => part[0])
      .take(2)
      .join()
      .toUpperCase();
}
