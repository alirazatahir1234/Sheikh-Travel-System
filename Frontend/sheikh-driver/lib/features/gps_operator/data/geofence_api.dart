import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../../alerts/domain/gps_alert_models.dart';
import '../../fleet/domain/fleet_models.dart';
import '../domain/geofence_models.dart';

final geofenceApiProvider =
    Provider<GeofenceApi>((ref) => GeofenceApi(ref.read(dioProvider)));

/// Thrown when the tenant lacks the `geofencing` add-on (API 403
/// `code: feature_disabled`), so the UI can show an upsell instead of an error.
class GeofencingDisabledException implements Exception {
  const GeofencingDisabledException(this.message);
  final String message;

  @override
  String toString() => message;
}

class GeofenceApi {
  GeofenceApi(this._dio);
  final Dio _dio;

  static const _base = ApiEndpoints.gpsGeofences;

  Future<T> _guard<T>(Future<T> Function() run) async {
    try {
      return await run();
    } on DioException catch (e) {
      final data = e.response?.data;
      if (e.response?.statusCode == 403 &&
          data is Map &&
          data['code'] == 'feature_disabled') {
        throw GeofencingDisabledException(
          data['message'] as String? ?? 'Geofencing is not enabled.',
        );
      }
      rethrow;
    }
  }

  Future<List<GpsGeofenceItem>> list({
    String? search,
    String? areaType,
    bool? isActive,
    int? vehicleId,
  }) =>
      _guard(() async {
        final res = await _dio.get<Map<String, dynamic>>(
          _base,
          queryParameters: {
            if (search != null && search.trim().isNotEmpty)
              'search': search.trim(),
            if (areaType != null) 'areaType': areaType,
            if (isActive != null) 'isActive': isActive,
            if (vehicleId != null) 'vehicleId': vehicleId,
          },
        );
        return ApiResponseParser.dataList(res.data)
            .map(GpsGeofenceItem.fromJson)
            .toList();
      });

  Future<List<GeofenceVehicleState>> states({
    int? geofenceId,
    int? vehicleId,
  }) =>
      _guard(() async {
        final res = await _dio.get<Map<String, dynamic>>(
          '$_base/states',
          queryParameters: {
            if (geofenceId != null) 'geofenceId': geofenceId,
            if (vehicleId != null) 'vehicleId': vehicleId,
          },
        );
        return ApiResponseParser.dataList(res.data)
            .map(GeofenceVehicleState.fromJson)
            .toList();
      });

  Future<List<GeofenceAssignment>> assignments(int geofenceId) =>
      _guard(() async {
        final res =
            await _dio.get<Map<String, dynamic>>('$_base/$geofenceId/assignments');
        return ApiResponseParser.dataList(res.data)
            .map(GeofenceAssignment.fromJson)
            .toList();
      });

  Future<List<GpsAlertEvent>> events(int geofenceId) => _guard(() async {
        final res =
            await _dio.get<Map<String, dynamic>>('$_base/$geofenceId/events');
        return ApiResponseParser.dataList(res.data)
            .map(GpsAlertEvent.fromJson)
            .toList();
      });

  Future<void> create(GeofenceDraft draft) => _guard(() async {
        final res =
            await _dio.post<Map<String, dynamic>>(_base, data: draft.toJson());
        ApiResponseParser.ensureSuccess(res.data);
      });

  Future<void> update(int id, GeofenceDraft draft) => _guard(() async {
        final res = await _dio.put<Map<String, dynamic>>(
          '$_base/$id',
          data: draft.toJson(),
        );
        ApiResponseParser.ensureSuccess(res.data);
      });

  Future<void> delete(int id) => _guard(() async {
        final res = await _dio.delete<Map<String, dynamic>>('$_base/$id');
        ApiResponseParser.ensureSuccess(res.data);
      });
}
