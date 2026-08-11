import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/fleet_models.dart';

final fleetApiProvider =
    Provider<FleetApi>((ref) => FleetApi(ref.read(dioProvider)));

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
    return ApiResponseParser.dataList(res.data)
        .map(GpsPosition.fromJson)
        .toList();
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

  Future<List<GpsTrip>> getGpsTrips({
    required int vehicleId,
    DateTime? from,
    DateTime? to,
    int pageSize = 100,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsTrips,
      queryParameters: {
        'vehicleId': vehicleId,
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
        'page': 1,
        'pageSize': pageSize,
      },
      options: Options(receiveTimeout: const Duration(seconds: 45)),
    );
    final items = ApiResponseParser.pagedItems(res.data);
    final fallback = ApiResponseParser.dataList(res.data);
    return (items.isNotEmpty ? items : fallback).map(GpsTrip.fromJson).toList();
  }

  Future<TripDetailBundle> getTripDetail(String tripKey) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsTripByKey(tripKey),
      options: Options(
        receiveTimeout: const Duration(seconds: 90),
        sendTimeout: const Duration(seconds: 30),
      ),
    );
    return TripDetailBundle.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<HistoryReplayBundle> getTripReplay({
    required int vehicleId,
    required DateTime from,
    required DateTime to,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsTripsReplay,
      queryParameters: {
        'vehicleId': vehicleId,
        'from': from.toUtc().toIso8601String(),
        'to': to.toUtc().toIso8601String(),
      },
      options: Options(
        receiveTimeout: const Duration(seconds: 90),
        sendTimeout: const Duration(seconds: 30),
      ),
    );
    return HistoryReplayBundle.fromJson(ApiResponseParser.dataMap(res.data));
  }

  /// Today's (or custom-range) trip analytics for a vehicle.
  Future<TripAnalyticsBundle> getTripAnalytics(
    int vehicleId, {
    DateTime? from,
    DateTime? to,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsTripsAnalytics,
      queryParameters: {
        'vehicleId': vehicleId,
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
      },
      options: Options(
        receiveTimeout: const Duration(seconds: 45),
      ),
    );
    return TripAnalyticsBundle.fromJson(ApiResponseParser.dataMap(res.data));
  }

  /// Resolves a human-readable address for map coordinates (cache-first on server).
  /// Set [forceRefresh] when the caller knows the cached line is too coarse (city-only).
  Future<ReverseGeocodeInfo?> reverseGeocodeInfo(
    double latitude,
    double longitude, {
    bool forceRefresh = false,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.gpsLocationReverse,
      queryParameters: {
        'lat': latitude,
        'lng': longitude,
        if (forceRefresh) 'forceRefresh': true,
      },
      options: Options(
        receiveTimeout: const Duration(seconds: 20),
      ),
    );
    final data = ApiResponseParser.dataMap(res.data);
    final formatted = (data['formattedAddress'] as String? ??
            data['FormattedAddress'] as String?)
        ?.trim();
    if (formatted == null || formatted.isEmpty) return null;
    final road = (data['road'] as String? ?? data['Road'] as String?)?.trim();
    final placeName =
        (data['placeName'] as String? ?? data['PlaceName'] as String?)?.trim();
    final placeType =
        (data['placeType'] as String? ?? data['PlaceType'] as String?)?.trim();
    final city = (data['city'] as String? ?? data['City'] as String?)?.trim();
    final state =
        (data['state'] as String? ?? data['State'] as String?)?.trim();

    var address = formatted;
    if (road != null &&
        road.isNotEmpty &&
        !_looksStreetLevel(formatted) &&
        !formatted.toLowerCase().contains(road.toLowerCase())) {
      address = [road, city, state]
          .where((s) => s != null && s.isNotEmpty)
          .join(', ');
    }
    return ReverseGeocodeInfo(
      formattedAddress: address,
      placeName: placeName?.isEmpty == true ? null : placeName,
      placeType: placeType?.isEmpty == true ? null : placeType,
      road: road,
      city: city,
      state: state,
    );
  }

  /// Convenience: formatted address string (with place name prefix when present).
  Future<String?> reverseGeocode(
    double latitude,
    double longitude, {
    bool forceRefresh = false,
  }) async {
    final info = await reverseGeocodeInfo(
      latitude,
      longitude,
      forceRefresh: forceRefresh,
    );
    return info?.displayLine;
  }

  static bool _looksStreetLevel(String address) {
    final parts = address
        .split(',')
        .map((e) => e.trim())
        .where((e) => e.isNotEmpty)
        .toList();
    if (parts.any((p) => RegExp(r'\d').hasMatch(p))) return true;
    return parts.length > 3;
  }

  Future<({String title, String summary, List<String> bullets})>
      getReplayInsights(
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

class ReverseGeocodeInfo {
  const ReverseGeocodeInfo({
    required this.formattedAddress,
    this.placeName,
    this.placeType,
    this.road,
    this.city,
    this.state,
  });

  final String formattedAddress;
  final String? placeName;
  final String? placeType;
  final String? road;
  final String? city;
  final String? state;

  /// Street / locality first; placeName is optional secondary metadata only.
  String get displayLine {
    final addr = formattedAddress.trim();
    if (addr.isNotEmpty) return addr;
    final place = placeName?.trim();
    if (place != null && place.isNotEmpty) return place;
    return addr;
  }
}
