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
      fullName: json['fullName'] as String? ?? json['FullName'] as String? ?? '',
      phone: json['phone'] as String? ?? json['Phone'] as String? ?? '',
      licenseNumber:
          json['licenseNumber'] as String? ?? json['LicenseNumber'] as String? ?? '',
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      isActive: json['isActive'] as bool? ?? json['IsActive'] as bool? ?? true,
      driverCode: json['driverCode'] as String? ?? json['DriverCode'] as String?,
      licenseExpiryDate: _date(json['licenseExpiryDate'] ?? json['LicenseExpiryDate']),
      licenseExpired:
          json['licenseExpired'] as bool? ?? json['LicenseExpired'] as bool? ?? false,
      licenseExpiringSoon: json['licenseExpiringSoon'] as bool? ??
          json['LicenseExpiringSoon'] as bool? ??
          false,
      verificationStatus: json['verificationStatus'] as String? ??
          json['VerificationStatus'] as String?,
      branchName: json['branchName'] as String? ?? json['BranchName'] as String?,
      assignedVehicleId:
          json['assignedVehicleId'] as int? ?? json['AssignedVehicleId'] as int?,
      assignedVehicleRegistration: json['assignedVehicleRegistration'] as String? ??
          json['AssignedVehicleRegistration'] as String?,
      assignedVehicleName: json['assignedVehicleName'] as String? ??
          json['AssignedVehicleName'] as String?,
      rating: (json['rating'] as num? ?? json['Rating'] as num?)?.toDouble(),
      gpsOnline: json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
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
    this.email,
    this.cnic,
    this.address,
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
  final String? email;
  final String? cnic;
  final String? address;
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
  final int? yearsExperience;
  final DateTime? hireDate;
  final String? emergencyContactName;
  final String? emergencyContact;
  final String? photoUrl;

  factory DriverDetail.fromJson(Map<String, dynamic> json) {
    return DriverDetail(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      fullName: json['fullName'] as String? ?? json['FullName'] as String? ?? '',
      phone: json['phone'] as String? ?? json['Phone'] as String? ?? '',
      licenseNumber:
          json['licenseNumber'] as String? ?? json['LicenseNumber'] as String? ?? '',
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      isActive: json['isActive'] as bool? ?? json['IsActive'] as bool? ?? true,
      driverCode: json['driverCode'] as String? ?? json['DriverCode'] as String?,
      email: json['email'] as String? ?? json['Email'] as String?,
      cnic: json['cnic'] as String? ?? json['CNIC'] as String?,
      address: json['address'] as String? ?? json['Address'] as String?,
      licenseExpiryDate: _date(json['licenseExpiryDate'] ?? json['LicenseExpiryDate']),
      licenseExpired:
          json['licenseExpired'] as bool? ?? json['LicenseExpired'] as bool? ?? false,
      licenseExpiringSoon: json['licenseExpiringSoon'] as bool? ??
          json['LicenseExpiringSoon'] as bool? ??
          false,
      verificationStatus: json['verificationStatus'] as String? ??
          json['VerificationStatus'] as String?,
      branchName: json['branchName'] as String? ?? json['BranchName'] as String?,
      assignedVehicleId:
          json['assignedVehicleId'] as int? ?? json['AssignedVehicleId'] as int?,
      assignedVehicleRegistration: json['assignedVehicleRegistration'] as String? ??
          json['AssignedVehicleRegistration'] as String?,
      assignedVehicleName: json['assignedVehicleName'] as String? ??
          json['AssignedVehicleName'] as String?,
      rating: (json['rating'] as num? ?? json['Rating'] as num?)?.toDouble(),
      gpsOnline: json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
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
  });

  final int totalDrivers;
  final int active;
  final int onTrip;
  final int available;
  final int offDuty;
  final int gpsOnline;
  final int licensesExpiringSoon;
  final int licensesExpired;

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
  });

  final int driverId;
  final String driverName;
  final int score;
  final String rating;
  final bool isPartial;

  factory DriverRankItem.fromJson(Map<String, dynamic> json) {
    return DriverRankItem(
      driverId: json['driverId'] as int? ?? json['DriverId'] as int? ?? 0,
      driverName:
          json['driverName'] as String? ?? json['DriverName'] as String? ?? '',
      score: json['score'] as int? ?? json['Score'] as int? ?? 0,
      rating: (json['rating'] ?? json['Rating'] ?? '').toString(),
      isPartial:
          json['isPartial'] as bool? ?? json['IsPartial'] as bool? ?? false,
    );
  }
}

DateTime? _date(Object? raw) {
  if (raw == null) return null;
  if (raw is DateTime) return raw;
  return DateTime.tryParse(raw.toString());
}
