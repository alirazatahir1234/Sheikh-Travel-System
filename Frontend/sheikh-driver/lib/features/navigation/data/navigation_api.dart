import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../domain/gps_eta.dart';

final navigationApiProvider =
    Provider<NavigationApi>((ref) => NavigationApi(ref.read(dioProvider)));

class NavigationApi {
  NavigationApi(this._dio);
  final Dio _dio;

  /// Uses ERP GPS module endpoint: `GET /gps/eta?bookingId=`
  Future<GpsEta?> getEta(int bookingId) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.gpsEta,
        queryParameters: {'bookingId': bookingId},
      );
      final body = res.data;
      final data = body?['data'] as Map<String, dynamic>?;
      if (data == null) return null;
      return GpsEta.fromJson(data);
    } on DioException {
      return null;
    }
  }
}
