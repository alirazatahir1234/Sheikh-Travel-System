import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/booking_models.dart';

final bookingsApiProvider =
    Provider<BookingsApi>((ref) => BookingsApi(ref.read(dioProvider)));

class BookingsApi {
  BookingsApi(this._dio);
  final Dio _dio;

  Future<List<BookingListItem>> list({
    String? status,
    String? search,
    int page = 1,
    int pageSize = 100,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.bookings,
      queryParameters: {
        'page': page,
        'pageSize': pageSize,
        if (status != null && status.isNotEmpty) 'status': status,
        if (search != null && search.isNotEmpty) 'search': search,
      },
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(BookingListItem.fromJson)
        .toList();
  }

  Future<BookingDetail> getById(int id) async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.bookingById(id));
    return BookingDetail.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<void> assignDriver(int id, int driverId) async {
    final res = await _dio.put<Map<String, dynamic>>(
      ApiEndpoints.bookingAssignDriver(id),
      data: {'driverId': driverId},
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<void> assignVehicle(int id, int vehicleId) async {
    final res = await _dio.put<Map<String, dynamic>>(
      ApiEndpoints.bookingAssignVehicle(id),
      data: {'vehicleId': vehicleId},
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<void> updateStatus(
    int id,
    String status, {
    String? cancellationReason,
  }) async {
    final res = await _dio.put<Map<String, dynamic>>(
      ApiEndpoints.bookingStatus(id),
      data: {
        'status': status,
        if (cancellationReason != null) 'cancellationReason': cancellationReason,
      },
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<int> createTripFromBooking(int bookingId) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.opsTripFromBooking(bookingId),
    );
    ApiResponseParser.ensureSuccess(res.data);
    final data = res.data?['data'];
    if (data is int) return data;
    if (data is num) return data.toInt();
    if (data is Map) {
      return data['id'] as int? ?? data['Id'] as int? ?? 0;
    }
    return 0;
  }
}
