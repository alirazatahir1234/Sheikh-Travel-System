import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../auth/data/auth_repository.dart';
import '../../auth/domain/auth_models.dart';
import '../domain/driver_profile_model.dart';

final profileApiProvider = Provider<ProfileApi>(
  (ref) => ProfileApi(
    ref.read(dioProvider),
    () => ref.read(fleetSessionProvider),
  ),
);

class ProfileApi {
  ProfileApi(this._dio, this._session);

  final Dio _dio;
  final FleetSession? Function() _session;

  Future<DriverProfile> getProfile() async {
    final session = _session();
    if (session != null && !session.isDriverOnly) {
      return _getStaffProfile(session);
    }
    return _getDriverProfile();
  }

  Future<DriverProfile> _getDriverProfile() async {
    final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.driverProfile);
    final body = res.data;
    if (body == null) throw Exception('No profile data');
    final data = (body['data'] as Map<String, dynamic>?) ?? body;
    return DriverProfile.fromJson(data);
  }

  Future<DriverProfile> _getStaffProfile(FleetSession session) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.authMe);
      final data = ApiResponseParser.dataMap(res.data);
      return DriverProfile.fromStaffUserJson(data, session);
    } catch (_) {
      return DriverProfile.fromStaffSession(session);
    }
  }
}
