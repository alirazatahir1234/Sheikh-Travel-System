import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'package:workmanager/workmanager.dart';
import 'package:dio/dio.dart';
import '../../../core/config/app_config.dart';
import 'location_queue.dart';

const _drainTaskName = 'gps_queue_drain';
const _drainTaskTag = 'gps_drain';

// Called from Workmanager's isolate — must be a top-level function.
@pragma('vm:entry-point')
void callbackDispatcher() {
  Workmanager().executeTask((task, inputData) async {
    if (task == _drainTaskName) {
      await _drainQueue();
    }
    return true;
  });
}

Future<void> _drainQueue() async {
  await Hive.initFlutter();
  await LocationQueue.init();

  if (LocationQueue.isEmpty) return;

  const storage = FlutterSecureStorage();
  final token = await storage.read(key: 'driver_access_token');
  if (token == null) return;

  final dio = Dio(BaseOptions(
    baseUrl: AppConfig.baseUrl,
    headers: {
      'Authorization': 'Bearer $token',
      'X-Tenant-Slug': AppConfig.tenantSlug,
      'Content-Type': 'application/json',
    },
    connectTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 10),
  ));

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
  static Future<void> initialize() async {
    await Workmanager().initialize(callbackDispatcher);
  }

  /// Register a periodic drain task — runs when device is online.
  static Future<void> registerDrainTask() async {
    await Workmanager().registerPeriodicTask(
      _drainTaskTag,
      _drainTaskName,
      frequency: const Duration(minutes: 15),
      constraints: Constraints(networkType: NetworkType.connected),
      existingWorkPolicy: ExistingPeriodicWorkPolicy.keep,
    );
  }

  static Future<void> cancelDrainTask() async {
    await Workmanager().cancelByTag(_drainTaskTag);
  }
}
