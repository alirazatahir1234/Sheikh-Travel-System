import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../domain/notification_models.dart';

final notificationsApiProvider =
    Provider<NotificationsApi>((ref) => NotificationsApi(ref.read(dioProvider)));

class NotificationsApi {
  NotificationsApi(this._dio);
  final Dio _dio;

  Future<List<AppNotification>> list({
    bool archived = false,
    bool? unreadOnly,
    String? module,
    int page = 1,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.notifications,
      queryParameters: {
        'page': page,
        'pageSize': 50,
        'archived': archived,
        if (unreadOnly != null) 'unreadOnly': unreadOnly,
        if (module != null && module.isNotEmpty) 'module': module,
      },
    );
    final data = res.data?['data'];
    List list;
    if (data is Map<String, dynamic>) {
      list = (data['items'] as List?) ?? [];
    } else if (data is List) {
      list = data;
    } else {
      list = const [];
    }
    return list.cast<Map<String, dynamic>>().map(AppNotification.fromJson).toList();
  }

  Future<int> unreadCount() async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.notificationsUnreadCount,
    );
    final data = res.data?['data'];
    if (data is int) return data;
    if (data is num) return data.toInt();
    return 0;
  }

  Future<void> markRead(List<int>? ids) async {
    await _dio.put(ApiEndpoints.notificationsRead, data: ids);
  }

  Future<void> archive(List<int> ids) async {
    await _dio.post(ApiEndpoints.notificationsArchive, data: {'ids': ids});
  }

  Future<void> restore(List<int> ids) async {
    await _dio.post(ApiEndpoints.notificationsRestore, data: {'ids': ids});
  }

  Future<void> delete(int id) async {
    await _dio.delete(ApiEndpoints.notificationById(id));
  }
}
