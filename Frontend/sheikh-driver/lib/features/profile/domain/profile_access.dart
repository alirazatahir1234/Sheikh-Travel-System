import '../../auth/domain/auth_models.dart';

/// Permission / role gates for Profile surfaces.
/// Backend JWT permissions remain the source of truth; this only hides UI.
abstract final class ProfileAccess {
  static bool canSeeProfile(FleetSession? session) => session != null;

  /// Edit / personal information — authenticated self-service only.
  static bool canSeePersonalInformation(FleetSession? session) =>
      session != null;

  static bool canSeeAppSettings(FleetSession? session) => session != null;

  static bool canSeeSecurityPrivacy(FleetSession? session) => session != null;

  static bool canSeeNotifications(FleetSession? session) => session != null;

  static bool canSeeLanguageAppearance(FleetSession? session) =>
      session != null;

  /// Active organization card — only when company context is hydrated.
  static bool canSeeActiveOrganization(FleetSession? session) {
    final ctx = session?.companyContext;
    if (ctx == null) return false;
    return ctx.companyName.trim().isNotEmpty || ctx.companyId > 0;
  }

  /// No mobile org-switch API yet — never advertise cross-tenant switch.
  static bool canSwitchOrganization(FleetSession? session) => false;

  /// Assigned vehicle block — drivers (or staff with a linked driver id)
  /// when profile API returns vehicle fields.
  static bool canSeeAssignedVehicle(FleetSession? session) {
    if (session == null) return false;
    return session.isDriverSession || session.driverId != null;
  }

  /// Driver operational shortcuts (fuel / earnings / docs) — driver sessions only.
  /// Staff shells use More / Fleet hubs instead of Profile ops links.
  static bool canSeeDriverOperations(FleetSession? session) {
    if (session == null) return false;
    return session.isDriverSession;
  }

  static bool canSeeFuelHistory(FleetSession? session) {
    if (session == null) return false;
    return session.isDriverSession ||
        session.hasPermission(FleetPermissions.fuelView);
  }

  static bool canSeeEarnings(FleetSession? session) {
    if (session == null) return false;
    return session.isDriverSession || session.canSeeOwnerEarnings;
  }

  static bool canSeeDocuments(FleetSession? session) {
    if (session == null) return false;
    return session.isDriverSession ||
        session.hasPermission(FleetPermissions.driverView);
  }

  static bool canSeeInspection(FleetSession? session) {
    if (session == null) return false;
    return session.isDriverSession ||
        session.hasPermission(FleetPermissions.vehicleView);
  }

  /// Platform roles (Category Platform / SUPER_ADMIN / TENANT_ADMIN).
  static List<AssignedRole> platformRoles(CompanyContext? ctx) {
    if (ctx == null || ctx.assignedRoles.isEmpty) return const [];
    return ctx.assignedRoles.where(_isPlatformRole).toList();
  }

  /// Organization / operational roles (non-platform).
  static List<AssignedRole> organizationRoles(CompanyContext? ctx) {
    if (ctx == null || ctx.assignedRoles.isEmpty) return const [];
    return ctx.assignedRoles.where((r) => !_isPlatformRole(r)).toList();
  }

  static bool _isPlatformRole(AssignedRole role) {
    final cat = (role.category ?? '').trim().toLowerCase();
    if (cat == 'platform') return true;
    final code = role.code.trim().toUpperCase();
    return code == FleetRole.superAdmin || code == FleetRole.tenantAdmin;
  }

  /// Display label for platform role from assigned roles or session codes.
  static String? platformRoleLabel(FleetSession? session) {
    if (session == null) return null;
    final fromAssigned = platformRoles(session.companyContext)
        .map(_roleDisplay)
        .where((s) => s.isNotEmpty)
        .toList();
    if (fromAssigned.isNotEmpty) return fromAssigned.join(', ');

    if (session.hasRole(FleetRole.superAdmin)) return 'Super Admin';
    if (session.hasRole(FleetRole.tenantAdmin)) return 'Company Admin';
    return null;
  }

  /// Display label for organization role (never merges platform codes).
  static String? organizationRoleLabel(FleetSession? session) {
    if (session == null) return null;
    final fromAssigned = organizationRoles(session.companyContext)
        .map(_roleDisplay)
        .where((s) => s.isNotEmpty)
        .toList();
    if (fromAssigned.isNotEmpty) return fromAssigned.join(', ');

    // Fall back to highest non-platform nav role when assignedRoles empty.
    final code = session.primaryNavRole.toUpperCase();
    if (code == FleetRole.superAdmin || code == FleetRole.tenantAdmin) {
      return null;
    }
    return _prettyCode(code);
  }

  static String _roleDisplay(AssignedRole r) {
    if (r.displayName.trim().isNotEmpty) return r.displayName.trim();
    if (r.name.trim().isNotEmpty) return r.name.trim();
    return _prettyCode(r.code);
  }

  static String _prettyCode(String raw) {
    final cleaned = raw
        .replaceAll('_', ' ')
        .replaceAllMapped(
          RegExp(r'([a-z])([A-Z])'),
          (m) => '${m[1]} ${m[2]}',
        )
        .trim();
    if (cleaned.isEmpty) return '';
    return cleaned
        .split(RegExp(r'\s+'))
        .map((w) => w.isEmpty
            ? w
            : '${w[0].toUpperCase()}${w.substring(1).toLowerCase()}')
        .join(' ');
  }
}
