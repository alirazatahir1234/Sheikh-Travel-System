import '../../../core/utils/jwt_utils.dart';

enum AuthMode { staff, driver }

/// Role codes aligned with backend `Roles.Code` values.
abstract final class FleetRole {
  static const superAdmin = 'SUPER_ADMIN';
  static const tenantAdmin = 'TENANT_ADMIN';
  static const fleetManager = 'FLEET_MANAGER';
  static const driverManager = 'DRIVER_MANAGER';
  static const gpsOperator = 'GPS_OPERATOR';
  static const dispatcher = 'DISPATCHER';
  static const driver = 'DRIVER';
  static const accountant = 'ACCOUNTANT';

  /// Priority for bottom-nav shell (first match wins).
  static const navRolePriority = [
    superAdmin,
    tenantAdmin,
    fleetManager,
    driverManager,
    gpsOperator,
    dispatcher,
    accountant,
    driver,
  ];

  static const managementRoles = {
    superAdmin,
    tenantAdmin,
    fleetManager,
    driverManager,
    gpsOperator,
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
  const LoginRequest({
    required this.identifier,
    required this.password,
    this.rememberMe = true,
  });

  /// Email for staff login, phone for driver login.
  final String identifier;
  final String password;

  /// Client-only: when false, session is not restored after app restart.
  final bool rememberMe;

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
    this.companyContext,
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
  final CompanyContext? companyContext;

  /// Product alias — persistence remains tenantId.
  int get companyId => companyContext?.companyId ?? tenantId;
  String? get companyName => companyContext?.companyName;

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

  /// True when primary shell is GPS Operator (mobile V1 monitoring role).
  bool get isGpsOperator =>
      !isDriverOnly && hasRole(FleetRole.gpsOperator) && primaryNavRole == FleetRole.gpsOperator;

  bool get canSeeFleetTab =>
      !isDriverOnly &&
      (hasAnyRole(const [
            FleetRole.superAdmin,
            FleetRole.tenantAdmin,
            FleetRole.fleetManager,
            FleetRole.gpsOperator,
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
    FleetSession session;
    if (mode == AuthMode.driver.name) {
      session = FleetSession.fromDriverJson(json);
    } else if (mode == AuthMode.staff.name) {
      session = FleetSession.fromStaffJson(json);
    } else if (json.containsKey('driverId') && !json.containsKey('roles')) {
      session = FleetSession.fromLegacyDriverJson(json);
    } else {
      session = FleetSession.fromStaffJson(json);
    }

    final rawContext = json['companyContext'] as Map<String, dynamic>?;
    if (rawContext == null) return session;
    return session.copyWith(companyContext: CompanyContext.fromJson(rawContext));
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
        if (companyContext != null) 'companyContext': companyContext!.toJson(),
      };

  FleetSession copyWith({
    String? accessToken,
    String? refreshToken,
    CompanyContext? companyContext,
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
      companyContext: companyContext ?? this.companyContext,
    );
  }

  static List<String> _parseStringList(Object? raw) {
    if (raw is List) {
      return raw.map((e) => e.toString()).where((e) => e.isNotEmpty).toList();
    }
    return const [];
  }
}

/// Read-only company context for mobile (no admin CRUD).
class CompanyContext {
  const CompanyContext({
    required this.companyId,
    required this.tenantId,
    required this.companyName,
    this.slug,
    this.logoUrl,
    this.primaryColor,
    this.branchId,
    this.branchName,
    this.departmentId,
    this.departmentName,
    this.enabledModuleKeys = const [],
    this.featureKeys = const [],
    this.features = const [],
    this.modules = const [],
    this.workspaceHint,
    this.workspace,
    this.roleCode,
    this.subscription,
    this.currentUser,
    this.assignedRoles = const [],
    this.effectivePermissions = const [],
    this.navSummary,
    this.dashboard,
    this.dataScope,
    this.security,
    this.audit,
  });

  final int companyId;
  final int tenantId;
  final String companyName;
  final String? slug;
  final String? logoUrl;
  final String? primaryColor;
  final int? branchId;
  final String? branchName;
  final int? departmentId;
  final String? departmentName;
  final List<String> enabledModuleKeys;
  final List<String> featureKeys;
  final List<CompanyFeature> features;
  final List<CompanyModule> modules;
  final String? workspaceHint;
  final CompanyWorkspace? workspace;
  final String? roleCode;
  final CompanySubscription? subscription;
  final CompanyCurrentUser? currentUser;
  final List<AssignedRole> assignedRoles;
  final List<EffectivePermission> effectivePermissions;
  final CompanyNavSummary? navSummary;
  final CompanyDashboardSummary? dashboard;
  final CompanyDataScope? dataScope;
  final CompanySecuritySummary? security;
  final CompanyAuditSummary? audit;

  String? get jobTitle => currentUser?.jobTitle;
  String? get employeeType => currentUser?.employeeType;
  String? get theme => currentUser?.theme;
  String? get language => currentUser?.language;

  /// Display label for the resolved workspace (Stage 10).
  String? get effectiveWorkspace =>
      workspace?.displayName ??
      workspace?.key ??
      currentUser?.defaultWorkspaceKey ??
      workspaceHint;

  String? get workspaceKey =>
      workspace?.key ?? currentUser?.defaultWorkspaceKey ?? workspaceHint;

  /// Display names for assigned platform roles (prefer over legacy roleCode).
  List<String> get roleDisplayLabels {
    if (assignedRoles.isNotEmpty) {
      return assignedRoles
          .map((r) => r.displayName.isNotEmpty ? r.displayName : r.code)
          .where((n) => n.isNotEmpty)
          .toList();
    }
    if (roleCode != null && roleCode!.isNotEmpty) return [roleCode!];
    return const [];
  }

  /// Category labels derived from effective permissions (Stage 8).
  List<String> get permissionCategoryLabels {
    if (effectivePermissions.isEmpty) return const [];
    final seen = <String>{};
    final out = <String>[];
    for (final p in effectivePermissions) {
      final label = (p.category != null && p.category!.isNotEmpty)
          ? p.category!
          : (p.displayName.isNotEmpty ? p.displayName : p.code);
      if (label.isNotEmpty && seen.add(label)) out.add(label);
    }
    return out;
  }

  /// Display labels for installed modules (Fleet, GPS, …).
  List<String> get moduleDisplayLabels {
    if (modules.isNotEmpty) {
      return modules
          .map((m) => m.displayName.isNotEmpty ? m.displayName : m.name)
          .where((n) => n.isNotEmpty)
          .toList();
    }
    // Fallback from legacy keys when modules[] missing
    const labels = <String, String>{
      'vehicles': 'Fleet',
      'drivers': 'Fleet',
      'maintenance': 'Maintenance',
      'fuel-logs': 'Fuel',
      'gps-tracking': 'GPS',
      'reports': 'Reports',
      'bookings': 'Travel',
      'trips': 'Trips',
    };
    final seen = <String>{};
    final out = <String>[];
    for (final key in enabledModuleKeys) {
      final label = labels[key.toLowerCase()];
      if (label != null && seen.add(label)) out.add(label);
    }
    return out;
  }

  /// Category / display labels for enabled features.
  List<String> get featureDisplayLabels {
    if (features.isNotEmpty) {
      final seen = <String>{};
      final out = <String>[];
      for (final f in features.where((x) => x.isEnabled)) {
        final label = f.category?.isNotEmpty == true
            ? f.category!
            : (f.displayName.isNotEmpty
                ? f.displayName
                : (f.name.isNotEmpty ? f.name : f.featureKey));
        if (label.isNotEmpty && seen.add(label)) out.add(label);
      }
      return out;
    }
    return featureKeys;
  }

  /// Nav module / mobile item labels from company context (Stage 9, read-only).
  List<String> get navDisplayLabels {
    final summary = navSummary;
    if (summary == null) return const [];
    final seen = <String>{};
    final out = <String>[];
    for (final label in [
      ...summary.topModuleLabels,
      ...summary.mobileItemLabels,
    ]) {
      if (label.isNotEmpty && seen.add(label)) out.add(label);
    }
    return out;
  }

  /// Data scope labels (Stage 12, read-only).
  List<String> get dataScopeDisplayLabels {
    final scope = dataScope;
    if (scope == null) return const [];
    if (scope.isCompanyWide) return const ['Company-wide'];
    final seen = <String>{};
    final out = <String>[];
    for (final label in [...scope.branchLabels, ...scope.departmentLabels]) {
      if (label.isNotEmpty && seen.add(label)) out.add(label);
    }
    if (out.isEmpty && scope.mode.isNotEmpty) out.add(scope.mode);
    return out;
  }

  /// Dashboard display chip (Stage 11, read-only).
  String? get dashboardDisplayLabel {
    final name = dashboard?.displayName?.trim();
    if (name != null && name.isNotEmpty) return name;
    final key = dashboard?.key?.trim();
    if (key != null && key.isNotEmpty) return key;
    return currentUser?.defaultDashboardKey;
  }

  /// Safe security summary chips (Stage 13, read-only — no IP/complexity).
  List<String> get securityDisplayLabels {
    final s = security;
    if (s == null) return const [];
    final out = <String>[];
    if (s.passwordExpired == true) {
      out.add('Password expired');
    } else if (s.passwordDaysRemaining != null) {
      out.add('Password expires in ${s.passwordDaysRemaining}d');
    } else if (s.passwordExpiryDays != null && s.passwordExpiryDays! > 0) {
      out.add('Password max age ${s.passwordExpiryDays}d');
    }
    if (s.idleTimeoutMinutes != null && s.idleTimeoutMinutes! > 0) {
      out.add('Idle ${s.idleTimeoutMinutes}m');
    }
    if (s.lockoutEnabled) out.add('Lockout on');
    if (s.auditLevel.isNotEmpty) out.add('Audit ${s.auditLevel}');
    return out;
  }

  /// Audit summary chips (Stage 14, read-only — no event list).
  List<String> get auditDisplayLabels {
    final a = audit;
    if (a == null) return const [];
    final out = <String>[];
    out.add(a.enabled ? 'Audit on' : 'Audit off');
    if (a.retentionDays > 0) out.add('Retain ${a.retentionDays}d');
    return out;
  }

  factory CompanyContext.fromJson(Map<String, dynamic> json) {
    final featuresRaw = json['features'] ?? json['Features'];
    final featureKeys = <String>[];
    final features = <CompanyFeature>[];
    if (featuresRaw is List) {
      for (final item in featuresRaw) {
        if (item is Map<String, dynamic>) {
          final feature = CompanyFeature.fromJson(item);
          features.add(feature);
          if (feature.isEnabled && feature.featureKey.isNotEmpty) {
            featureKeys.add(feature.featureKey);
          }
        } else if (item is Map) {
          final feature =
              CompanyFeature.fromJson(Map<String, dynamic>.from(item));
          features.add(feature);
          if (feature.isEnabled && feature.featureKey.isNotEmpty) {
            featureKeys.add(feature.featureKey);
          }
        } else if (item != null) {
          featureKeys.add(item.toString());
        }
      }
    }

    final modulesRaw = json['modules'] ?? json['Modules'];
    final modules = <CompanyModule>[];
    if (modulesRaw is List) {
      for (final item in modulesRaw) {
        if (item is Map<String, dynamic>) {
          modules.add(CompanyModule.fromJson(item));
        } else if (item is Map) {
          modules.add(CompanyModule.fromJson(Map<String, dynamic>.from(item)));
        }
      }
    }

    CompanySubscription? subscription;
    final subRaw = json['subscription'] ?? json['Subscription'];
    if (subRaw is Map<String, dynamic>) {
      subscription = CompanySubscription.fromJson(subRaw);
    } else if (subRaw is Map) {
      subscription =
          CompanySubscription.fromJson(Map<String, dynamic>.from(subRaw));
    }

    CompanyCurrentUser? currentUser;
    final userRaw = json['currentUser'] ?? json['CurrentUser'];
    if (userRaw is Map<String, dynamic>) {
      currentUser = CompanyCurrentUser.fromJson(userRaw);
    } else if (userRaw is Map) {
      currentUser =
          CompanyCurrentUser.fromJson(Map<String, dynamic>.from(userRaw));
    }

    final assignedRoles = <AssignedRole>[];
    final rolesRaw = json['assignedRoles'] ?? json['AssignedRoles'];
    if (rolesRaw is List) {
      for (final item in rolesRaw) {
        if (item is Map<String, dynamic>) {
          assignedRoles.add(AssignedRole.fromJson(item));
        } else if (item is Map) {
          assignedRoles.add(
              AssignedRole.fromJson(Map<String, dynamic>.from(item)));
        }
      }
    }

    final effectivePermissions = <EffectivePermission>[];
    final permsRaw = json['effectivePermissions'] ?? json['EffectivePermissions'];
    if (permsRaw is List) {
      for (final item in permsRaw) {
        if (item is Map<String, dynamic>) {
          effectivePermissions.add(EffectivePermission.fromJson(item));
        } else if (item is Map) {
          effectivePermissions.add(
              EffectivePermission.fromJson(Map<String, dynamic>.from(item)));
        }
      }
    }

    CompanyNavSummary? navSummary;
    final navRaw = json['navSummary'] ?? json['NavSummary'];
    if (navRaw is Map<String, dynamic>) {
      navSummary = CompanyNavSummary.fromJson(navRaw);
    } else if (navRaw is Map) {
      navSummary =
          CompanyNavSummary.fromJson(Map<String, dynamic>.from(navRaw));
    }

    CompanyWorkspace? workspace;
    final wsRaw = json['workspace'] ?? json['Workspace'];
    if (wsRaw is Map<String, dynamic>) {
      workspace = CompanyWorkspace.fromJson(wsRaw);
    } else if (wsRaw is Map) {
      workspace = CompanyWorkspace.fromJson(Map<String, dynamic>.from(wsRaw));
    }

    CompanyDashboardSummary? dashboard;
    final dashRaw = json['dashboard'] ?? json['Dashboard'];
    if (dashRaw is Map<String, dynamic>) {
      dashboard = CompanyDashboardSummary.fromJson(dashRaw);
    } else if (dashRaw is Map) {
      dashboard =
          CompanyDashboardSummary.fromJson(Map<String, dynamic>.from(dashRaw));
    }

    CompanyDataScope? dataScope;
    final scopeRaw = json['dataScope'] ?? json['DataScope'];
    if (scopeRaw is Map<String, dynamic>) {
      dataScope = CompanyDataScope.fromJson(scopeRaw);
    } else if (scopeRaw is Map) {
      dataScope =
          CompanyDataScope.fromJson(Map<String, dynamic>.from(scopeRaw));
    }

    CompanySecuritySummary? security;
    final secRaw = json['security'] ?? json['Security'];
    if (secRaw is Map<String, dynamic>) {
      security = CompanySecuritySummary.fromJson(secRaw);
    } else if (secRaw is Map) {
      security =
          CompanySecuritySummary.fromJson(Map<String, dynamic>.from(secRaw));
    }

    CompanyAuditSummary? audit;
    final auditRaw = json['audit'] ?? json['Audit'];
    if (auditRaw is Map<String, dynamic>) {
      audit = CompanyAuditSummary.fromJson(auditRaw);
    } else if (auditRaw is Map) {
      audit = CompanyAuditSummary.fromJson(Map<String, dynamic>.from(auditRaw));
    }

    final parsedFeatureKeys = featureKeys.isNotEmpty
        ? featureKeys
        : FleetSession._parseStringList(
            json['featureKeys'] ?? json['FeatureKeys'],
          );

    final hint = json['workspaceHint'] as String? ??
        json['WorkspaceHint'] as String? ??
        workspace?.key;

    return CompanyContext(
      companyId: json['companyId'] as int? ??
          json['CompanyId'] as int? ??
          json['tenantId'] as int? ??
          json['TenantId'] as int? ??
          0,
      tenantId: json['tenantId'] as int? ??
          json['TenantId'] as int? ??
          json['companyId'] as int? ??
          json['CompanyId'] as int? ??
          0,
      companyName: json['companyName'] as String? ??
          json['CompanyName'] as String? ??
          '',
      slug: json['slug'] as String? ?? json['Slug'] as String?,
      logoUrl: json['logoUrl'] as String? ?? json['LogoUrl'] as String?,
      primaryColor:
          json['primaryColor'] as String? ?? json['PrimaryColor'] as String?,
      branchId: json['branchId'] as int? ?? json['BranchId'] as int?,
      branchName: json['branchName'] as String? ?? json['BranchName'] as String?,
      departmentId:
          json['departmentId'] as int? ?? json['DepartmentId'] as int?,
      departmentName: json['departmentName'] as String? ??
          json['DepartmentName'] as String?,
      enabledModuleKeys: FleetSession._parseStringList(
        json['enabledModuleKeys'] ?? json['EnabledModuleKeys'],
      ),
      featureKeys: parsedFeatureKeys,
      features: features,
      modules: modules,
      workspaceHint: hint,
      workspace: workspace,
      roleCode: json['roleCode'] as String? ?? json['RoleCode'] as String?,
      subscription: subscription,
      currentUser: currentUser,
      assignedRoles: assignedRoles,
      effectivePermissions: effectivePermissions,
      navSummary: navSummary,
      dashboard: dashboard,
      dataScope: dataScope,
      security: security,
      audit: audit,
    );
  }

  Map<String, dynamic> toJson() => {
        'companyId': companyId,
        'tenantId': tenantId,
        'companyName': companyName,
        if (slug != null) 'slug': slug,
        if (logoUrl != null) 'logoUrl': logoUrl,
        if (primaryColor != null) 'primaryColor': primaryColor,
        if (branchId != null) 'branchId': branchId,
        if (branchName != null) 'branchName': branchName,
        if (departmentId != null) 'departmentId': departmentId,
        if (departmentName != null) 'departmentName': departmentName,
        'enabledModuleKeys': enabledModuleKeys,
        'featureKeys': featureKeys,
        'features': features.map((f) => f.toJson()).toList(),
        'modules': modules.map((m) => m.toJson()).toList(),
        if (workspaceHint != null) 'workspaceHint': workspaceHint,
        if (workspace != null) 'workspace': workspace!.toJson(),
        if (roleCode != null) 'roleCode': roleCode,
        if (subscription != null) 'subscription': subscription!.toJson(),
        if (currentUser != null) 'currentUser': currentUser!.toJson(),
        'assignedRoles': assignedRoles.map((r) => r.toJson()).toList(),
        'effectivePermissions':
            effectivePermissions.map((p) => p.toJson()).toList(),
        if (navSummary != null) 'navSummary': navSummary!.toJson(),
        if (dashboard != null) 'dashboard': dashboard!.toJson(),
        if (dataScope != null) 'dataScope': dataScope!.toJson(),
        if (security != null) 'security': security!.toJson(),
        if (audit != null) 'audit': audit!.toJson(),
      };
}

/// Safe audit summary from company context (Stage 14, read-only).
class CompanyAuditSummary {
  const CompanyAuditSummary({
    this.enabled = true,
    this.retentionDays = 90,
  });

  final bool enabled;
  final int retentionDays;

  factory CompanyAuditSummary.fromJson(Map<String, dynamic> json) {
    int? asInt(dynamic v) {
      if (v is int) return v;
      if (v == null) return null;
      return int.tryParse(v.toString());
    }

    return CompanyAuditSummary(
      enabled: json['enabled'] as bool? ?? json['Enabled'] as bool? ?? true,
      retentionDays:
          asInt(json['retentionDays'] ?? json['RetentionDays']) ?? 90,
    );
  }

  Map<String, dynamic> toJson() => {
        'enabled': enabled,
        'retentionDays': retentionDays,
      };
}

/// Safe security summary from company context (Stage 13, read-only).
class CompanySecuritySummary {
  const CompanySecuritySummary({
    this.passwordExpiryDays,
    this.idleTimeoutMinutes,
    this.absoluteTimeoutMinutes,
    this.auditLevel = 'Always',
    this.lockoutEnabled = false,
    this.mfaRequired = false,
    this.passwordExpired = false,
    this.passwordDaysRemaining,
  });

  final int? passwordExpiryDays;
  final int? idleTimeoutMinutes;
  final int? absoluteTimeoutMinutes;
  final String auditLevel;
  final bool lockoutEnabled;
  final bool mfaRequired;
  final bool passwordExpired;
  final int? passwordDaysRemaining;

  factory CompanySecuritySummary.fromJson(Map<String, dynamic> json) {
    int? asInt(dynamic v) {
      if (v is int) return v;
      if (v == null) return null;
      return int.tryParse(v.toString());
    }

    return CompanySecuritySummary(
      passwordExpiryDays:
          asInt(json['passwordExpiryDays'] ?? json['PasswordExpiryDays']),
      idleTimeoutMinutes:
          asInt(json['idleTimeoutMinutes'] ?? json['IdleTimeoutMinutes']),
      absoluteTimeoutMinutes: asInt(
          json['absoluteTimeoutMinutes'] ?? json['AbsoluteTimeoutMinutes']),
      auditLevel: json['auditLevel'] as String? ??
          json['AuditLevel'] as String? ??
          'Always',
      lockoutEnabled: json['lockoutEnabled'] as bool? ??
          json['LockoutEnabled'] as bool? ??
          false,
      mfaRequired:
          json['mfaRequired'] as bool? ?? json['MfaRequired'] as bool? ?? false,
      passwordExpired: json['passwordExpired'] as bool? ??
          json['PasswordExpired'] as bool? ??
          false,
      passwordDaysRemaining: asInt(
          json['passwordDaysRemaining'] ?? json['PasswordDaysRemaining']),
    );
  }

  Map<String, dynamic> toJson() => {
        if (passwordExpiryDays != null) 'passwordExpiryDays': passwordExpiryDays,
        if (idleTimeoutMinutes != null) 'idleTimeoutMinutes': idleTimeoutMinutes,
        if (absoluteTimeoutMinutes != null)
          'absoluteTimeoutMinutes': absoluteTimeoutMinutes,
        'auditLevel': auditLevel,
        'lockoutEnabled': lockoutEnabled,
        'mfaRequired': mfaRequired,
        'passwordExpired': passwordExpired,
        if (passwordDaysRemaining != null)
          'passwordDaysRemaining': passwordDaysRemaining,
      };
}

/// Effective data scope from company context (Stage 12, read-only).
class CompanyDataScope {
  const CompanyDataScope({
    this.mode = 'Company',
    this.isCompanyWide = true,
    this.branchIds = const [],
    this.departmentIds = const [],
    this.branchLabels = const [],
    this.departmentLabels = const [],
    this.source = 'default',
    this.homeBranchId,
    this.homeDepartmentId,
  });

  final String mode;
  final bool isCompanyWide;
  final List<int> branchIds;
  final List<int> departmentIds;
  final List<String> branchLabels;
  final List<String> departmentLabels;
  final String source;
  final int? homeBranchId;
  final int? homeDepartmentId;

  factory CompanyDataScope.fromJson(Map<String, dynamic> json) {
    List<int> parseIds(dynamic raw) {
      if (raw is! List) return const [];
      return raw
          .map((e) => e is int ? e : int.tryParse(e.toString()))
          .whereType<int>()
          .toList();
    }

    return CompanyDataScope(
      mode: json['mode'] as String? ?? json['Mode'] as String? ?? 'Company',
      isCompanyWide: json['isCompanyWide'] as bool? ??
          json['IsCompanyWide'] as bool? ??
          true,
      branchIds: parseIds(json['branchIds'] ?? json['BranchIds']),
      departmentIds: parseIds(json['departmentIds'] ?? json['DepartmentIds']),
      branchLabels: FleetSession._parseStringList(
        json['branchLabels'] ?? json['BranchLabels'],
      ),
      departmentLabels: FleetSession._parseStringList(
        json['departmentLabels'] ?? json['DepartmentLabels'],
      ),
      source: json['source'] as String? ?? json['Source'] as String? ?? 'default',
      homeBranchId:
          json['homeBranchId'] as int? ?? json['HomeBranchId'] as int?,
      homeDepartmentId:
          json['homeDepartmentId'] as int? ?? json['HomeDepartmentId'] as int?,
    );
  }

  Map<String, dynamic> toJson() => {
        'mode': mode,
        'isCompanyWide': isCompanyWide,
        'branchIds': branchIds,
        'departmentIds': departmentIds,
        'branchLabels': branchLabels,
        'departmentLabels': departmentLabels,
        'source': source,
        if (homeBranchId != null) 'homeBranchId': homeBranchId,
        if (homeDepartmentId != null) 'homeDepartmentId': homeDepartmentId,
      };
}

/// Resolved dashboard summary from company context (Stage 11, read-only).
class CompanyDashboardSummary {
  const CompanyDashboardSummary({
    this.key,
    this.displayName,
    this.widgetKeys = const [],
    this.source = 'default',
  });

  final String? key;
  final String? displayName;
  final List<String> widgetKeys;
  final String source;

  factory CompanyDashboardSummary.fromJson(Map<String, dynamic> json) {
    return CompanyDashboardSummary(
      key: json['key'] as String? ?? json['Key'] as String?,
      displayName:
          json['displayName'] as String? ?? json['DisplayName'] as String?,
      widgetKeys: FleetSession._parseStringList(
        json['widgetKeys'] ?? json['WidgetKeys'],
      ),
      source: json['source'] as String? ??
          json['Source'] as String? ??
          'default',
    );
  }

  Map<String, dynamic> toJson() => {
        if (key != null) 'key': key,
        if (displayName != null) 'displayName': displayName,
        'widgetKeys': widgetKeys,
        'source': source,
      };
}

/// Resolved landing workspace from company context (Stage 10, read-only).
class CompanyWorkspace {
  const CompanyWorkspace({
    required this.key,
    required this.displayName,
    required this.homeRoute,
    this.icon,
    this.defaultDashboardKey,
    this.source,
    this.moduleKeys = const [],
    this.isMobileSupported = false,
  });

  final String key;
  final String displayName;
  final String homeRoute;
  final String? icon;
  final String? defaultDashboardKey;
  final String? source;
  final List<String> moduleKeys;
  final bool isMobileSupported;

  factory CompanyWorkspace.fromJson(Map<String, dynamic> json) =>
      CompanyWorkspace(
        key: json['key'] as String? ?? json['Key'] as String? ?? '',
        displayName: json['displayName'] as String? ??
            json['DisplayName'] as String? ??
            json['key'] as String? ??
            json['Key'] as String? ??
            '',
        homeRoute: json['homeRoute'] as String? ??
            json['HomeRoute'] as String? ??
            '/home',
        icon: json['icon'] as String? ?? json['Icon'] as String?,
        defaultDashboardKey: json['defaultDashboardKey'] as String? ??
            json['DefaultDashboardKey'] as String?,
        source: json['source'] as String? ?? json['Source'] as String?,
        moduleKeys: FleetSession._parseStringList(
          json['moduleKeys'] ?? json['ModuleKeys'],
        ),
        isMobileSupported: json['isMobileSupported'] as bool? ??
            json['IsMobileSupported'] as bool? ??
            false,
      );

  Map<String, dynamic> toJson() => {
        'key': key,
        'displayName': displayName,
        'homeRoute': homeRoute,
        if (icon != null) 'icon': icon,
        if (defaultDashboardKey != null)
          'defaultDashboardKey': defaultDashboardKey,
        if (source != null) 'source': source,
        'moduleKeys': moduleKeys,
        'isMobileSupported': isMobileSupported,
      };
}

class CompanyNavSummary {
  const CompanyNavSummary({
    this.moduleCount = 0,
    this.itemCount = 0,
    this.topModuleLabels = const [],
    this.mobileItemLabels = const [],
  });

  final int moduleCount;
  final int itemCount;
  final List<String> topModuleLabels;
  final List<String> mobileItemLabels;

  factory CompanyNavSummary.fromJson(Map<String, dynamic> json) =>
      CompanyNavSummary(
        moduleCount: json['moduleCount'] as int? ??
            json['ModuleCount'] as int? ??
            0,
        itemCount:
            json['itemCount'] as int? ?? json['ItemCount'] as int? ?? 0,
        topModuleLabels: FleetSession._parseStringList(
          json['topModuleLabels'] ?? json['TopModuleLabels'],
        ),
        mobileItemLabels: FleetSession._parseStringList(
          json['mobileItemLabels'] ?? json['MobileItemLabels'],
        ),
      );

  Map<String, dynamic> toJson() => {
        'moduleCount': moduleCount,
        'itemCount': itemCount,
        'topModuleLabels': topModuleLabels,
        'mobileItemLabels': mobileItemLabels,
      };
}

/// Effective permission summary from company context (Stage 8, read-only).
class EffectivePermission {
  const EffectivePermission({
    required this.code,
    required this.displayName,
    this.category,
    this.moduleKey,
    this.action,
  });

  final String code;
  final String displayName;
  final String? category;
  final String? moduleKey;
  final String? action;

  factory EffectivePermission.fromJson(Map<String, dynamic> json) {
    final code = json['code'] as String? ?? json['Code'] as String? ?? '';
    final display = json['displayName'] as String? ??
        json['DisplayName'] as String? ??
        code;
    return EffectivePermission(
      code: code,
      displayName: display.isNotEmpty ? display : code,
      category: json['category'] as String? ?? json['Category'] as String?,
      moduleKey: json['moduleKey'] as String? ?? json['ModuleKey'] as String?,
      action: json['action'] as String? ?? json['Action'] as String?,
    );
  }

  Map<String, dynamic> toJson() => {
        'code': code,
        'displayName': displayName,
        if (category != null) 'category': category,
        if (moduleKey != null) 'moduleKey': moduleKey,
        if (action != null) 'action': action,
      };
}

/// Assigned platform role from company context (Stage 7, read-only).
class AssignedRole {
  const AssignedRole({
    required this.roleId,
    required this.code,
    required this.name,
    required this.displayName,
    this.category,
    this.roleType,
  });

  final int roleId;
  final String code;
  final String name;
  final String displayName;
  final String? category;
  final String? roleType;

  factory AssignedRole.fromJson(Map<String, dynamic> json) {
    final name = json['name'] as String? ?? json['Name'] as String? ?? '';
    final display = json['displayName'] as String? ??
        json['DisplayName'] as String? ??
        name;
    return AssignedRole(
      roleId: json['roleId'] as int? ?? json['RoleId'] as int? ?? 0,
      code: json['code'] as String? ?? json['Code'] as String? ?? '',
      name: name,
      displayName: display.isNotEmpty ? display : name,
      category: json['category'] as String? ?? json['Category'] as String?,
      roleType: json['roleType'] as String? ?? json['RoleType'] as String?,
    );
  }

  Map<String, dynamic> toJson() => {
        'roleId': roleId,
        'code': code,
        'name': name,
        'displayName': displayName,
        if (category != null) 'category': category,
        if (roleType != null) 'roleType': roleType,
      };
}

/// Current-user profile slice from company context (Stage 6).
class CompanyCurrentUser {
  const CompanyCurrentUser({
    this.jobTitle,
    this.employeeType,
    this.status,
    this.defaultWorkspaceKey,
    this.defaultDashboardKey,
    this.homeRoute,
    this.language,
    this.theme,
    this.avatarUrl,
    this.employeeCode,
  });

  final String? jobTitle;
  final String? employeeType;
  final String? status;
  final String? defaultWorkspaceKey;
  final String? defaultDashboardKey;
  final String? homeRoute;
  final String? language;
  final String? theme;
  final String? avatarUrl;
  final String? employeeCode;

  factory CompanyCurrentUser.fromJson(Map<String, dynamic> json) {
    return CompanyCurrentUser(
      jobTitle: json['jobTitle'] as String? ?? json['JobTitle'] as String?,
      employeeType:
          json['employeeType'] as String? ?? json['EmployeeType'] as String?,
      status: json['status'] as String? ?? json['Status'] as String?,
      defaultWorkspaceKey: json['defaultWorkspaceKey'] as String? ??
          json['DefaultWorkspaceKey'] as String?,
      defaultDashboardKey: json['defaultDashboardKey'] as String? ??
          json['DefaultDashboardKey'] as String?,
      homeRoute: json['homeRoute'] as String? ?? json['HomeRoute'] as String?,
      language: json['language'] as String? ?? json['Language'] as String?,
      theme: json['theme'] as String? ?? json['Theme'] as String?,
      avatarUrl: json['avatarUrl'] as String? ?? json['AvatarUrl'] as String?,
      employeeCode:
          json['employeeCode'] as String? ?? json['EmployeeCode'] as String?,
    );
  }

  Map<String, dynamic> toJson() => {
        if (jobTitle != null) 'jobTitle': jobTitle,
        if (employeeType != null) 'employeeType': employeeType,
        if (status != null) 'status': status,
        if (defaultWorkspaceKey != null)
          'defaultWorkspaceKey': defaultWorkspaceKey,
        if (defaultDashboardKey != null)
          'defaultDashboardKey': defaultDashboardKey,
        if (homeRoute != null) 'homeRoute': homeRoute,
        if (language != null) 'language': language,
        if (theme != null) 'theme': theme,
        if (avatarUrl != null) 'avatarUrl': avatarUrl,
        if (employeeCode != null) 'employeeCode': employeeCode,
      };
}

/// Enabled company feature (read-only registry metadata).
class CompanyFeature {
  const CompanyFeature({
    required this.featureKey,
    required this.moduleKey,
    required this.name,
    this.displayName = '',
    this.description,
    this.category,
    this.icon,
    this.status,
    this.isEnabled = true,
    this.sortOrder = 0,
    this.isMobileSupported = false,
    this.isAISupported = false,
    this.isGPSSupported = false,
  });

  final String featureKey;
  final String moduleKey;
  final String name;
  final String displayName;
  final String? description;
  final String? category;
  final String? icon;
  final String? status;
  final bool isEnabled;
  final int sortOrder;
  final bool isMobileSupported;
  final bool isAISupported;
  final bool isGPSSupported;

  factory CompanyFeature.fromJson(Map<String, dynamic> json) {
    return CompanyFeature(
      featureKey: json['featureKey'] as String? ??
          json['FeatureKey'] as String? ??
          '',
      moduleKey: json['moduleKey'] as String? ??
          json['ModuleKey'] as String? ??
          json['moduleCode'] as String? ??
          json['ModuleCode'] as String? ??
          '',
      name: json['name'] as String? ?? json['Name'] as String? ?? '',
      displayName: json['displayName'] as String? ??
          json['DisplayName'] as String? ??
          json['name'] as String? ??
          json['Name'] as String? ??
          '',
      description:
          json['description'] as String? ?? json['Description'] as String?,
      category: json['category'] as String? ?? json['Category'] as String?,
      icon: json['icon'] as String? ?? json['Icon'] as String?,
      status: json['status'] as String? ?? json['Status'] as String?,
      isEnabled: json['isEnabled'] as bool? ??
          json['IsEnabled'] as bool? ??
          true,
      sortOrder: json['sortOrder'] as int? ?? json['SortOrder'] as int? ?? 0,
      isMobileSupported: json['isMobileSupported'] as bool? ??
          json['IsMobileSupported'] as bool? ??
          false,
      isAISupported:
          json['isAISupported'] as bool? ?? json['IsAISupported'] as bool? ?? false,
      isGPSSupported: json['isGPSSupported'] as bool? ??
          json['IsGPSSupported'] as bool? ??
          false,
    );
  }

  Map<String, dynamic> toJson() => {
        'featureKey': featureKey,
        'moduleKey': moduleKey,
        'name': name,
        'displayName': displayName,
        if (description != null) 'description': description,
        if (category != null) 'category': category,
        if (icon != null) 'icon': icon,
        if (status != null) 'status': status,
        'isEnabled': isEnabled,
        'sortOrder': sortOrder,
        'isMobileSupported': isMobileSupported,
        'isAISupported': isAISupported,
        'isGPSSupported': isGPSSupported,
      };
}

/// Read-only subscription / license summary from company context.
class CompanySubscription {
  const CompanySubscription({
    this.subscriptionCode,
    this.planName,
    this.planDisplayName,
    this.status = 'Unknown',
    this.startDate,
    this.endDate,
    this.autoRenew = false,
    this.licensedModuleCodes = const [],
    this.maxUsers,
    this.maxDrivers,
    this.maxVehicles,
    this.maxBranches,
    this.maxGpsDevices,
    this.storageQuotaGb,
    this.aiCredits,
    this.gpsEnabled = true,
    this.usedUsers = 0,
    this.usedDrivers = 0,
    this.usedVehicles = 0,
  });

  final String? subscriptionCode;
  final String? planName;
  final String? planDisplayName;
  final String status;
  final DateTime? startDate;
  final DateTime? endDate;
  final bool autoRenew;
  final List<String> licensedModuleCodes;
  final int? maxUsers;
  final int? maxDrivers;
  final int? maxVehicles;
  final int? maxBranches;
  final int? maxGpsDevices;
  final int? storageQuotaGb;
  final int? aiCredits;
  final bool gpsEnabled;
  final int usedUsers;
  final int usedDrivers;
  final int usedVehicles;

  String get displayPlanName =>
      (planDisplayName != null && planDisplayName!.isNotEmpty)
          ? planDisplayName!
          : (planName ?? subscriptionCode ?? '—');

  factory CompanySubscription.fromJson(Map<String, dynamic> json) {
    DateTime? parseDate(dynamic v) {
      if (v == null) return null;
      return DateTime.tryParse(v.toString());
    }

    int? parseInt(dynamic v) {
      if (v == null) return null;
      if (v is int) return v;
      return int.tryParse(v.toString());
    }

    return CompanySubscription(
      subscriptionCode: json['subscriptionCode'] as String? ??
          json['SubscriptionCode'] as String?,
      planName: json['planName'] as String? ?? json['PlanName'] as String?,
      planDisplayName: json['planDisplayName'] as String? ??
          json['PlanDisplayName'] as String?,
      status: json['status'] as String? ?? json['Status'] as String? ?? 'Unknown',
      startDate: parseDate(json['startDate'] ?? json['StartDate']),
      endDate: parseDate(json['endDate'] ?? json['EndDate']),
      autoRenew: json['autoRenew'] as bool? ??
          json['AutoRenew'] as bool? ??
          false,
      licensedModuleCodes: FleetSession._parseStringList(
        json['licensedModuleCodes'] ?? json['LicensedModuleCodes'],
      ),
      maxUsers: parseInt(json['maxUsers'] ?? json['MaxUsers']),
      maxDrivers: parseInt(json['maxDrivers'] ?? json['MaxDrivers']),
      maxVehicles: parseInt(json['maxVehicles'] ?? json['MaxVehicles']),
      maxBranches: parseInt(json['maxBranches'] ?? json['MaxBranches']),
      maxGpsDevices: parseInt(json['maxGpsDevices'] ?? json['MaxGpsDevices']),
      storageQuotaGb:
          parseInt(json['storageQuotaGb'] ?? json['StorageQuotaGb']),
      aiCredits: parseInt(json['aiCredits'] ?? json['AICredits']),
      gpsEnabled:
          json['gpsEnabled'] as bool? ?? json['GPSEnabled'] as bool? ?? true,
      usedUsers: parseInt(json['usedUsers'] ?? json['UsedUsers']) ?? 0,
      usedDrivers: parseInt(json['usedDrivers'] ?? json['UsedDrivers']) ?? 0,
      usedVehicles: parseInt(json['usedVehicles'] ?? json['UsedVehicles']) ?? 0,
    );
  }

  Map<String, dynamic> toJson() => {
        if (subscriptionCode != null) 'subscriptionCode': subscriptionCode,
        if (planName != null) 'planName': planName,
        if (planDisplayName != null) 'planDisplayName': planDisplayName,
        'status': status,
        if (startDate != null) 'startDate': startDate!.toIso8601String(),
        if (endDate != null) 'endDate': endDate!.toIso8601String(),
        'autoRenew': autoRenew,
        'licensedModuleCodes': licensedModuleCodes,
        if (maxUsers != null) 'maxUsers': maxUsers,
        if (maxDrivers != null) 'maxDrivers': maxDrivers,
        if (maxVehicles != null) 'maxVehicles': maxVehicles,
        if (maxBranches != null) 'maxBranches': maxBranches,
        if (maxGpsDevices != null) 'maxGpsDevices': maxGpsDevices,
        if (storageQuotaGb != null) 'storageQuotaGb': storageQuotaGb,
        if (aiCredits != null) 'aiCredits': aiCredits,
        'gpsEnabled': gpsEnabled,
        'usedUsers': usedUsers,
        'usedDrivers': usedDrivers,
        'usedVehicles': usedVehicles,
      };
}

/// Installed company module (read-only registry metadata).
class CompanyModule {
  const CompanyModule({
    required this.moduleCode,
    required this.name,
    this.displayName = '',
    this.description,
    this.category,
    this.version,
    this.icon,
    this.status,
    this.isMobileSupported = false,
    this.isAISupported = false,
    this.isGPSSupported = false,
  });

  final String moduleCode;
  final String name;
  final String displayName;
  final String? description;
  final String? category;
  final String? version;
  final String? icon;
  final String? status;
  final bool isMobileSupported;
  final bool isAISupported;
  final bool isGPSSupported;

  factory CompanyModule.fromJson(Map<String, dynamic> json) {
    return CompanyModule(
      moduleCode: json['moduleCode'] as String? ??
          json['ModuleCode'] as String? ??
          json['code'] as String? ??
          '',
      name: json['name'] as String? ?? json['Name'] as String? ?? '',
      displayName: json['displayName'] as String? ??
          json['DisplayName'] as String? ??
          json['name'] as String? ??
          json['Name'] as String? ??
          '',
      description:
          json['description'] as String? ?? json['Description'] as String?,
      category: json['category'] as String? ?? json['Category'] as String?,
      version: json['version'] as String? ?? json['Version'] as String?,
      icon: json['icon'] as String? ?? json['Icon'] as String?,
      status: json['status'] as String? ?? json['Status'] as String?,
      isMobileSupported: json['isMobileSupported'] as bool? ??
          json['IsMobileSupported'] as bool? ??
          false,
      isAISupported:
          json['isAISupported'] as bool? ?? json['IsAISupported'] as bool? ?? false,
      isGPSSupported: json['isGPSSupported'] as bool? ??
          json['IsGPSSupported'] as bool? ??
          false,
    );
  }

  Map<String, dynamic> toJson() => {
        'moduleCode': moduleCode,
        'name': name,
        'displayName': displayName,
        if (description != null) 'description': description,
        if (category != null) 'category': category,
        if (version != null) 'version': version,
        if (icon != null) 'icon': icon,
        if (status != null) 'status': status,
        'isMobileSupported': isMobileSupported,
        'isAISupported': isAISupported,
        'isGPSSupported': isGPSSupported,
      };
}

/// @deprecated Use [FleetSession]. Kept for incremental migration in tests.
typedef DriverSession = FleetSession;
