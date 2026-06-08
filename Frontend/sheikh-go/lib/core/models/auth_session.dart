class AuthSession {
  const AuthSession({
    required this.accessToken,
    required this.refreshToken,
    required this.fullName,
    required this.roles,
    this.email,
    this.phoneNumber,
  });

  final String accessToken;
  final String refreshToken;
  final String fullName;
  final List<String> roles;
  final String? email;
  final String? phoneNumber;

  factory AuthSession.fromJson(Map<String, dynamic> json) {
    final role = json['role']?.toString();
    return AuthSession(
      accessToken: json['accessToken']?.toString() ?? '',
      refreshToken: json['refreshToken']?.toString() ?? '',
      fullName: json['fullName']?.toString() ?? '',
      roles: role != null && role.isNotEmpty ? [role] : const [],
      email: json['email']?.toString(),
      phoneNumber: json['phoneNumber']?.toString(),
    );
  }

  Map<String, dynamic> toJson() => {
        'accessToken': accessToken,
        'refreshToken': refreshToken,
        'fullName': fullName,
        'role': roles.isNotEmpty ? roles.first : null,
        'email': email,
        'phoneNumber': phoneNumber,
      };
}
