import '../../../core/utils/jwt_utils.dart';

enum AuthMode { staff, driver }

/// Role codes aligned with backend `Roles.Code` values.
abstract final class FleetRole {
  static const superAdmin = 'SUPER_ADMIN';
  static const tenantAdmin = 'TENANT_ADMIN';
  static const fleetManager = 'FLEET_MANAGER';
  static const driverManager = 'DRIVER_MANAGER';
  static const dispatcher = 'DISPATCHER';
  static const driver = 'DRIVER';
  static const accountant = 'ACCOUNTANT';

  /// Priority for bottom-nav shell (first match wins).
  static const navRolePriority = [
    superAdmin,
    tenantAdmin,
    fleetManager,
    driverManager,
    dispatcher,
    accountant,
    driver,
  ];

  static const managementRoles = {
    superAdmin,
    tenantAdmin,
    fleetManager,
    driverManager,
    dispatcher,
    accountant,
  };
}

/// Permission codes aligned with backend RBAC templates.
abstract final class FleetPermissions {
  static const tripView = 'Trip.View';
  static const gpsView = 'GPS.View';
  static const vehicleView = 'Vehicle.View';
  static const driverView = 'Driver.View';
  static const driverAssign = 'Driver.Assign';
  static const driverManageStatus = 'Driver.ManageStatus';
  static const driverViewPerformance = 'Driver.ViewPerformance';
  static const driverUpdate = 'Driver.Update';
  static const fuelView = 'Fuel.View';
  static const maintenanceView = 'Maintenance.View';
  static const reportView = 'Report.View';
  static const dashboardView = 'Platform.Dashboard.View';
  static const bookingView = 'Booking.View';
  static const paymentView = 'Payment.View';
  static const invoiceView = 'Invoice.View';
  static const platformUsersView = 'Platform.Users.View';
  static const gpsCommandView = 'Gps.CommandView';
  static const gpsCommandSend = 'Gps.CommandSend';
  static const gpsCommandEngineCutoff = 'Gps.CommandEngineCutoff';
  static const aiView = 'Ai.View';
  static const aiManage = 'Ai.Manage';
  static const aiExecuteWrite = 'Ai.ExecuteWrite';
  static const notificationView = 'Notification.View';

  static const driverDefaults = [
    tripView,
    gpsView,
    fuelView,
    maintenanceView,
    notificationView,
  ];
}

class LoginRequest {
  const LoginRequest({required this.identifier, required this.password});

  /// Email for staff login, phone for driver login.
  final String identifier;
  final String password;

  bool get isEmailLogin => identifier.contains('@');

  Map<String, dynamic> toStaffJson() => {
        'email': identifier.trim(),
        'password': password,
      };

  Map<String, dynamic> toDriverJson() => {
        'phone': identifier.trim(),
        'password': password,
      };
}

class FleetSession {
  const FleetSession({
    required this.accessToken,
    required this.refreshToken,
    required this.userId,
    required this.tenantId,
    required this.displayName,
    required this.roles,
    required this.permissions,
    required this.authMode,
    this.driverId,
    this.email,
    this.phone,
  });

  final String accessToken;
  final String refreshToken;
  final int userId;
  final int tenantId;
  final String displayName;
  final List<String> roles;
  final List<String> permissions;
  final AuthMode authMode;
  final int? driverId;
  final String? email;
  final String? phone;

  /// Backward-compatible alias used across existing screens.
  String get fullName => displayName;

  bool get isDriverSession =>
      authMode == AuthMode.driver || driverId != null && _isDriverOnlyRoleSet;

  bool get _isDriverOnlyRoleSet {
    final normalized = roles.map((r) => r.toUpperCase()).toSet();
    if (normalized.isEmpty) return driverId != null;
    return normalized.contains(FleetRole.driver) &&
        !normalized.any(FleetRole.managementRoles.contains);
  }

  bool get isDriverOnly =>
      isDriverSession &&
      !roles.any((r) => FleetRole.managementRoles.contains(r.toUpperCase()));

  bool hasRole(String role) =>
      roles.any((r) => r.toUpperCase() == role.toUpperCase());

  bool hasAnyRole(Iterable<String> codes) =>
      codes.any((code) => hasRole(code));

  bool hasPermission(String permission) =>
      permissions.any((p) => p.toLowerCase() == permission.toLowerCase());

  bool hasAnyPermission(Iterable<String> codes) =>
      codes.any(hasPermission);

  /// Resolves which bottom-nav shell to show (role-first).
  String get primaryNavRole {
    if (isDriverOnly || authMode == AuthMode.driver) {
      return FleetRole.driver;
    }
    for (final code in FleetRole.navRolePriority) {
      if (hasRole(code)) return code;
    }
    if (hasPermission(FleetPermissions.bookingView)) {
      return FleetRole.dispatcher;
    }
    if (hasAnyPermission(const [
      FleetPermissions.paymentView,
      FleetPermissions.invoiceView,
    ])) {
      return FleetRole.accountant;
    }
    if (hasAnyPermission(const [
      FleetPermissions.gpsView,
      FleetPermissions.vehicleView,
    ])) {
      return FleetRole.fleetManager;
    }
    return FleetRole.driver;
  }

  bool get canSeeFleetTab =>
      !isDriverOnly &&
      (hasAnyRole(const [
            FleetRole.superAdmin,
            FleetRole.tenantAdmin,
            FleetRole.fleetManager,
            FleetRole.dispatcher,
            FleetRole.driverManager,
          ]) ||
          hasPermission(FleetPermissions.gpsView) ||
          hasPermission(FleetPermissions.vehicleView));

  bool get canSeeTripsTab =>
      hasPermission(FleetPermissions.tripView) || isDriverSession;

  bool get canSeeAiTab =>
      hasPermission(FleetPermissions.aiView) ||
      hasAnyRole(const [
        FleetRole.superAdmin,
        FleetRole.tenantAdmin,
        FleetRole.fleetManager,
      ]);

  bool get canSeeBookingsTab =>
      !isDriverOnly && hasPermission(FleetPermissions.bookingView);

  bool get canSeeFinanceTab =>
      !isDriverOnly &&
      hasAnyPermission(const [
        FleetPermissions.paymentView,
        FleetPermissions.invoiceView,
        FleetPermissions.reportView,
      ]);

  bool get canSeeUsersTab =>
      !isDriverOnly && hasPermission(FleetPermissions.platformUsersView);

  bool get canSeeDriversTab =>
      !isDriverOnly && hasPermission(FleetPermissions.driverView);

  factory FleetSession.fromStaffJson(Map<String, dynamic> json) {
    final accessToken =
        json['accessToken'] as String? ?? json['AccessToken'] as String? ?? '';
    final refreshToken =
        json['refreshToken'] as String? ?? json['RefreshToken'] as String? ?? '';
    final roles = _parseStringList(json['roles'] ?? json['Roles']);
    final primaryRole =
        json['role'] as String? ?? json['Role'] as String? ?? '';
    final mergedRoles = roles.isEmpty && primaryRole.isNotEmpty
        ? [primaryRole]
        : roles;
    final permissions =
        _parseStringList(json['permissions'] ?? json['Permissions']);
    final userId = json['userId'] as int? ??
        json['UserId'] as int? ??
        JwtUtils.claimInt(accessToken, 'userId') ??
        JwtUtils.claimInt(accessToken, 'sub') ??
        0;
    final tenantId = json['tenantId'] as int? ??
        json['TenantId'] as int? ??
        JwtUtils.claimInt(accessToken, 'tenant_id') ??
        0;
    final driverId = JwtUtils.claimInt(accessToken, 'driver_id');

    return FleetSession(
      accessToken: accessToken,
      refreshToken: refreshToken,
      userId: userId,
      tenantId: tenantId,
      displayName: json['fullName'] as String? ??
          json['FullName'] as String? ??
          '',
      roles: mergedRoles.map((r) => r.toUpperCase()).toList(),
      permissions: permissions,
      authMode: AuthMode.staff,
      driverId: driverId,
      email: json['email'] as String? ?? json['Email'] as String?,
      phone: json['phoneNumber'] as String? ??
          json['PhoneNumber'] as String? ??
          json['phone'] as String? ??
          json['Phone'] as String?,
    );
  }

  factory FleetSession.fromDriverJson(Map<String, dynamic> json) {
    final accessToken =
        json['accessToken'] as String? ?? json['AccessToken'] as String? ?? '';
    final refreshToken =
        json['refreshToken'] as String? ?? json['RefreshToken'] as String? ?? '';
    final driverId = json['driverId'] as int? ?? json['DriverId'] as int? ?? 0;
    final userId = JwtUtils.claimInt(accessToken, 'userId') ??
        JwtUtils.claimInt(accessToken, 'sub') ??
        0;
    final tenantId = JwtUtils.claimInt(accessToken, 'tenant_id') ?? 0;

    return FleetSession(
      accessToken: accessToken,
      refreshToken: refreshToken,
      userId: userId,
      tenantId: tenantId,
      displayName: json['fullName'] as String? ??
          json['FullName'] as String? ??
          '',
      roles: const [FleetRole.driver],
      permissions: FleetPermissions.driverDefaults,
      authMode: AuthMode.driver,
      driverId: driverId,
      phone: json['phone'] as String? ?? json['Phone'] as String?,
    );
  }

  /// Migrates legacy persisted driver-only sessions.
  factory FleetSession.fromLegacyDriverJson(Map<String, dynamic> json) {
    final session = FleetSession.fromDriverJson(json);
    final resolvedUserId =
        session.userId != 0 ? session.userId : session.driverId ?? 0;
    return FleetSession(
      accessToken: session.accessToken,
      refreshToken: session.refreshToken,
      userId: resolvedUserId,
      tenantId: session.tenantId,
      displayName: session.displayName,
      roles: session.roles,
      permissions: session.permissions,
      authMode: session.authMode,
      driverId: session.driverId,
      email: session.email,
      phone: session.phone,
    );
  }

  factory FleetSession.fromJson(Map<String, dynamic> json) {
    final mode = json['authMode'] as String?;
    if (mode == AuthMode.driver.name) {
      return FleetSession.fromDriverJson(json);
    }
    if (mode == AuthMode.staff.name) {
      return FleetSession.fromStaffJson(json);
    }
    if (json.containsKey('driverId') && !json.containsKey('roles')) {
      return FleetSession.fromLegacyDriverJson(json);
    }
    return FleetSession.fromStaffJson(json);
  }

  Map<String, dynamic> toJson() => {
        'accessToken': accessToken,
        'refreshToken': refreshToken,
        'userId': userId,
        'tenantId': tenantId,
        'displayName': displayName,
        'fullName': displayName,
        'roles': roles,
        'permissions': permissions,
        'authMode': authMode.name,
        if (driverId != null) 'driverId': driverId,
        if (email != null) 'email': email,
        if (phone != null) 'phone': phone,
      };

  FleetSession copyWith({
    String? accessToken,
    String? refreshToken,
  }) {
    return FleetSession(
      accessToken: accessToken ?? this.accessToken,
      refreshToken: refreshToken ?? this.refreshToken,
      userId: userId,
      tenantId: tenantId,
      displayName: displayName,
      roles: roles,
      permissions: permissions,
      authMode: authMode,
      driverId: driverId,
      email: email,
      phone: phone,
    );
  }

  static List<String> _parseStringList(Object? raw) {
    if (raw is List) {
      return raw.map((e) => e.toString()).where((e) => e.isNotEmpty).toList();
    }
    return const [];
  }
}

/// @deprecated Use [FleetSession]. Kept for incremental migration in tests.
typedef DriverSession = FleetSession;
