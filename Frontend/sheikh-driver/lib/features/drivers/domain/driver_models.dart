class DriverListItem {
  const DriverListItem({
    required this.id,
    required this.fullName,
    required this.phone,
    required this.licenseNumber,
    required this.status,
    required this.isActive,
    this.driverCode,
    this.licenseExpiryDate,
    this.licenseExpired = false,
    this.licenseExpiringSoon = false,
    this.verificationStatus,
    this.branchName,
    this.assignedVehicleId,
    this.assignedVehicleRegistration,
    this.assignedVehicleName,
    this.rating,
    this.gpsOnline = false,
    this.availabilityBucket,
    this.photoUrl,
  });

  final int id;
  final String fullName;
  final String phone;
  final String licenseNumber;
  final String status;
  final bool isActive;
  final String? driverCode;
  final DateTime? licenseExpiryDate;
  final bool licenseExpired;
  final bool licenseExpiringSoon;
  final String? verificationStatus;
  final String? branchName;
  final int? assignedVehicleId;
  final String? assignedVehicleRegistration;
  final String? assignedVehicleName;
  final double? rating;
  final bool gpsOnline;
  final String? availabilityBucket;
  final String? photoUrl;

  factory DriverListItem.fromJson(Map<String, dynamic> json) {
    return DriverListItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      fullName: resolveDriverDisplayName(json),
      phone: json['phone'] as String? ?? json['Phone'] as String? ?? '',
      licenseNumber: json['licenseNumber'] as String? ??
          json['LicenseNumber'] as String? ??
          '',
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      isActive: json['isActive'] as bool? ?? json['IsActive'] as bool? ?? true,
      driverCode:
          json['driverCode'] as String? ?? json['DriverCode'] as String?,
      licenseExpiryDate:
          _date(json['licenseExpiryDate'] ?? json['LicenseExpiryDate']),
      licenseExpired: json['licenseExpired'] as bool? ??
          json['LicenseExpired'] as bool? ??
          false,
      licenseExpiringSoon: json['licenseExpiringSoon'] as bool? ??
          json['LicenseExpiringSoon'] as bool? ??
          false,
      verificationStatus: json['verificationStatus'] as String? ??
          json['VerificationStatus'] as String?,
      branchName:
          json['branchName'] as String? ?? json['BranchName'] as String?,
      assignedVehicleId: json['assignedVehicleId'] as int? ??
          json['AssignedVehicleId'] as int?,
      assignedVehicleRegistration:
          json['assignedVehicleRegistration'] as String? ??
              json['AssignedVehicleRegistration'] as String?,
      assignedVehicleName: json['assignedVehicleName'] as String? ??
          json['AssignedVehicleName'] as String?,
      rating: (json['rating'] as num? ?? json['Rating'] as num?)?.toDouble(),
      gpsOnline:
          json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
      availabilityBucket: json['availabilityBucket'] as String? ??
          json['AvailabilityBucket'] as String?,
      photoUrl: json['photoUrl'] as String? ?? json['PhotoUrl'] as String?,
    );
  }
}

class DriverDetail {
  const DriverDetail({
    required this.id,
    required this.fullName,
    required this.phone,
    required this.licenseNumber,
    required this.status,
    required this.isActive,
    this.driverCode,
    this.firstName,
    this.lastName,
    this.email,
    this.cnic,
    this.address,
    this.nationality,
    this.gender,
    this.dateOfBirth,
    this.licenseExpiryDate,
    this.licenseExpired = false,
    this.licenseExpiringSoon = false,
    this.verificationStatus,
    this.branchId,
    this.branchName,
    this.assignedVehicleId,
    this.assignedVehicleRegistration,
    this.assignedVehicleName,
    this.rating,
    this.gpsOnline = false,
    this.availabilityBucket,
    this.yearsExperience,
    this.hireDate,
    this.emergencyContactName,
    this.emergencyContact,
    this.photoUrl,
  });

  final int id;
  final String fullName;
  final String phone;
  final String licenseNumber;
  final String status;
  final bool isActive;
  final String? driverCode;
  final String? firstName;
  final String? lastName;
  final String? email;
  final String? cnic;
  final String? address;
  final String? nationality;
  final String? gender;
  final DateTime? dateOfBirth;
  final DateTime? licenseExpiryDate;
  final bool licenseExpired;
  final bool licenseExpiringSoon;
  final String? verificationStatus;
  final int? branchId;
  final String? branchName;
  final int? assignedVehicleId;
  final String? assignedVehicleRegistration;
  final String? assignedVehicleName;
  final double? rating;
  final bool gpsOnline;
  final String? availabilityBucket;
  final int? yearsExperience;
  final DateTime? hireDate;
  final String? emergencyContactName;
  final String? emergencyContact;
  final String? photoUrl;

  factory DriverDetail.fromJson(Map<String, dynamic> json) {
    return DriverDetail(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      fullName: resolveDriverDisplayName(json),
      phone: json['phone'] as String? ?? json['Phone'] as String? ?? '',
      licenseNumber: json['licenseNumber'] as String? ??
          json['LicenseNumber'] as String? ??
          '',
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      isActive: json['isActive'] as bool? ?? json['IsActive'] as bool? ?? true,
      driverCode:
          json['driverCode'] as String? ?? json['DriverCode'] as String?,
      firstName: json['firstName'] as String? ?? json['FirstName'] as String?,
      lastName: json['lastName'] as String? ?? json['LastName'] as String?,
      email: json['email'] as String? ?? json['Email'] as String?,
      cnic: json['cnic'] as String? ?? json['CNIC'] as String?,
      address: json['address'] as String? ?? json['Address'] as String?,
      nationality:
          json['nationality'] as String? ?? json['Nationality'] as String?,
      gender: json['gender'] as String? ?? json['Gender'] as String?,
      dateOfBirth: _date(json['dateOfBirth'] ?? json['DateOfBirth']),
      licenseExpiryDate:
          _date(json['licenseExpiryDate'] ?? json['LicenseExpiryDate']),
      licenseExpired: json['licenseExpired'] as bool? ??
          json['LicenseExpired'] as bool? ??
          false,
      licenseExpiringSoon: json['licenseExpiringSoon'] as bool? ??
          json['LicenseExpiringSoon'] as bool? ??
          false,
      verificationStatus: json['verificationStatus'] as String? ??
          json['VerificationStatus'] as String?,
      branchId: json['branchId'] as int? ?? json['BranchId'] as int?,
      branchName:
          json['branchName'] as String? ?? json['BranchName'] as String?,
      assignedVehicleId: json['assignedVehicleId'] as int? ??
          json['AssignedVehicleId'] as int?,
      assignedVehicleRegistration:
          json['assignedVehicleRegistration'] as String? ??
              json['AssignedVehicleRegistration'] as String?,
      assignedVehicleName: json['assignedVehicleName'] as String? ??
          json['AssignedVehicleName'] as String?,
      rating: (json['rating'] as num? ?? json['Rating'] as num?)?.toDouble(),
      gpsOnline:
          json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
      availabilityBucket: json['availabilityBucket'] as String? ??
          json['AvailabilityBucket'] as String?,
      yearsExperience:
          json['yearsExperience'] as int? ?? json['YearsExperience'] as int?,
      hireDate: _date(json['hireDate'] ?? json['HireDate']),
      emergencyContactName: json['emergencyContactName'] as String? ??
          json['EmergencyContactName'] as String?,
      emergencyContact: json['emergencyContact'] as String? ??
          json['EmergencyContact'] as String?,
      photoUrl: json['photoUrl'] as String? ?? json['PhotoUrl'] as String?,
    );
  }
}

class DriverStats {
  const DriverStats({
    required this.totalDrivers,
    required this.active,
    required this.onTrip,
    required this.available,
    required this.offDuty,
    required this.gpsOnline,
    required this.licensesExpiringSoon,
    required this.licensesExpired,
    this.suspended = 0,
    this.pendingVerification = 0,
    this.rejectedDrivers = 0,
    this.licensesExpiringIn7Days = 0,
    this.licensesExpiringIn60Days = 0,
    this.assignedDrivers = 0,
    this.openViolations = 0,
    this.verifiedDrivers = 0,
    this.underReviewDrivers = 0,
    this.onLeave = 0,
    this.busy = 0,
  });

  final int totalDrivers;
  final int active;
  final int onTrip;
  final int available;
  final int offDuty;
  final int gpsOnline;
  final int licensesExpiringSoon;
  final int licensesExpired;
  final int suspended;
  final int pendingVerification;
  final int rejectedDrivers;
  final int licensesExpiringIn7Days;
  final int licensesExpiringIn60Days;
  final int assignedDrivers;
  final int openViolations;
  final int verifiedDrivers;
  final int underReviewDrivers;
  final int onLeave;
  final int busy;

  factory DriverStats.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) =>
        json[a] as int? ?? (b != null ? json[b] as int? : null) ?? 0;
    return DriverStats(
      totalDrivers: n('totalDrivers', 'TotalDrivers'),
      active: n('active', 'Active'),
      onTrip: n('onTrip', 'OnTrip'),
      available: n('available', 'Available'),
      offDuty: n('offDuty', 'OffDuty'),
      gpsOnline: n('gpsOnline', 'GpsOnline'),
      licensesExpiringSoon: n('licensesExpiringSoon', 'LicensesExpiringSoon'),
      licensesExpired: n('licensesExpired', 'LicensesExpired'),
      suspended: n('suspended', 'Suspended'),
      pendingVerification: n('pendingVerification', 'PendingVerification'),
      rejectedDrivers: n('rejectedDrivers', 'RejectedDrivers'),
      licensesExpiringIn7Days:
          n('licensesExpiringIn7Days', 'LicensesExpiringIn7Days'),
      licensesExpiringIn60Days:
          n('licensesExpiringIn60Days', 'LicensesExpiringIn60Days'),
      assignedDrivers: n('assignedDrivers', 'AssignedDrivers'),
      openViolations: n('openViolations', 'OpenViolations'),
      verifiedDrivers: n('verifiedDrivers', 'VerifiedDrivers'),
      underReviewDrivers: n('underReviewDrivers', 'UnderReviewDrivers'),
      onLeave: n('onLeave', 'OnLeave'),
      busy: n('busy', 'Busy'),
    );
  }

  static const empty = DriverStats(
    totalDrivers: 0,
    active: 0,
    onTrip: 0,
    available: 0,
    offDuty: 0,
    gpsOnline: 0,
    licensesExpiringSoon: 0,
    licensesExpired: 0,
  );
}

/// Client-side compliance score aligned with ERP `computeDriverScore`.
int computeDriverScore(DriverListItem row) {
  if (row.rating != null) {
    return (row.rating! * 20).round().clamp(0, 100);
  }
  var score = 88;
  if (row.licenseExpired) {
    score -= 35;
  } else if (row.licenseExpiringSoon) {
    score -= 12;
  }
  final status = row.status.toLowerCase();
  if (status == 'suspended') {
    score -= 45;
  } else if (status == 'onleave') {
    score -= 8;
  }
  if ((row.verificationStatus ?? '').toLowerCase() == 'verified') {
    score += 5;
  }
  if (row.assignedVehicleId != null) {
    score += 3;
  }
  return score.clamp(0, 100);
}

/// ERP-aligned Top Drivers blend: 60% GPS safety + 40% compliance.
const double kGpsScoreWeight = 0.6;
const double kComplianceScoreWeight = 0.4;

int blendDriverTopScore(int complianceScore, int? gpsScore) {
  if (gpsScore == null) return complianceScore.clamp(0, 100);
  return (kGpsScoreWeight * gpsScore + kComplianceScoreWeight * complianceScore)
      .round()
      .clamp(0, 100);
}

List<DriverRankItem> buildTopDriversFromList(
  List<DriverListItem> drivers, {
  int limit = 5,
  Map<int, DriverRankItem> gpsByDriverId = const {},
}) {
  final scored = drivers.map((d) {
    final compliance = computeDriverScore(d);
    final gps = gpsByDriverId[d.id];
    final blended = blendDriverTopScore(compliance, gps?.score);
    final label = blended >= 80
        ? 'Good'
        : blended >= 60
            ? 'Fair'
            : 'Needs attention';
    final breakdown = gps == null
        ? 'Compliance $compliance (no GPS)'
        : 'Safety ${gps.score}${gps.isPartial ? '*' : ''} · Compliance $compliance';
    return DriverRankItem(
      driverId: d.id,
      driverName: d.fullName.trim().isEmpty ? 'Driver' : d.fullName.trim(),
      score: blended,
      rating: '$label · $breakdown',
      isPartial: gps?.isPartial ?? false,
      safetyScore: gps?.score,
      complianceScore: compliance,
    );
  }).toList()
    ..sort((a, b) => b.score.compareTo(a.score));
  if (scored.length <= limit) return scored;
  return scored.take(limit).toList();
}

/// Prefer `fullName`; fall back to first/last when demo tenants omit names.
String resolveDriverDisplayName(Map<String, dynamic> json) {
  final full = (json['fullName'] ?? json['FullName'])?.toString().trim();
  if (full != null && full.isNotEmpty) return full;
  final first =
      (json['firstName'] ?? json['FirstName'])?.toString().trim() ?? '';
  final last = (json['lastName'] ?? json['LastName'])?.toString().trim() ?? '';
  final combined = '$first $last'.trim();
  if (combined.isNotEmpty) return combined;
  final code = (json['driverCode'] ?? json['DriverCode'])?.toString().trim();
  if (code != null && code.isNotEmpty) return code;
  return 'Driver';
}

class DriverPerformanceSummary {
  const DriverPerformanceSummary({
    required this.driverId,
    required this.driverName,
    this.rating,
    this.yearsExperience,
    required this.totalTrips,
    required this.completedTrips,
    required this.totalRevenue,
    required this.completionRate,
    required this.violationCount,
    required this.attendancePresentCount,
  });

  final int driverId;
  final String driverName;
  final double? rating;
  final int? yearsExperience;
  final int totalTrips;
  final int completedTrips;
  final double totalRevenue;
  final double completionRate;
  final int violationCount;
  final int attendancePresentCount;

  factory DriverPerformanceSummary.fromJson(Map<String, dynamic> json) {
    double d(String a, [String? b]) =>
        (json[a] as num? ?? (b != null ? json[b] as num? : null))?.toDouble() ??
        0;
    int n(String a, [String? b]) =>
        json[a] as int? ?? (b != null ? json[b] as int? : null) ?? 0;
    return DriverPerformanceSummary(
      driverId: n('driverId', 'DriverId'),
      driverName:
          json['driverName'] as String? ?? json['DriverName'] as String? ?? '',
      rating: (json['rating'] as num? ?? json['Rating'] as num?)?.toDouble(),
      yearsExperience:
          json['yearsExperience'] as int? ?? json['YearsExperience'] as int?,
      totalTrips: n('totalTrips', 'TotalTrips'),
      completedTrips: n('completedTrips', 'CompletedTrips'),
      totalRevenue: d('totalRevenue', 'TotalRevenue'),
      completionRate: d('completionRate', 'CompletionRate'),
      violationCount: n('violationCount', 'ViolationCount'),
      attendancePresentCount:
          n('attendancePresentCount', 'AttendancePresentCount'),
    );
  }
}

class DriverViolation {
  const DriverViolation({
    required this.id,
    required this.violationType,
    required this.severity,
    required this.occurredAt,
    required this.status,
    this.description,
  });

  final int id;
  final String violationType;
  final String severity;
  final DateTime occurredAt;
  final String status;
  final String? description;

  factory DriverViolation.fromJson(Map<String, dynamic> json) {
    return DriverViolation(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      violationType: json['violationType'] as String? ??
          json['ViolationType'] as String? ??
          '',
      severity:
          json['severity'] as String? ?? json['Severity'] as String? ?? '',
      occurredAt: _date(json['occurredAt'] ?? json['OccurredAt']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      description:
          json['description'] as String? ?? json['Description'] as String?,
    );
  }
}

class DriverAttendanceRow {
  const DriverAttendanceRow({
    required this.id,
    required this.attendanceDate,
    required this.status,
    this.checkInAt,
    this.checkOutAt,
    this.notes,
  });

  final int id;
  final DateTime attendanceDate;
  final String status;
  final DateTime? checkInAt;
  final DateTime? checkOutAt;
  final String? notes;

  factory DriverAttendanceRow.fromJson(Map<String, dynamic> json) {
    return DriverAttendanceRow(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      attendanceDate: _date(json['attendanceDate'] ?? json['AttendanceDate']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      checkInAt: _date(json['checkInAt'] ?? json['CheckInAt']),
      checkOutAt: _date(json['checkOutAt'] ?? json['CheckOutAt']),
      notes: json['notes'] as String? ?? json['Notes'] as String?,
    );
  }
}

class DriverDocumentItem {
  const DriverDocumentItem({
    required this.id,
    required this.documentType,
    required this.status,
    this.fileUrl,
    this.expiryDate,
    this.rejectionReason,
  });

  final int id;
  final String documentType;
  final String status;
  final String? fileUrl;
  final DateTime? expiryDate;
  final String? rejectionReason;

  bool get isExpired =>
      expiryDate != null && expiryDate!.toLocal().isBefore(DateTime.now());

  bool get isExpiringSoon {
    if (expiryDate == null || isExpired) return false;
    return expiryDate!.toLocal().difference(DateTime.now()).inDays <= 30;
  }

  factory DriverDocumentItem.fromJson(Map<String, dynamic> json) {
    return DriverDocumentItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      documentType: json['documentType'] as String? ??
          json['DocumentType'] as String? ??
          '',
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      fileUrl: json['fileUrl'] as String? ?? json['FileUrl'] as String?,
      expiryDate: _date(json['expiryDate'] ?? json['ExpiryDate']),
      rejectionReason: json['rejectionReason'] as String? ??
          json['RejectionReason'] as String?,
    );
  }
}

class DriverRankItem {
  const DriverRankItem({
    required this.driverId,
    required this.driverName,
    required this.score,
    required this.rating,
    this.isPartial = false,
    this.safetyScore,
    this.complianceScore,
  });

  final int driverId;
  final String driverName;
  final int score;
  final String rating;
  final bool isPartial;
  final int? safetyScore;
  final int? complianceScore;

  factory DriverRankItem.fromJson(Map<String, dynamic> json) {
    return DriverRankItem(
      driverId: json['driverId'] as int? ?? json['DriverId'] as int? ?? 0,
      driverName:
          json['driverName'] as String? ?? json['DriverName'] as String? ?? '',
      score: json['score'] as int? ?? json['Score'] as int? ?? 0,
      rating: (json['rating'] ?? json['Rating'] ?? '').toString(),
      isPartial:
          json['isPartial'] as bool? ?? json['IsPartial'] as bool? ?? false,
      safetyScore: json['score'] as int? ?? json['Score'] as int?,
    );
  }
}

class DriverAssignment {
  const DriverAssignment({
    required this.id,
    required this.vehicleId,
    required this.assignmentType,
    required this.status,
    required this.startAt,
    this.vehicleRegistration,
    this.vehicleCode,
    this.vehicleName,
    this.vehicleMake,
    this.vehicleModel,
    this.vehicleColor,
    this.endAt,
    this.bookingId,
    this.assignedBy,
    this.remarks,
  });

  final int id;
  final int vehicleId;
  final String assignmentType;
  final String status;
  final DateTime startAt;
  final String? vehicleRegistration;
  final String? vehicleCode;
  final String? vehicleName;
  final String? vehicleMake;
  final String? vehicleModel;
  final String? vehicleColor;
  final DateTime? endAt;
  final int? bookingId;
  final String? assignedBy;
  final String? remarks;

  String get vehicleLabel {
    final name = vehicleName?.trim();
    if (name != null && name.isNotEmpty) return name;
    final reg = vehicleRegistration?.trim();
    if (reg != null && reg.isNotEmpty) return reg;
    final code = vehicleCode?.trim();
    if (code != null && code.isNotEmpty) return code;
    return 'Vehicle #$vehicleId';
  }

  factory DriverAssignment.fromJson(Map<String, dynamic> json) {
    return DriverAssignment(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      assignmentType:
          (json['assignmentType'] ?? json['AssignmentType'] ?? '').toString(),
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      startAt: _date(json['startAt'] ?? json['StartAt']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      vehicleRegistration: json['vehicleRegistration'] as String? ??
          json['VehicleRegistration'] as String?,
      vehicleCode:
          json['vehicleCode'] as String? ?? json['VehicleCode'] as String?,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      vehicleMake:
          json['vehicleMake'] as String? ?? json['VehicleMake'] as String?,
      vehicleModel:
          json['vehicleModel'] as String? ?? json['VehicleModel'] as String?,
      vehicleColor:
          json['vehicleColor'] as String? ?? json['VehicleColor'] as String?,
      endAt: _date(json['endAt'] ?? json['EndAt']),
      bookingId: json['bookingId'] as int? ?? json['BookingId'] as int?,
      assignedBy:
          json['assignedBy'] as String? ?? json['AssignedBy'] as String?,
      remarks: json['remarks'] as String? ?? json['Remarks'] as String?,
    );
  }
}

/// Client-side mirror of [DriverAssignmentGuard] for assign/reassign UX.
String? driverAssignmentBlockReason({
  required bool isActive,
  required String status,
  required bool licenseExpired,
  String? verificationStatus,
  bool requireVerified = true,
}) {
  if (!isActive) return 'Driver account is inactive.';
  final s = status.trim().toLowerCase();
  if (s == 'suspended') return 'Driver is Suspended and cannot be assigned.';
  if (s == 'onleave' || s == 'on leave') {
    return 'Driver is OnLeave and cannot be assigned.';
  }
  if (s == 'ontrip' || s == 'on trip') {
    return 'Driver is on an active trip and cannot be reassigned.';
  }
  if (licenseExpired) return 'Driver license is expired.';
  if (requireVerified) {
    final v = (verificationStatus ?? '').trim().toLowerCase();
    if (v != 'verified') {
      return 'Driver must be verified before vehicle assignment.';
    }
  } else {
    final v = (verificationStatus ?? '').trim().toLowerCase();
    if (v == 'rejected') return 'Driver verification was rejected.';
    if (v == 'expireddocs') return 'Driver documents have expired.';
  }
  return null;
}

DateTime? _date(Object? raw) {
  if (raw == null) return null;
  if (raw is DateTime) return raw;
  return DateTime.tryParse(raw.toString());
}
