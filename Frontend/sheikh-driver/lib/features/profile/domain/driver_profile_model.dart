import '../../auth/domain/auth_models.dart';

class DriverProfile {
  const DriverProfile({
    required this.id,
    required this.fullName,
    required this.phone,
    this.email,
    this.photoUrl,
    required this.driverCode,
    required this.licenseNumber,
    this.licenseExpiryDate,
    required this.status,
    required this.statusName,
    required this.isActive,
    this.currentVehicleName,
    this.currentVehiclePlate,
    this.branchName,
    this.rating,
    this.yearsExperience,
    this.verificationStatus,
    this.isStaffProfile = false,
    this.roleLabel,
    this.lastLoginAt,
  });

  final int id;
  final String fullName;
  final String phone;
  final String? email;
  final String? photoUrl;
  final String driverCode;
  final String licenseNumber;
  final DateTime? licenseExpiryDate;
  final int status;
  final String statusName;
  final bool isActive;
  final String? currentVehicleName;
  final String? currentVehiclePlate;
  final String? branchName;
  final double? rating;
  final int? yearsExperience;
  final String? verificationStatus;
  final bool isStaffProfile;
  final String? roleLabel;

  /// From auth `/auth/me` when present — never invented client-side.
  final DateTime? lastLoginAt;

  bool get isLicenseExpiringSoon {
    if (licenseExpiryDate == null) return false;
    return licenseExpiryDate!.difference(DateTime.now()).inDays <= 30;
  }

  factory DriverProfile.fromJson(Map<String, dynamic> json) {
    return DriverProfile(
      id: json['id'] as int? ?? 0,
      fullName: json['fullName'] as String? ?? '',
      phone: json['phone'] as String? ?? '',
      email: json['email'] as String?,
      photoUrl: json['photoUrl'] as String?,
      driverCode: json['driverCode'] as String? ?? '',
      licenseNumber: json['licenseNumber'] as String? ?? '',
      licenseExpiryDate: json['licenseExpiryDate'] != null
          ? DateTime.tryParse(json['licenseExpiryDate'].toString())
          : null,
      status: json['status'] as int? ?? 1,
      statusName: json['statusName'] as String? ?? 'Available',
      isActive: json['isActive'] as bool? ?? true,
      currentVehicleName: json['currentVehicleName'] as String?,
      currentVehiclePlate: json['currentVehiclePlate'] as String?,
      branchName: json['branchName'] as String?,
      rating: (json['rating'] as num?)?.toDouble(),
      yearsExperience: json['yearsExperience'] as int?,
      verificationStatus: json['verificationStatus'] as String?,
      lastLoginAt: _parseDate(json['lastLoginAt'] ?? json['LastLoginAt']),
    );
  }

  factory DriverProfile.fromStaffUserJson(
    Map<String, dynamic> json,
    FleetSession session,
  ) {
    final roleRaw = json['role'];
    String? roleFromApi;
    if (roleRaw is String && roleRaw.trim().isNotEmpty) {
      roleFromApi = roleRaw;
    } else if (roleRaw is num) {
      roleFromApi = switch (roleRaw.toInt()) {
        1 => 'Admin',
        2 => 'Dispatcher',
        3 => 'Driver',
        4 => 'Accountant',
        _ => null,
      };
    }
    final roleLabel = _prettyRole(
      roleFromApi ??
          (session.roles.isNotEmpty ? session.primaryNavRole : 'Staff'),
    );
    final statusRaw = (json['status'] as String?)?.trim();
    final active = json['isActive'] as bool? ??
        (statusRaw == null || statusRaw.isEmpty
            ? true
            : statusRaw.toLowerCase() == 'active');
    final statusName = (statusRaw != null && statusRaw.isNotEmpty)
        ? statusRaw
        : (active ? 'Active' : 'Inactive');

    return DriverProfile(
      id: json['id'] as int? ?? session.userId,
      fullName: json['fullName'] as String? ?? session.displayName,
      phone: (json['phone'] as String?)?.trim().isNotEmpty == true
          ? json['phone'] as String
          : (session.phone ?? ''),
      email: json['email'] as String? ?? session.email,
      photoUrl: json['avatarUrl'] as String? ??
          json['AvatarUrl'] as String? ??
          json['photoUrl'] as String?,
      driverCode: 'U${json['id'] as int? ?? session.userId}',
      licenseNumber: '—',
      status: active ? 1 : 0,
      statusName: statusName,
      isActive: active,
      isStaffProfile: true,
      roleLabel: roleLabel,
      lastLoginAt: _parseDate(json['lastLoginAt'] ?? json['LastLoginAt']),
    );
  }

  factory DriverProfile.fromStaffSession(FleetSession session) {
    final roleLabel = _prettyRole(
      session.roles.isNotEmpty ? session.primaryNavRole : 'Staff',
    );
    final userStatus = session.companyContext?.currentUser?.status?.trim();
    final active = userStatus == null ||
        userStatus.isEmpty ||
        userStatus.toLowerCase() == 'active';
    return DriverProfile(
      id: session.userId,
      fullName: session.displayName,
      phone: session.phone ?? '',
      email: session.email,
      photoUrl: session.companyContext?.currentUser?.avatarUrl,
      driverCode: 'U${session.userId}',
      licenseNumber: '—',
      status: active ? 1 : 0,
      statusName: (userStatus != null && userStatus.isNotEmpty)
          ? userStatus
          : 'Active',
      isActive: active,
      isStaffProfile: true,
      roleLabel: roleLabel,
    );
  }

  static DateTime? _parseDate(Object? raw) {
    if (raw == null) return null;
    return DateTime.tryParse(raw.toString());
  }

  static String _prettyRole(String raw) {
    final cleaned = raw
        .replaceAll('_', ' ')
        .replaceAllMapped(
          RegExp(r'([a-z])([A-Z])'),
          (m) => '${m[1]} ${m[2]}',
        )
        .trim();
    if (cleaned.isEmpty) return 'Staff';
    return cleaned
        .split(RegExp(r'\s+'))
        .map((w) => w.isEmpty
            ? w
            : '${w[0].toUpperCase()}${w.substring(1).toLowerCase()}')
        .join(' ');
  }
}
