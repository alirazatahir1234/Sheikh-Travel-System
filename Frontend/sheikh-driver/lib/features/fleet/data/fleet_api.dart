import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/fleet_models.dart';

final fleetApiProvider = Provider<FleetApi>((ref) => FleetApi(ref.read(dioProvider)));

class FleetApi {
  FleetApi(this._dio);
  final Dio _dio;

  Future<GpsFleetStatusKpis> getFleetStatusLocal() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsFleetStatusLocal,
    );
    return GpsFleetStatusKpis.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<FleetOpsDashboard> getOpsDashboard() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.fleetDashboard,
    );
    return FleetOpsDashboard.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<List<GpsPosition>> getLivePositions({int pageSize = 100}) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsLive,
      queryParameters: {'page': 1, 'pageSize': pageSize},
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(GpsPosition.fromJson)
        .toList();
  }

  Future<List<VehicleListItem>> getVehicles({int pageSize = 100}) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.vehicles,
      queryParameters: {'page': 1, 'pageSize': pageSize},
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(VehicleListItem.fromJson)
        .toList();
  }

  Future<VehicleDetail> getVehicle(int id) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.vehicleById(id),
    );
    return VehicleDetail.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<List<GpsPosition>> getHistory(
    int vehicleId, {
    DateTime? from,
    DateTime? to,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsHistory(vehicleId),
      queryParameters: {
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
      },
    );
    return ApiResponseParser.dataList(res.data).map(GpsPosition.fromJson).toList();
  }

  Future<HistoryReplayBundle> getHistoryReplay(
    int vehicleId, {
    DateTime? from,
    DateTime? to,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsHistoryReplay,
      queryParameters: {
        'vehicleId': vehicleId,
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
      },
      // Replay pulls Traccar route + enrichment — often >20s on device networks.
      options: Options(
        receiveTimeout: const Duration(seconds: 90),
        sendTimeout: const Duration(seconds: 30),
      ),
    );
    return HistoryReplayBundle.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<({String title, String summary, List<String> bullets})> getReplayInsights(
    int vehicleId, {
    DateTime? from,
    DateTime? to,
  }) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.gpsHistoryReplayInsights,
      data: {
        'vehicleId': vehicleId,
        if (from != null) 'fromDate': from.toUtc().toIso8601String(),
        if (to != null) 'toDate': to.toUtc().toIso8601String(),
      },
    );
    final map = ApiResponseParser.dataMap(res.data);
    final bullets = map['bullets'] ?? map['Bullets'];
    return (
      title: (map['title'] ?? map['Title'] ?? 'Trip insight').toString(),
      summary: (map['summary'] ?? map['Summary'] ?? '').toString(),
      bullets: bullets is List
          ? bullets.map((e) => e.toString()).toList()
          : const <String>[],
    );
  }

  Future<List<VehicleDocumentItem>> getVehicleDocuments(int vehicleId) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.vehicleDocuments(vehicleId),
    );
    return ApiResponseParser.dataList(res.data)
        .map(VehicleDocumentItem.fromJson)
        .toList();
  }

  Future<List<VehicleMaintenanceItem>> getVehicleMaintenance(
    int vehicleId, {
    int pageSize = 20,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.vehicleMaintenance(vehicleId),
      queryParameters: {'page': 1, 'pageSize': pageSize},
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(VehicleMaintenanceItem.fromJson)
        .toList();
  }

  Future<VehicleFuelSummary> getVehicleFuel(
    int vehicleId, {
    int pageSize = 20,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.vehicleFuel(vehicleId),
      queryParameters: {'page': 1, 'pageSize': pageSize},
    );
    return VehicleFuelSummary.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<VehicleGpsInfo> getVehicleGps(int vehicleId) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.vehicleGps(vehicleId),
    );
    return VehicleGpsInfo.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<List<SupportedGpsCommand>> getSupportedCommands(int deviceId) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsCommandsSupported(deviceId),
    );
    return ApiResponseParser.dataList(res.data)
        .map(SupportedGpsCommand.fromJson)
        .toList();
  }

  Future<int> sendDeviceCommand({
    required int gpsDeviceId,
    required String commandType,
    String? reason,
  }) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.gpsCommandsSend,
      data: {
        'gpsDeviceId': gpsDeviceId,
        'commandType': commandType,
        if (reason != null) 'reason': reason,
      },
    );
    final data = res.data?['data'];
    if (data is int) return data;
    if (data is num) return data.toInt();
    return 0;
  }

  Future<List<GpsDeviceCommandItem>> getVehicleCommands(int vehicleId) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsCommandsByVehicle(vehicleId),
    );
    return ApiResponseParser.dataList(res.data)
        .map(GpsDeviceCommandItem.fromJson)
        .toList();
  }

  Future<List<GpsGeofenceItem>> getGeofences() async {
    final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.gpsGeofences);
    final data = res.data?['data'];
    if (data is List) {
      return data
          .whereType<Map>()
          .map((e) => GpsGeofenceItem.fromJson(Map<String, dynamic>.from(e)))
          .toList();
    }
    return ApiResponseParser.pagedItems(res.data)
        .map(GpsGeofenceItem.fromJson)
        .toList();
  }
}
