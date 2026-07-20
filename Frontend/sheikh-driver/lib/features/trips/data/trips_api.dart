import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/offline/connectivity_provider.dart';
import '../../../core/offline/offline_models.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../../../core/offline/trips_cache.dart';
import '../../../core/api/api_response.dart';
import '../../../core/analytics/analytics_service.dart';
import '../domain/trip_model.dart';

final tripsApiProvider =
    Provider<TripsApi>((ref) => TripsApi(ref.read(dioProvider), ref));

class TripsApi {
  TripsApi(this._dio, this._ref);
  final Dio _dio;
  final Ref _ref;

  Future<List<Trip>> getTrips() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.trips);
      final maps = ApiResponseParser.dataList(res.data);
      await TripsCache.save(maps);
      return maps.map(Trip.fromJson).toList();
    } catch (e) {
      if (!isOfflineDioError(e)) rethrow;
      final cached = TripsCache.load();
      if (cached.isEmpty) rethrow;
      return cached.map(Trip.fromJson).toList();
    }
  }

  Future<void> advance(int id, String action, {String? reason}) async {
    await _ref.read(offlineSyncProvider).runOrQueue(
          online: () async {
            final path = switch (action) {
              'Accept' => ApiEndpoints.acceptTrip(id),
              'Arrived' => ApiEndpoints.arrivedTrip(id),
              'Onboard' => ApiEndpoints.onboardTrip(id),
              'Complete' => ApiEndpoints.completeTrip(id),
              'Reject' => ApiEndpoints.rejectTrip(id),
              _ => throw ArgumentError('Unknown action: $action'),
            };
            if (action == 'Reject') {
              final res = await _dio.post<Map<String, dynamic>>(path, data: reason ?? '');
              ApiResponseParser.ensureSuccess(res.data);
            } else {
              final res = await _dio.post<Map<String, dynamic>>(path);
              ApiResponseParser.ensureSuccess(res.data);
            }
            // ignore: unawaited_futures
            AnalyticsService.instance.tripAction(action, tripId: id);
          },
          type: OfflineOpType.tripAdvance,
          payload: {
            'tripId': id,
            'action': action,
            if (reason != null) 'reason': reason,
          },
        );
  }

  Future<void> startTrip(int id) => advance(id, 'Accept');
  Future<void> completeTrip(int id) => advance(id, 'Complete');
  Future<void> rejectTrip(int id, String reason) =>
      advance(id, 'Reject', reason: reason);

  Future<void> postLocation({
    required int vehicleId,
    required double lat,
    required double lng,
    double speed = 0,
    int? bookingId,
  }) async {
    await _dio.post(ApiEndpoints.tripLocation, data: {
      'vehicleId': vehicleId,
      'latitude': lat,
      'longitude': lng,
      'speed': speed,
      if (bookingId != null) 'bookingId': bookingId,
    });
  }
}
