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
    );
  }
}
