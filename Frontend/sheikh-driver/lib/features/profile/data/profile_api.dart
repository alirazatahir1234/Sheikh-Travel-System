import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../domain/driver_profile_model.dart';

final profileApiProvider = Provider<ProfileApi>((ref) => ProfileApi(ref.read(dioProvider)));

class ProfileApi {
  ProfileApi(this._dio);
  final Dio _dio;

  Future<DriverProfile> getProfile() async {
    final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.driverProfile);
    final body = res.data;
    if (body == null) throw Exception('No profile data');
    final data = (body['data'] as Map<String, dynamic>?) ?? body;
    return DriverProfile.fromJson(data);
  }
}
