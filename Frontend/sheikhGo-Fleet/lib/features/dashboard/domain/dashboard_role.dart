/// Dashboard layout roles — mirrors [FleetSession.primaryNavRole] codes.
enum DashboardRole {
  driver,
  dispatcher,
  fleetManager,
  driverManager,
  accountant,
  tenantAdmin,
  superAdmin,
}

extension DashboardRoleX on DashboardRole {
  String get label => switch (this) {
        DashboardRole.driver => 'Driver',
        DashboardRole.dispatcher => 'Dispatcher',
        DashboardRole.fleetManager => 'Fleet Manager',
        DashboardRole.driverManager => 'Driver Manager',
        DashboardRole.accountant => 'Accountant',
        DashboardRole.tenantAdmin => 'Tenant Admin',
        DashboardRole.superAdmin => 'Super Admin',
      };

  String get subtitle => switch (this) {
        DashboardRole.driver => 'Your trips and day status',
        DashboardRole.dispatcher => 'Trips, assignments, and delays',
        DashboardRole.fleetManager => 'Overview of your fleet in real-time',
        DashboardRole.driverManager => 'Drivers, docs, and performance',
        DashboardRole.accountant => 'Costs, fuel, and reports',
        DashboardRole.tenantAdmin => 'Business overview — Owner view',
        DashboardRole.superAdmin => 'Platform admin — tenant scope',
      };

  /// PRD marketing name for Command Dashboard docs/UI.
  String get commandLabel => switch (this) {
        DashboardRole.driver => 'Driver',
        DashboardRole.dispatcher => 'Dispatcher',
        DashboardRole.fleetManager => 'Fleet Manager',
        DashboardRole.driverManager => 'Supervisor',
        DashboardRole.accountant => 'Accountant',
        DashboardRole.tenantAdmin => 'Owner',
        DashboardRole.superAdmin => 'Owner',
      };

  static DashboardRole fromNavRole(String code) {
    switch (code.toUpperCase()) {
      case 'SUPER_ADMIN':
        return DashboardRole.superAdmin;
      case 'TENANT_ADMIN':
        return DashboardRole.tenantAdmin;
      case 'FLEET_MANAGER':
        return DashboardRole.fleetManager;
      case 'DRIVER_MANAGER':
        return DashboardRole.driverManager;
      case 'DISPATCHER':
        return DashboardRole.dispatcher;
      case 'ACCOUNTANT':
        return DashboardRole.accountant;
      case 'DRIVER':
      default:
        return DashboardRole.driver;
    }
  }
}
