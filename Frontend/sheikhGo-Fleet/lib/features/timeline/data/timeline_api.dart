import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../domain/timeline_event_model.dart';

final timelineApiProvider = Provider<TimelineApi>((ref) => TimelineApi(ref.read(dioProvider)));

class TimelineApi {
  TimelineApi(this._dio);
  final Dio _dio;

  Future<List<TimelineEvent>> getTimeline({int page = 1, int pageSize = 50}) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.timeline,
      queryParameters: {'page': page, 'pageSize': pageSize},
    );
    final body = res.data;
    final list = (body?['data'] as List?) ?? (body?['items'] as List?) ?? [];
    return list.cast<Map<String, dynamic>>().map(TimelineEvent.fromJson).toList();
  }
}
