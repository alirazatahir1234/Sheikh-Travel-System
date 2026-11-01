import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/gps_alert_models.dart';

final gpsAlertsApiProvider =
    Provider<GpsAlertsApi>((ref) => GpsAlertsApi(ref.read(dioProvider)));

class GpsAlertsApi {
  GpsAlertsApi(this._dio);
  final Dio _dio;

  Future<List<GpsAlertEvent>> listEvents({
    String? eventType,
    String? severity,
    String? severityGroup,
    String? status,
    String? readState,
    String? datePreset,
    String? search,
    String? sort,
    int? vehicleId,
    int? driverId,
    int? geofenceId,
    bool? unacknowledgedOnly,
    int? page,
    int? pageSize,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsAlertEvents,
      queryParameters: {
        if (eventType != null) 'eventType': eventType,
        if (severity != null) 'severity': severity,
        if (severityGroup != null) 'severityGroup': severityGroup,
        if (status != null) 'status': status,
        if (readState != null) 'readState': readState,
        if (datePreset != null) 'datePreset': datePreset,
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        if (sort != null) 'sort': sort,
        if (vehicleId != null) 'vehicleId': vehicleId,
        if (driverId != null) 'driverId': driverId,
        if (geofenceId != null) 'geofenceId': geofenceId,
        if (unacknowledgedOnly == true) 'unacknowledgedOnly': true,
        if (page != null) 'page': page,
        if (pageSize != null) 'pageSize': pageSize,
      },
    );
    final body = res.data;
    ApiResponseParser.ensureSuccess(body);
    final data = body?['data'];
    if (data is List) {
      return data
          .whereType<Map>()
          .map((e) => GpsAlertEvent.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }
    return ApiResponseParser.pagedItems(body)
        .map(GpsAlertEvent.fromJson)
        .toList();
  }

  Future<GpsAlertStats> stats() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.gpsAlertStats);
    return GpsAlertStats.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<GpsAlertEvent> getById(int id) async {
    final res = await _dio
        .get<Map<String, dynamic>>(ApiEndpoints.gpsAlertEventById(id));
    return GpsAlertEvent.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<void> acknowledge(int id) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.gpsAlertAcknowledge(id),
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<int> bulkAcknowledge(List<int> ids) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.gpsAlertBulkAcknowledge,
      data: {'ids': ids},
    );
    ApiResponseParser.ensureSuccess(res.data);
    final data = res.data?['data'] ?? res.data?['Data'];
    if (data is num) return data.toInt();
    return ids.length;
  }

  Future<void> assign(
    int id, {
    int? assignedToUserId,
    String? assignedToName,
  }) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.gpsAlertAssign(id),
      data: {
        'assignedToUserId': assignedToUserId,
        'assignedToName': assignedToName,
      },
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<int> bulkAssign(
    List<int> ids, {
    int? assignedToUserId,
    String? assignedToName,
  }) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.gpsAlertBulkAssign,
      data: {
        'ids': ids,
        'assignedToUserId': assignedToUserId,
        'assignedToName': assignedToName,
      },
    );
    ApiResponseParser.ensureSuccess(res.data);
    final data = res.data?['data'] ?? res.data?['Data'];
    if (data is num) return data.toInt();
    return ids.length;
  }

  Future<void> markRead(int id) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.gpsAlertRead(id),
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<void> resolve(int id, {String? notes}) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.gpsAlertResolve(id),
      data: {'resolutionNotes': notes},
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<void> archive(int id, {String? reason}) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.gpsAlertArchive(id),
      data: {'archiveReason': reason},
    );
    ApiResponseParser.ensureSuccess(res.data);
  }
}
