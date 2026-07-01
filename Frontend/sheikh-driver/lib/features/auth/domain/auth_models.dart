class LoginRequest {
  const LoginRequest({required this.phone, required this.password});
  final String phone;
  final String password;
  Map<String, dynamic> toJson() => {'phone': phone, 'password': password};
}

class DriverSession {
  const DriverSession({
    required this.accessToken,
    required this.refreshToken,
    required this.driverId,
    required this.tenantId,
    required this.fullName,
    required this.phone,
  });

  final String accessToken;
  final String refreshToken;
  final int driverId;
  final int tenantId;
  final String fullName;
  final String phone;

  factory DriverSession.fromJson(Map<String, dynamic> json) {
    return DriverSession(
      accessToken: json['accessToken'] as String? ?? json['AccessToken'] as String? ?? '',
      refreshToken: json['refreshToken'] as String? ?? json['RefreshToken'] as String? ?? '',
      driverId: json['driverId'] as int? ?? json['DriverId'] as int? ?? 0,
      tenantId: json['tenantId'] as int? ?? json['TenantId'] as int? ?? 0,
      fullName: json['fullName'] as String? ?? json['FullName'] as String? ?? '',
      phone: json['phone'] as String? ?? json['Phone'] as String? ?? '',
    );
  }

  Map<String, dynamic> toJson() => {
        'accessToken': accessToken,
        'refreshToken': refreshToken,
        'driverId': driverId,
        'tenantId': tenantId,
        'fullName': fullName,
        'phone': phone,
      };

  DriverSession copyWith({String? accessToken, String? refreshToken}) {
    return DriverSession(
      accessToken: accessToken ?? this.accessToken,
      refreshToken: refreshToken ?? this.refreshToken,
      driverId: driverId,
      tenantId: tenantId,
      fullName: fullName,
      phone: phone,
    );
  }
}
