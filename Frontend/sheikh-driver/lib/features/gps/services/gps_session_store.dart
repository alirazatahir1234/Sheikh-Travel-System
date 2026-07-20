import 'package:hive_flutter/hive_flutter.dart';

const _boxName = 'gps_session';

/// Persists an active background tracking session across app restarts.
class GpsSessionStore {
  static Box? _box;

  static Future<void> init() async {
    _box = await Hive.openBox(_boxName);
  }

  static bool get isActive => _box?.get('active', defaultValue: false) == true;

  static int? get vehicleId {
    final v = _box?.get('vehicleId');
    return v is int ? v : int.tryParse(v?.toString() ?? '');
  }

  static int? get bookingId {
    final v = _box?.get('bookingId');
    return v is int ? v : int.tryParse(v?.toString() ?? '');
  }

  static DateTime? get startedAt {
    final s = _box?.get('startedAt')?.toString();
    return s == null ? null : DateTime.tryParse(s);
  }

  static Future<void> save({
    required int vehicleId,
    int? bookingId,
  }) async {
    await _box?.putAll({
      'active': true,
      'vehicleId': vehicleId,
      'bookingId': bookingId,
      'startedAt': DateTime.now().toUtc().toIso8601String(),
    });
  }

  static Future<void> clear() async {
    await _box?.clear();
  }
}
