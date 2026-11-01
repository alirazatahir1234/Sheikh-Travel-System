import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:hive_flutter/hive_flutter.dart';

import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/config/app_config.dart';
import '../domain/trip_driving_route.dart';

/// How long a pickup→drop polyline stays valid on device.
/// Long enough that a rebuild or hot restart does not call Routes again,
/// short enough that a changed road network is picked up the same day.
const tripRouteCacheTtl = Duration(hours: 6);

const _hiveBoxName = 'trip_driving_routes';

/// One Routes lookup per pickup/drop pair for the Trip Details map.
///
/// Prefers `GET /gps/maps/planned-route` (server key + server memory cache).
/// Falls back to a single client Routes call only when that fails and
/// `--dart-define=GOOGLE_MAPS_KEY` is set. Never calls Places or Geocoding.
class TripRouteLoader {
  TripRouteLoader(
    this._dio, {
    String? mapsApiKey,
    Dio? routesClient,
    DateTime Function()? clock,
  })  : mapsApiKey = mapsApiKey ?? AppConfig.googleMapsApiKey,
        _routesClient = routesClient,
        _clock = clock ?? DateTime.now;

  final Dio _dio;
  final String mapsApiKey;
  final Dio? _routesClient;
  final DateTime Function() _clock;

  static final Map<String, TripDrivingRoute> _memory = {};
  static final Map<String, DateTime> _memoryAt = {};
  static final Map<String, Future<TripDrivingRoute?>> _inflight = {};

  static TripDrivingRoute? memoryHit(String key, {DateTime? now}) {
    final route = _memory[key];
    final at = _memoryAt[key];
    if (route == null || at == null) return null;
    if ((now ?? DateTime.now()).difference(at) > tripRouteCacheTtl) {
      _memory.remove(key);
      _memoryAt.remove(key);
      return null;
    }
    return route;
  }

  @visibleForTesting
  static void clearMemory() {
    _memory.clear();
    _memoryAt.clear();
    _inflight.clear();
  }

  Future<TripDrivingRoute?> load({
    required double originLat,
    required double originLng,
    required double destLat,
    required double destLng,
  }) {
    final key = tripRouteCacheKey(originLat, originLng, destLat, destLng);
    final cached = memoryHit(key, now: _clock());
    if (cached != null) return Future<TripDrivingRoute?>.value(cached);

    final pending = _inflight[key];
    if (pending != null) return pending;

    final future = _load(key, originLat, originLng, destLat, destLng);
    _inflight[key] = future;
    return future.whenComplete(() => _inflight.remove(key));
  }

  Future<TripDrivingRoute?> _load(
    String key,
    double originLat,
    double originLng,
    double destLat,
    double destLng,
  ) async {
    final stored = await _readHive(key);
    if (stored != null) {
      _remember(key, stored);
      return stored;
    }

    final remote = await _fromBackend(originLat, originLng, destLat, destLng) ??
        await _fromClient(originLat, originLng, destLat, destLng);
    if (remote == null) return null;
    _remember(key, remote);
    await _writeHive(key, remote);
    return remote;
  }

  void _remember(String key, TripDrivingRoute route) {
    _memory[key] = route;
    _memoryAt[key] = _clock();
  }

  Future<TripDrivingRoute?> _fromBackend(
    double originLat,
    double originLng,
    double destLat,
    double destLng,
  ) async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.gpsPlannedRoute,
        queryParameters: {
          'originLat': originLat,
          'originLng': originLng,
          'destLat': destLat,
          'destLng': destLng,
        },
      );
      final data = ApiResponseParser.dataMap(res.data);
      return parsePlannedRouteData(data);
    } catch (e) {
      debugPrint('[TripRoute] planned-route unavailable: $e');
      return null;
    }
  }

  Future<TripDrivingRoute?> _fromClient(
    double originLat,
    double originLng,
    double destLat,
    double destLng,
  ) async {
    final key = mapsApiKey.trim();
    if (key.isEmpty) return null;
    try {
      final client = _routesClient ??
          Dio(BaseOptions(
            connectTimeout: const Duration(seconds: 12),
            receiveTimeout: const Duration(seconds: 12),
          ));
      final res = await client.post<Map<String, dynamic>>(
        'https://routes.googleapis.com/directions/v2:computeRoutes',
        data: {
          'origin': {
            'location': {
              'latLng': {'latitude': originLat, 'longitude': originLng},
            },
          },
          'destination': {
            'location': {
              'latLng': {'latitude': destLat, 'longitude': destLng},
            },
          },
          'travelMode': 'DRIVE',
          'routingPreference': 'TRAFFIC_AWARE',
          'polylineEncoding': 'ENCODED_POLYLINE',
          'polylineQuality': 'OVERVIEW',
        },
        options: Options(headers: {
          'X-Goog-Api-Key': key,
          'X-Goog-FieldMask':
              'routes.duration,routes.staticDuration,routes.distanceMeters,routes.polyline.encodedPolyline',
        }),
      );
      final body = res.data;
      if (body == null) return null;
      return parseRoutesComputePayload(body);
    } catch (e) {
      debugPrint('[TripRoute] client Routes fallback failed: $e');
      return null;
    }
  }

  Future<TripDrivingRoute?> _readHive(String key) async {
    try {
      final box = await Hive.openBox(_hiveBoxName);
      final raw = box.get(key);
      if (raw is! Map) return null;
      final savedAtMs = raw['at'];
      if (savedAtMs is! int) return null;
      final savedAt =
          DateTime.fromMillisecondsSinceEpoch(savedAtMs, isUtc: true);
      if (_clock().toUtc().difference(savedAt) > tripRouteCacheTtl) {
        await box.delete(key);
        return null;
      }
      final encoded = raw['encoded'];
      if (encoded is! String) return null;
      return parsePlannedRouteData({
        'encodedPolyline': encoded,
        'distanceMeters': raw['meters'],
        'durationSeconds': raw['seconds'],
      });
    } catch (e) {
      debugPrint('[TripRoute] local cache read skipped: $e');
      return null;
    }
  }

  Future<void> _writeHive(String key, TripDrivingRoute route) async {
    try {
      final box = await Hive.openBox(_hiveBoxName);
      await box.put(key, {
        'encoded': route.encodedPolyline,
        'meters': route.distanceMeters,
        'seconds': route.durationSeconds,
        'at': _clock().toUtc().millisecondsSinceEpoch,
      });
    } catch (e) {
      debugPrint('[TripRoute] local cache write skipped: $e');
    }
  }
}
