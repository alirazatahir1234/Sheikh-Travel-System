import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../domain/auth_models.dart';

final authApiProvider = Provider<AuthApi>((ref) => AuthApi(ref.read(dioProvider)));

class AuthApi {
  AuthApi(this._dio);
  final Dio _dio;

  Future<DriverSession> login(LoginRequest request) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.driverLogin,
      data: request.toJson(),
    );
    final body = res.data;
    if (body == null) throw Exception('Empty response');
    if (body['success'] == false) {
      throw Exception(body['message']?.toString() ?? 'Login failed');
    }
    final data = (body['data'] as Map<String, dynamic>?) ?? body;
    return DriverSession.fromJson(data);
  }

  Future<void> logout(String refreshToken) async {
    try {
      await _dio.post(ApiEndpoints.logout, data: {'refreshToken': refreshToken});
    } catch (_) {
      // Ignore logout errors — local session is cleared regardless
    }
  }
}
