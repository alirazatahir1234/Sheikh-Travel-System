import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../constants/api_config.dart';
import '../services/session_storage.dart';

final dioProvider = Provider<Dio>((ref) {
  final token = ref.watch(sessionTokenProvider);
  final dio = Dio(BaseOptions(
    baseUrl: ApiConfig.baseUrl,
    connectTimeout: const Duration(seconds: 15),
    receiveTimeout: const Duration(seconds: 15),
    headers: {
      'X-Tenant-Slug': ApiConfig.tenantSlug,
      'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    },
  ));
  return dio;
});

class DriverApiClient {
  DriverApiClient(this._dio);
  final Dio _dio;

  Future<String> login(String phone, String password) async {
    final res = await _dio.post<Map<String, dynamic>>(
      '/driver-app/auth/login',
      data: {'phone': phone, 'password': password},
    );
    final body = res.data;
    if (body == null) {
      throw Exception('Empty response from API');
    }
    if (body['success'] == false) {
      throw Exception(body['message']?.toString() ?? 'Login failed');
    }
    final data = body['data'] as Map<String, dynamic>? ?? body;
    final token = data['accessToken'] ?? data['AccessToken'];
    if (token == null) {
      throw Exception('No access token in response');
    }
    return token as String;
  }

  Future<List<dynamic>> getTrips() async {
    final res = await _dio.get<Map<String, dynamic>>('/driver-app/trips');
    final body = res.data;
    final data = body?['data'] ?? body;
    return data as List<dynamic>;
  }

  Future<void> startTrip(int id) async {
    await _dio.post('/driver-app/trips/$id/start');
  }

  Future<void> completeTrip(int id) async {
    await _dio.post('/driver-app/trips/$id/complete');
  }

  Future<void> postLocation({
    required int vehicleId,
    required double lat,
    required double lng,
    double speed = 0,
  }) async {
    await _dio.post('/driver-app/trips/location', data: {
      'vehicleId': vehicleId,
      'latitude': lat,
      'longitude': lng,
      'speed': speed,
    });
  }
}

String formatDioError(Object e) {
  if (e is DioException) {
    if (e.type == DioExceptionType.connectionError) {
      return 'Cannot reach API at ${ApiConfig.baseUrl}. Start the .NET API (port 5082).';
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
  return e.toString();
}

final driverApiProvider = Provider<DriverApiClient>(
  (ref) => DriverApiClient(ref.watch(dioProvider)),
);
