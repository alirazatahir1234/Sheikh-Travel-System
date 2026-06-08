import 'package:flutter/material.dart';

import 'nav_models.dart';
import 'tenant_type.dart';

abstract final class MenuConfig {
  static const dashboardGroup = NavGroup(
    id: 'dashboard',
    label: 'Dashboard',
    icon: Icons.dashboard_outlined,
    collapsible: false,
    items: [
      NavItem(
        id: NavRouteId.dashboard,
        label: 'Dashboard',
        icon: Icons.dashboard_outlined,
      ),
      NavItem(
        id: NavRouteId.notifications,
        label: 'Notifications',
        icon: Icons.notifications_outlined,
      ),
    ],
  );

  static const operationsGroup = NavGroup(
    id: 'operations',
    label: 'Operations',
    icon: Icons.settings_outlined,
    items: [
      NavItem(
        id: NavRouteId.bookings,
        label: 'Bookings',
        icon: Icons.confirmation_number_outlined,
      ),
      NavItem(
        id: NavRouteId.trips,
        label: 'Trips',
        icon: Icons.route_outlined,
      ),
      NavItem(
        id: NavRouteId.routes,
        label: 'Routes',
        icon: Icons.alt_route,
      ),
      NavItem(
        id: NavRouteId.dispatchBoard,
        label: 'Dispatch Board',
        icon: Icons.view_kanban_outlined,
      ),
    ],
  );

  static const fleetGroup = NavGroup(
    id: 'fleet',
    label: 'Fleet Management',
    icon: Icons.local_shipping_outlined,
    items: [
      NavItem(
        id: NavRouteId.vehicles,
        label: 'Vehicles',
        icon: Icons.directions_bus_outlined,
      ),
      NavItem(
        id: NavRouteId.drivers,
        label: 'Drivers',
        icon: Icons.badge_outlined,
      ),
      NavItem(
        id: NavRouteId.gpsTracking,
        label: 'GPS Tracking',
        icon: Icons.my_location_outlined,
      ),
      NavItem(
        id: NavRouteId.fuelManagement,
        label: 'Fuel Management',
        icon: Icons.local_gas_station_outlined,
      ),
      NavItem(
        id: NavRouteId.maintenance,
        label: 'Maintenance',
        icon: Icons.build_outlined,
      ),
    ],
  );

  static const customersGroup = NavGroup(
    id: 'customers',
    label: 'Customer Management',
    icon: Icons.groups_outlined,
    items: [
      NavItem(
        id: NavRouteId.customers,
        label: 'Customers',
        icon: Icons.group_outlined,
      ),
      NavItem(
        id: NavRouteId.corporateAccounts,
        label: 'Corporate Accounts',
        icon: Icons.business_outlined,
      ),
      NavItem(
        id: NavRouteId.passengers,
        label: 'Passengers',
        icon: Icons.person_outline,
      ),
      NavItem(
        id: NavRouteId.vendors,
        label: 'Vendors',
        icon: Icons.storefront_outlined,
      ),
    ],
  );

  static const financeGroup = NavGroup(
    id: 'finance',
    label: 'Finance',
    icon: Icons.account_balance_wallet_outlined,
    items: [
      NavItem(
        id: NavRouteId.payments,
        label: 'Payments',
        icon: Icons.payments_outlined,
      ),
      NavItem(
        id: NavRouteId.invoices,
        label: 'Invoices',
        icon: Icons.receipt_long_outlined,
      ),
      NavItem(
        id: NavRouteId.wallets,
        label: 'Wallets',
        icon: Icons.account_balance_outlined,
      ),
      NavItem(
        id: NavRouteId.expenses,
        label: 'Expenses',
        icon: Icons.money_off_outlined,
      ),
    ],
  );

  static const analyticsGroup = NavGroup(
    id: 'analytics',
    label: 'Analytics',
    icon: Icons.bar_chart_outlined,
    items: [
      NavItem(
        id: NavRouteId.reports,
        label: 'Reports',
        icon: Icons.insights_outlined,
      ),
      NavItem(
        id: NavRouteId.auditLogs,
        label: 'Audit Logs',
        icon: Icons.history,
        adminOnly: true,
      ),
      NavItem(
        id: NavRouteId.performanceAnalytics,
        label: 'Performance Analytics',
        icon: Icons.speed_outlined,
      ),
    ],
  );

  static const administrationGroup = NavGroup(
    id: 'administration',
    label: 'Administration',
    icon: Icons.admin_panel_settings_outlined,
    items: [
      NavItem(
        id: NavRouteId.users,
        label: 'Users',
        icon: Icons.manage_accounts_outlined,
        adminOnly: true,
      ),
      NavItem(
        id: NavRouteId.rolesPermissions,
        label: 'Roles & Permissions',
        icon: Icons.security_outlined,
        adminOnly: true,
      ),
      NavItem(
        id: NavRouteId.tenantSettings,
        label: 'Tenant Settings',
        icon: Icons.tune_outlined,
        adminOnly: true,
      ),
      NavItem(
        id: NavRouteId.allowanceRules,
        label: 'Allowance Rules',
        icon: Icons.rule_outlined,
        adminOnly: true,
      ),
      NavItem(
        id: NavRouteId.systemConfiguration,
        label: 'System Configuration',
        icon: Icons.settings_suggest_outlined,
        adminOnly: true,
      ),
    ],
  );

  static const allGroups = [
    dashboardGroup,
    operationsGroup,
    fleetGroup,
    customersGroup,
    financeGroup,
    analyticsGroup,
    administrationGroup,
  ];

  static const driverItems = [
    NavItem(
      id: NavRouteId.myTrips,
      label: 'My Trips',
      icon: Icons.route_outlined,
    ),
    NavItem(
      id: NavRouteId.navigation,
      label: 'Navigation',
      icon: Icons.navigation_outlined,
    ),
    NavItem(
      id: NavRouteId.documents,
      label: 'Documents',
      icon: Icons.description_outlined,
    ),
    NavItem(
      id: NavRouteId.wallet,
      label: 'Wallet',
      icon: Icons.account_balance_wallet_outlined,
    ),
    NavItem(
      id: NavRouteId.profile,
      label: 'Profile',
      icon: Icons.person_outline,
    ),
  ];

  static const _tenantGroupIds = {
    TenantType.travelAgency: [
      'dashboard',
      'operations',
      'customers',
      'finance',
      'analytics',
      'administration',
    ],
    TenantType.fleetOperator: [
      'dashboard',
      'fleet',
      'analytics',
      'administration',
    ],
    TenantType.corporateCustomer: [
      'dashboard',
      'operations',
      'customers',
      'finance',
      'analytics',
      'administration',
    ],
  };

  static const _tenantItemIds = {
    TenantType.corporateCustomer: {
      NavRouteId.bookings,
      NavRouteId.notifications,
      NavRouteId.dashboard,
      NavRouteId.corporateAccounts,
      NavRouteId.passengers,
      NavRouteId.invoices,
      NavRouteId.reports,
      NavRouteId.tenantSettings,
    },
    TenantType.fleetOperator: {
      NavRouteId.dashboard,
      NavRouteId.notifications,
      NavRouteId.vehicles,
      NavRouteId.drivers,
      NavRouteId.gpsTracking,
      NavRouteId.maintenance,
      NavRouteId.reports,
      NavRouteId.tenantSettings,
    },
  };

  static ResolvedMenu resolve({
    required TenantType tenantType,
    required List<String> roles,
    List<String> enabledModules = const [],
  }) {
    if (tenantType == TenantType.driver) {
      return const ResolvedMenu(
        groups: [],
        standaloneItems: driverItems,
        isDriverLayout: true,
      );
    }

    final isAdmin = roles.contains('Admin');
    final groupIds = _tenantGroupIds[tenantType] ?? _tenantGroupIds[TenantType.travelAgency]!;
    final itemFilter = _tenantItemIds[tenantType];

    final groups = <NavGroup>[];
    for (final group in allGroups) {
      if (!groupIds.contains(group.id)) continue;

      final items = group.items.where((item) {
        if (item.adminOnly && !isAdmin) return false;
        if (itemFilter != null && !itemFilter.contains(item.id)) return false;
        if (enabledModules.isNotEmpty &&
            !_moduleKeyMatches(item.id, enabledModules)) {
          return false;
        }
        return true;
      }).toList();

      if (items.isEmpty) continue;
      groups.add(NavGroup(
        id: group.id,
        label: group.label,
        icon: group.icon,
        collapsible: group.collapsible,
        items: items,
      ));
    }

    return ResolvedMenu(
      groups: groups,
      standaloneItems: const [],
      isDriverLayout: false,
    );
  }

  static bool _moduleKeyMatches(NavRouteId id, List<String> enabledModules) {
    final key = _moduleKeyFor(id);
    return key == null || enabledModules.contains(key);
  }

  static String? _moduleKeyFor(NavRouteId id) => switch (id) {
        NavRouteId.dashboard => 'dashboard',
        NavRouteId.bookings => 'bookings',
        NavRouteId.trips => 'trips',
        NavRouteId.routes => 'routes',
        NavRouteId.vehicles => 'vehicles',
        NavRouteId.drivers => 'drivers',
        NavRouteId.gpsTracking => 'gps-tracking',
        NavRouteId.fuelManagement => 'fuel-logs',
        NavRouteId.maintenance => 'maintenance',
        NavRouteId.customers => 'customers',
        NavRouteId.payments => 'payments',
        NavRouteId.reports => 'reports',
        NavRouteId.auditLogs => 'audit-logs',
        NavRouteId.users => 'users',
        NavRouteId.allowanceRules => 'driver-allowance-rules',
        _ => null,
      };

  static String labelFor(NavRouteId id) {
    for (final group in allGroups) {
      for (final item in group.items) {
        if (item.id == id) return item.label;
      }
    }
    for (final item in driverItems) {
      if (item.id == id) return item.label;
    }
    return id.name;
  }
}
