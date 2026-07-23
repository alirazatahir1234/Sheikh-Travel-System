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
    this.roleCode,
    this.subscription,
    this.currentUser,
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
  final String? roleCode;
  final CompanySubscription? subscription;
  final CompanyCurrentUser? currentUser;

  String? get jobTitle => currentUser?.jobTitle;
  String? get employeeType => currentUser?.employeeType;
  String? get theme => currentUser?.theme;
  String? get language => currentUser?.language;
  String? get effectiveWorkspace =>
      currentUser?.defaultWorkspaceKey ?? workspaceHint;

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

    final parsedFeatureKeys = featureKeys.isNotEmpty
        ? featureKeys
        : FleetSession._parseStringList(
            json['featureKeys'] ?? json['FeatureKeys'],
          );

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
      workspaceHint:
          json['workspaceHint'] as String? ?? json['WorkspaceHint'] as String?,
      roleCode: json['roleCode'] as String? ?? json['RoleCode'] as String?,
      subscription: subscription,
      currentUser: currentUser,
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
        if (roleCode != null) 'roleCode': roleCode,
        if (subscription != null) 'subscription': subscription!.toJson(),
        if (currentUser != null) 'currentUser': currentUser!.toJson(),
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
