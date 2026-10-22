import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/errors/app_exception.dart';
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
    final data = _unwrapLoginData(res.data);
    final session = FleetSession.fromStaffJson(data);
    _ensureSessionTokens(session, res.data);
    return session;
  }

  Future<FleetSession> _driverLogin(LoginRequest request) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.driverLogin,
      data: request.toDriverJson(),
    );
    final data = _unwrapLoginData(res.data);
    final session = FleetSession.fromDriverJson(data);
    _ensureSessionTokens(session, res.data);
    return session;
  }

  Future<void> logout(String refreshToken) async {
    try {
      await _dio.post(ApiEndpoints.logout, data: {'refreshToken': refreshToken});
    } catch (_) {
      // Ignore logout errors — local session is cleared regardless
    }
  }

  /// Returns optional debugToken when PortalAuth:DevMode is enabled on the API.
  Future<String?> forgotPassword(String phone) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.driverForgotPassword,
      data: {'phone': phone.trim()},
    );
    final body = res.data;
    if (body == null) throw const UnknownException('Empty response');
    ApiResponseParser.ensureSuccess(body);
    final data = body['data'] ?? body['Data'];
    if (data is Map<String, dynamic>) {
      return data['debugToken']?.toString();
    }
    if (data is Map) {
      return Map<String, dynamic>.from(data)['debugToken']?.toString();
    }
    return null;
  }

  Future<void> resetPassword({required String token, required String newPassword}) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.driverResetPassword,
      data: {'token': token.trim(), 'newPassword': newPassword},
    );
    final body = res.data;
    if (body == null) throw const UnknownException('Empty response');
    ApiResponseParser.ensureSuccess(body);
  }

  Future<CompanyContext?> fetchCompanyContext() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.companyContext);
      final data = ApiResponseParser.requireDataMap(res.data);
      return CompanyContext.fromJson(data);
    } catch (_) {
      try {
        final res = await _dio.get<Map<String, dynamic>>(
          ApiEndpoints.companyContextAlias,
        );
        final data = ApiResponseParser.requireDataMap(res.data);
        return CompanyContext.fromJson(data);
      } catch (_) {
        return null;
      }
    }
  }

  /// FailResponse often returns HTTP 200 with `success:false` and `data:null`.
  /// Never treat the envelope itself as session JSON.
  Map<String, dynamic> _unwrapLoginData(Map<String, dynamic>? body) {
    return ApiResponseParser.requireDataMap(
      body,
      fallbackMessage: 'Login failed',
    );
  }

  void _ensureSessionTokens(FleetSession session, Map<String, dynamic>? body) {
    if (session.accessToken.trim().isEmpty ||
        session.refreshToken.trim().isEmpty) {
      throw ValidationException(
        ApiResponseParser.messageOf(body) ?? 'Login failed',
      );
    }
  }
}
