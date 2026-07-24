import 'package:hive_flutter/hive_flutter.dart';

const _boxName = 'gps_queue';

class QueuedLocation {
  QueuedLocation({
    required this.vehicleId,
    required this.lat,
    required this.lng,
    required this.speed,
    required this.ts,
    this.bookingId,
  });

  final int vehicleId;
  final double lat;
  final double lng;
  final double speed;
  final int ts; // epoch millis
  final int? bookingId;

  Map<String, dynamic> toMap() => {
        'vehicleId': vehicleId,
        'lat': lat,
        'lng': lng,
        'speed': speed,
        'ts': ts,
        if (bookingId != null) 'bookingId': bookingId,
      };

  factory QueuedLocation.fromMap(Map map) => QueuedLocation(
        vehicleId: map['vehicleId'] as int,
        lat: (map['lat'] as num).toDouble(),
        lng: (map['lng'] as num).toDouble(),
        speed: (map['speed'] as num).toDouble(),
        ts: map['ts'] as int,
        bookingId: map['bookingId'] as int?,
      );
}

class LocationQueue {
  static Box<Map>? _box;

  static Future<void> init() async {
    _box = await Hive.openBox<Map>(_boxName);
  }

  static Future<void> enqueue(QueuedLocation loc) async {
    await _box?.add(loc.toMap());
  }

  static List<QueuedLocation> getAll() {
    final box = _box;
    if (box == null) return [];
    return box.values.map(QueuedLocation.fromMap).toList();
  }

  static Future<void> clear() async {
    await _box?.clear();
  }

  static int get length => _box?.length ?? 0;
  static bool get isEmpty => (_box?.length ?? 0) == 0;
}
