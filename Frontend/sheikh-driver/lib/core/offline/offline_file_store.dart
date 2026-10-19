import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:path_provider/path_provider.dart';

/// Copies transient picker/camera files into app documents so offline sync
/// still has them after the OS clears cache/temp paths.
class OfflineFileStore {
  OfflineFileStore._();

  static const _subdir = 'offline_attachments';

  static Future<Directory> _dir() async {
    final root = await getApplicationDocumentsDirectory();
    final dir = Directory('${root.path}${Platform.pathSeparator}$_subdir');
    if (!await dir.exists()) {
      await dir.create(recursive: true);
    }
    return dir;
  }

  /// Persist [sourcePaths] into documents; skips missing files.
  /// Returns durable paths in the same order as successfully copied sources.
  static Future<List<String>> persistForOffline(List<String> sourcePaths) async {
    if (sourcePaths.isEmpty) return const [];
    try {
      final dir = await _dir();
      final out = <String>[];
      for (final src in sourcePaths) {
        final file = File(src);
        if (!await file.exists()) {
          debugPrint('[OfflineFileStore] Missing source: $src');
          continue;
        }
        // Already under our durable folder — reuse.
        if (src.contains('$_subdir${Platform.pathSeparator}') ||
            src.contains('/$_subdir/')) {
          out.add(src);
          continue;
        }
        final base = src.split(Platform.pathSeparator).last;
        final safe = base.isEmpty ? 'file.bin' : base;
        final destPath =
            '${dir.path}${Platform.pathSeparator}${DateTime.now().microsecondsSinceEpoch}_$safe';
        await file.copy(destPath);
        out.add(destPath);
      }
      return out;
    } catch (e) {
      debugPrint('[OfflineFileStore] Persist failed: $e');
      return sourcePaths.where((p) => File(p).existsSync()).toList();
    }
  }

  /// Rewrites known payload path fields from [original] → [durable] mapping.
  static Map<String, dynamic> rewritePayloadPaths(
    Map<String, dynamic> payload,
    List<String> original,
    List<String> durable,
  ) {
    if (original.isEmpty || durable.isEmpty) {
      return Map<String, dynamic>.from(payload);
    }
    final map = Map<String, dynamic>.from(payload);
    String remap(String path) {
      final i = original.indexOf(path);
      if (i >= 0 && i < durable.length) return durable[i];
      return path;
    }

    for (final key in ['signaturePath', 'filePath', 'receiptPath']) {
      final v = map[key];
      if (v is String && v.isNotEmpty) map[key] = remap(v);
    }
    final photos = map['photoPaths'];
    if (photos is List) {
      map['photoPaths'] = photos.map((e) => remap(e.toString())).toList();
    }
    return map;
  }
}
