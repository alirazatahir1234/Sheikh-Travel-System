import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../domain/auth_models.dart';

final authApiProvider = Provider<AuthApi>((ref) => AuthApi(ref.read(dioProvider)));

class AuthApi {
  AuthApi(this._dio);
  final Dio _dio;

  Future<FleetSession> login(LoginRequest request) {
    if (request.isEmailLogin) {
      return _staffLogin(request);
    }
    return _driverLogin(request);
  }

  Future<FleetSession> _staffLogin(LoginRequest request) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.staffLogin,
      data: request.toStaffJson(),
    );
    final data = _unwrapData(res.data);
    return FleetSession.fromStaffJson(data);
  }

  Future<FleetSession> _driverLogin(LoginRequest request) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.driverLogin,
      data: request.toDriverJson(),
    );
    final data = _unwrapData(res.data);
    return FleetSession.fromDriverJson(data);
  }

  Future<void> logout(String refreshToken) async {
    try {
      await _dio.post(ApiEndpoints.logout, data: {'refreshToken': refreshToken});
    } catch (_) {
      // Ignore logout errors — local session is cleared regardless
    }
  }

  Future<CompanyContext?> fetchCompanyContext() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.companyContext);
      final data = _unwrapData(res.data);
      return CompanyContext.fromJson(data);
    } catch (_) {
      try {
        final res = await _dio.get<Map<String, dynamic>>(
          ApiEndpoints.companyContextAlias,
        );
        final data = _unwrapData(res.data);
        return CompanyContext.fromJson(data);
      } catch (_) {
        return null;
      }
    }
  }

  Map<String, dynamic> _unwrapData(Map<String, dynamic>? body) {
    if (body == null) throw Exception('Empty response');
    if (body['success'] == false) {
      throw Exception(body['message']?.toString() ?? 'Login failed');
    }
    return (body['data'] as Map<String, dynamic>?) ?? body;
  }
}
