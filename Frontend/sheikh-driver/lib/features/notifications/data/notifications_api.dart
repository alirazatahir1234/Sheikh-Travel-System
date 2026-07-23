import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../../auth/data/auth_repository.dart';
import '../domain/notification_models.dart';

final notificationsApiProvider = Provider<NotificationsApi>((ref) {
  return NotificationsApi(
    ref.read(dioProvider),
    () => ref.read(fleetSessionProvider)?.isDriverOnly ?? true,
  );
});

class NotificationsApi {
  NotificationsApi(this._dio, this._isDriverOnly);
  final Dio _dio;
  final bool Function() _isDriverOnly;

  bool get _driver => _isDriverOnly();

  String get _listPath =>
      _driver ? ApiEndpoints.notifications : ApiEndpoints.staffNotifications;
  String get _unreadPath => _driver
      ? ApiEndpoints.notificationsUnreadCount
      : ApiEndpoints.staffNotificationsUnreadCount;
  String get _readPath =>
      _driver ? ApiEndpoints.notificationsRead : ApiEndpoints.staffNotificationsRead;
  String get _archivePath => _driver
      ? ApiEndpoints.notificationsArchive
      : ApiEndpoints.staffNotificationsArchive;
  String get _restorePath => _driver
      ? ApiEndpoints.notificationsRestore
      : ApiEndpoints.staffNotificationsRestore;
  String _byId(int id) => _driver
      ? ApiEndpoints.notificationById(id)
      : ApiEndpoints.staffNotificationById(id);

  Future<List<AppNotification>> list({
    bool archived = false,
    bool? unreadOnly,
    String? module,
    int page = 1,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      _listPath,
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
    final res = await _dio.get<Map<String, dynamic>>(_unreadPath);
    final data = res.data?['data'];
    if (data is int) return data;
    if (data is num) return data.toInt();
    return 0;
  }

  Future<void> markRead(List<int>? ids) async {
    await _dio.put(_readPath, data: ids);
  }

  Future<void> archive(List<int> ids) async {
    await _dio.post(_archivePath, data: {'ids': ids});
  }

  Future<void> restore(List<int> ids) async {
    await _dio.post(_restorePath, data: {'ids': ids});
  }

  Future<void> delete(int id) async {
    await _dio.delete(_byId(id));
  }
}
