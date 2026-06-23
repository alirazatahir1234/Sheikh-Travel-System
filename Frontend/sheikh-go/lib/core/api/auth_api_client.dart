import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../constants/api_config.dart';
import '../models/auth_session.dart';
import '../services/session_storage.dart';

final dioProvider = Provider<Dio>((ref) {
  final session = ref.watch(sessionProvider).valueOrNull;
  return Dio(
    BaseOptions(
      baseUrl: ApiConfig.baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 15),
      headers: {
        'X-Tenant-Slug': ApiConfig.tenantSlug,
        'Content-Type': 'application/json',
        if (session?.accessToken != null)
          'Authorization': 'Bearer ${session!.accessToken}',
      },
    ),
  );
});

class AuthApiClient {
  AuthApiClient(this._dio);

  final Dio _dio;

  /// Same contract as SheikhGo ERP: POST /api/auth/login
  Future<AuthSession> login({
    required String emailOrPhone,
    required String password,
  }) async {
    final res = await _dio.post<Map<String, dynamic>>(
      '/auth/login',
      data: {'email': emailOrPhone, 'password': password},
    );
    final body = res.data;
    if (body == null) {
      throw Exception('Empty response from API');
    }
    if (body['success'] == false) {
      throw Exception(body['message']?.toString() ?? 'Login failed');
    }
    final data = body['data'] as Map<String, dynamic>? ?? body;
    final session = AuthSession.fromJson(data);
    if (session.accessToken.isEmpty) {
      throw Exception('No access token in response');
    }
    return session;
  }

  Future<void> logout(String refreshToken) async {
    try {
      await _dio.post('/auth/logout', data: {'refreshToken': refreshToken});
    } on DioException {
      // Clear local session even if server logout fails.
    }
  }
}

String formatDioError(Object e) {
  if (e is DioException) {
    if (e.type == DioExceptionType.connectionError) {
      final url = ApiConfig.baseUrl;
      final usesLocalhost =
          url.contains('127.0.0.1') || url.contains('localhost');
      final emulatorHint = usesLocalhost
          ? ' If using an Android emulator, use http://10.0.2.2:5082/api.'
          : '';
      return 'Cannot reach API at $url.$emulatorHint Otherwise start the .NET API (port 5082).';
    }
    final data = e.response?.data;
    if (data is Map && data['message'] != null) {
      return data['message'].toString();
    }
    if (e.response?.statusCode != null) {
      return 'HTTP ${e.response!.statusCode}: ${e.message}';
    }
    return e.message ?? e.toString();
  }
  return e.toString().replaceFirst('Exception: ', '');
}

final authApiProvider = Provider<AuthApiClient>(
  (ref) => AuthApiClient(ref.watch(dioProvider)),
);
