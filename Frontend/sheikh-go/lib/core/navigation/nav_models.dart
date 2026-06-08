import 'package:flutter/material.dart';

enum NavRouteId {
  dashboard,
  notifications,
  bookings,
  trips,
  routes,
  dispatchBoard,
  vehicles,
  drivers,
  gpsTracking,
  fuelManagement,
  maintenance,
  customers,
  corporateAccounts,
  passengers,
  vendors,
  payments,
  invoices,
  wallets,
  expenses,
  reports,
  auditLogs,
  performanceAnalytics,
  users,
  rolesPermissions,
  tenantSettings,
  allowanceRules,
  systemConfiguration,
  myTrips,
  navigation,
  documents,
  wallet,
  profile,
}

class NavItem {
  const NavItem({
    required this.id,
    required this.label,
    required this.icon,
    this.adminOnly = false,
  });

  final NavRouteId id;
  final String label;
  final IconData icon;
  final bool adminOnly;
}

class NavGroup {
  const NavGroup({
    required this.id,
    required this.label,
    required this.icon,
    required this.items,
    this.collapsible = true,
  });

  final String id;
  final String label;
  final IconData icon;
  final List<NavItem> items;
  final bool collapsible;
}

class ResolvedMenu {
  const ResolvedMenu({
    required this.groups,
    required this.standaloneItems,
    required this.isDriverLayout,
  });

  final List<NavGroup> groups;
  final List<NavItem> standaloneItems;
  final bool isDriverLayout;

  List<NavItem> get allItems => [
        ...standaloneItems,
        for (final group in groups) ...group.items,
      ];
}
