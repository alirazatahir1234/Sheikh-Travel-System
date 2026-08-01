import 'dart:io' show Platform;
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:geolocator/geolocator.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'package:workmanager/workmanager.dart';
import '../../../core/config/app_config.dart';
import 'gps_session_store.dart';
import 'location_queue.dart';

const _drainTaskName = 'gps_queue_drain';
const _drainTaskTag = 'gps_drain';
const _heartbeatTaskName = 'gps_heartbeat';
const _heartbeatTaskTag = 'gps_heartbeat';

// Called from Workmanager's isolate — must be a top-level function.
@pragma('vm:entry-point')
void callbackDispatcher() {
  Workmanager().executeTask((task, inputData) async {
    if (task == _drainTaskName || task == _heartbeatTaskName) {
      await _backgroundGpsMaintenance(captureFix: task == _heartbeatTaskName);
    }
    return true;
  });
}

Future<void> _backgroundGpsMaintenance({required bool captureFix}) async {
  await Hive.initFlutter();
  await LocationQueue.init();
  await GpsSessionStore.init();

  const storage = FlutterSecureStorage();
  final token = await storage.read(key: 'driver_access_token');
  if (token == null) return;

  final dio = Dio(BaseOptions(
    baseUrl: AppConfig.resolvedBaseUrl,
    headers: {
      'Authorization': 'Bearer $token',
      'X-Tenant-Slug': AppConfig.tenantSlug,
      'Content-Type': 'application/json',
    },
    connectTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 10),
  ));

  // Presence heartbeat keeps dispatcher “mobile online”.
  try {
    await dio.post('/ai/presence/mobile-heartbeat');
  } catch (_) {}

  if (captureFix && GpsSessionStore.isActive) {
    final vehicleId = GpsSessionStore.vehicleId;
    if (vehicleId != null) {
      try {
        final pos = await Geolocator.getCurrentPosition(
          desiredAccuracy: LocationAccuracy.medium,
          timeLimit: const Duration(seconds: 15),
        );
        await LocationQueue.enqueue(QueuedLocation(
          vehicleId: vehicleId,
          lat: pos.latitude,
          lng: pos.longitude,
          speed: pos.speed * 3.6,
          ts: pos.timestamp.millisecondsSinceEpoch,
          bookingId: GpsSessionStore.bookingId,
        ));
      } catch (_) {}
    }
  }

  if (LocationQueue.isEmpty) return;

  try {
    final positions = LocationQueue.getAll();
    await dio.post('/driver-app/location/batch', data: {
      'positions': positions
          .map((p) => {
                'vehicleId': p.vehicleId,
                'latitude': p.lat,
                'longitude': p.lng,
                'speed': p.speed,
                if (p.bookingId != null) 'bookingId': p.bookingId,
              })
          .toList(),
    });
    await LocationQueue.clear();
  } catch (_) {
    // Leave queue intact — will retry on next task run
  }
}

class GpsBackgroundService {
  /// Workmanager only ships Android/iOS implementations.
  static bool get _supported {
    if (kIsWeb) return false;
    try {
      return Platform.isAndroid || Platform.isIOS;
    } catch (_) {
      return false;
    }
  }

  static Future<void> initialize() async {
    if (!_supported) {
      debugPrint('[GPS] Workmanager skipped on this platform');
      return;
    }
    try {
      await Workmanager().initialize(callbackDispatcher);
    } catch (e) {
      debugPrint('[GPS] Workmanager init skipped: $e');
    }
  }

  /// Register periodic drain + heartbeat tasks (Android ~15 min minimum).
  static Future<void> registerDrainTask() async {
    if (!_supported) return;
    try {
      await Workmanager().registerPeriodicTask(
        _drainTaskTag,
        _drainTaskName,
        frequency: const Duration(minutes: 15),
        constraints: Constraints(networkType: NetworkType.connected),
        existingWorkPolicy: ExistingPeriodicWorkPolicy.keep,
      );
      await Workmanager().registerPeriodicTask(
        _heartbeatTaskTag,
        _heartbeatTaskName,
        frequency: const Duration(minutes: 15),
        constraints: Constraints(networkType: NetworkType.connected),
        existingWorkPolicy: ExistingPeriodicWorkPolicy.keep,
      );
    } catch (e) {
      debugPrint('[GPS] Workmanager register skipped: $e');
    }
  }

  static Future<void> cancelDrainTask() async {
    if (!_supported) return;
    try {
      await Workmanager().cancelByTag(_drainTaskTag);
      await Workmanager().cancelByTag(_heartbeatTaskTag);
    } catch (_) {}
  }
}
