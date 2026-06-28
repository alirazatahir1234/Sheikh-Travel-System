import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../domain/trip_model.dart';

final tripsApiProvider = Provider<TripsApi>((ref) => TripsApi(ref.read(dioProvider)));

class TripsApi {
  TripsApi(this._dio);
  final Dio _dio;

  Future<List<Trip>> getTrips() async {
    final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.trips);
    final body = res.data;
    final list = (body?['data'] as List?) ?? (body as List?) ?? [];
    return list
        .cast<Map<String, dynamic>>()
        .map(Trip.fromJson)
        .toList();
  }

  Future<void> startTrip(int id) async {
    await _dio.post(ApiEndpoints.startTrip(id));
  }

  Future<void> completeTrip(int id) async {
    await _dio.post(ApiEndpoints.completeTrip(id));
  }

  Future<void> rejectTrip(int id, String reason) async {
    await _dio.post(ApiEndpoints.rejectTrip(id), data: reason);
  }

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
