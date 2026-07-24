import 'package:hive_flutter/hive_flutter.dart';

const _tripsCacheBox = 'trips_cache';

/// Last-known trip list for offline viewing.
class TripsCache {
  static Box? _box;

  static Future<void> init() async {
    _box = await Hive.openBox(_tripsCacheBox);
  }

  static Future<void> save(List<Map<String, dynamic>> trips) async {
    await _box?.put('items', trips);
    await _box?.put('savedAt', DateTime.now().toUtc().toIso8601String());
  }

  static List<Map<String, dynamic>> load() {
    final raw = _box?.get('items');
    if (raw is! List) return [];
    return raw
        .whereType<Map>()
        .map((e) => Map<String, dynamic>.from(e))
        .toList();
  }

  static DateTime? get savedAt {
    final s = _box?.get('savedAt')?.toString();
    return s == null ? null : DateTime.tryParse(s);
  }
}
