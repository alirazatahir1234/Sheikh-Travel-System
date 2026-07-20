import 'dart:math';
import 'package:hive_flutter/hive_flutter.dart';
import 'offline_models.dart';

const _boxName = 'offline_outbox';

class OfflineOutbox {
  static Box<Map>? _box;

  static Future<void> init() async {
    _box = await Hive.openBox<Map>(_boxName);
  }

  static String newId() {
    final r = Random.secure().nextInt(1 << 32);
    return '${DateTime.now().microsecondsSinceEpoch}_$r';
  }

  static Future<OfflineOperation> enqueue({
    required OfflineOpType type,
    required Map<String, dynamic> payload,
    List<String> filePaths = const [],
  }) async {
    final op = OfflineOperation(
      id: newId(),
      type: type,
      payload: payload,
      filePaths: filePaths,
      createdAt: DateTime.now().toUtc(),
    );
    await _box?.put(op.id, op.toMap());
    return op;
  }

  static List<OfflineOperation> pending() {
    final box = _box;
    if (box == null) return [];
    return box.values
        .map(OfflineOperation.fromMap)
        .where((o) =>
            o.status == OfflineOpStatus.pending ||
            o.status == OfflineOpStatus.failed ||
            o.status == OfflineOpStatus.syncing)
        .toList()
      ..sort((a, b) => a.createdAt.compareTo(b.createdAt));
  }

  static List<OfflineOperation> all() {
    final box = _box;
    if (box == null) return [];
    return box.values.map(OfflineOperation.fromMap).toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
  }

  static int get length => pending().length;

  static Future<void> update(OfflineOperation op) async {
    await _box?.put(op.id, op.toMap());
  }

  static Future<void> remove(String id) async {
    await _box?.delete(id);
  }

  static Future<void> clearResolved() async {
    final box = _box;
    if (box == null) return;
    final keys = <dynamic>[];
    for (final entry in box.toMap().entries) {
      final op = OfflineOperation.fromMap(entry.value);
      if (op.status == OfflineOpStatus.conflict) keys.add(entry.key);
    }
    await box.deleteAll(keys);
  }
}
