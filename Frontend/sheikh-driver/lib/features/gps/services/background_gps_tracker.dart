import 'dart:async';
import 'dart:io';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:geolocator/geolocator.dart';
import 'package:permission_handler/permission_handler.dart';
import '../../../core/api/api_endpoints.dart';
import 'gps_session_store.dart';
import 'location_queue.dart';

/// Continuous GPS tracking with:
/// - Android foreground service notification (GeolocatorLocationService)
/// - iOS background location updates
/// - Offline Hive queue + batch drain
/// - Mobile presence heartbeat
/// - Battery-aware distance / interval adaptation
class BackgroundGpsTracker extends ChangeNotifier {
  BackgroundGpsTracker._();
  static final instance = BackgroundGpsTracker._();

  Dio? _dio;
  StreamSubscription<Position>? _posSub;
  StreamSubscription<List<ConnectivityResult>>? _connectivitySub;
  Timer? _heartbeatTimer;
  Timer? _adaptTimer;

  bool _tracking = false;
  bool _online = true;
  bool _powerSave = false;
  Position? _lastPosition;
  int _queuedCount = 0;
  String? _lastError;
  DateTime? _lastHeartbeatAt;
  DateTime? _lastPostedAt;

  bool get isTracking => _tracking;
  bool get isOnline => _online;
  bool get powerSave => _powerSave;
  Position? get lastPosition => _lastPosition;
  int get queuedCount => _queuedCount;
  String? get lastError => _lastError;
  DateTime? get lastHeartbeatAt => _lastHeartbeatAt;

  void bindDio(Dio dio) => _dio = dio;

  Future<String?> ensurePermissions() async {
    var whenInUse = await Permission.locationWhenInUse.request();
    if (!whenInUse.isGranted) {
      return 'Location permission is required for live tracking.';
    }

    // Background / always — required for tracking with screen off.
    if (Platform.isAndroid || Platform.isIOS) {
      var always = await Permission.locationAlways.status;
      if (!always.isGranted) {
        always = await Permission.locationAlways.request();
        if (!always.isGranted) {
          return 'Allow “Always” location so tracking continues in the background.';
        }
      }
    }

    if (Platform.isAndroid) {
      final notif = await Permission.notification.request();
      if (!notif.isGranted) {
        debugPrint('[GPS] Notification permission denied — FGS may be limited');
      }
      // Battery optimization exemption (best-effort).
      final batt = await Permission.ignoreBatteryOptimizations.status;
      if (!batt.isGranted) {
        await Permission.ignoreBatteryOptimizations.request();
      }
    }

    final service = await Geolocator.isLocationServiceEnabled();
    if (!service) return 'Turn on device location services.';
    return null;
  }

  /// Start background-capable tracking for an assigned vehicle/trip.
  Future<String?> start({
    required int vehicleId,
    int? bookingId,
    Dio? dio,
  }) async {
    if (dio != null) _dio = dio;
    final err = await ensurePermissions();
    if (err != null) return err;

    await stop(clearSession: false);
    await GpsSessionStore.save(vehicleId: vehicleId, bookingId: bookingId);

    _watchConnectivity();
    await _startPositionStream();
    _startHeartbeat();
    _startAdaptLoop();

    _tracking = true;
    _lastError = null;
    _queuedCount = LocationQueue.length;
    notifyListeners();
    await _sendHeartbeat();
    return null;
  }

  Future<void> stop({bool clearSession = true}) async {
    await _posSub?.cancel();
    _posSub = null;
    await _connectivitySub?.cancel();
    _connectivitySub = null;
    _heartbeatTimer?.cancel();
    _heartbeatTimer = null;
    _adaptTimer?.cancel();
    _adaptTimer = null;

    if (clearSession) await GpsSessionStore.clear();

    _tracking = false;
    _lastPosition = null;
    notifyListeners();
  }

  /// Resume from Hive session after app restart / login.
  Future<void> resumeIfNeeded({Dio? dio}) async {
    if (!GpsSessionStore.isActive) return;
    final vehicleId = GpsSessionStore.vehicleId;
    if (vehicleId == null) {
      await GpsSessionStore.clear();
      return;
    }
    if (dio != null) _dio = dio;
    if (_tracking) return;
    await start(
      vehicleId: vehicleId,
      bookingId: GpsSessionStore.bookingId,
      dio: _dio,
    );
  }

  void _watchConnectivity() {
    _connectivitySub?.cancel();
    _connectivitySub = Connectivity().onConnectivityChanged.listen((results) {
      final online =
          results.isNotEmpty && !results.every((r) => r == ConnectivityResult.none);
      _online = online;
      notifyListeners();
      if (online && LocationQueue.length > 0) {
        unawaited(_drainQueue());
      }
    });
  }

  Future<void> _startPositionStream() async {
    await _posSub?.cancel();
    _posSub = Geolocator.getPositionStream(
      locationSettings: _buildSettings(powerSave: _powerSave),
    ).listen(
      (pos) async {
        _lastPosition = pos;
        notifyListeners();
        await _postOrQueue(pos);
      },
      onError: (e) {
        _lastError = e.toString();
        notifyListeners();
        debugPrint('[GPS] stream error: $e');
      },
    );
  }

  LocationSettings _buildSettings({required bool powerSave}) {
    final distance = powerSave ? 40 : 10;
    final interval = Duration(seconds: powerSave ? 25 : 8);

    if (Platform.isAndroid) {
      return AndroidSettings(
        accuracy: powerSave ? LocationAccuracy.medium : LocationAccuracy.high,
        distanceFilter: distance,
        intervalDuration: interval,
        foregroundNotificationConfig: const ForegroundNotificationConfig(
          notificationTitle: 'SheikhGo Driver',
          notificationText: 'Sharing live trip location with dispatch',
          notificationChannelName: 'Live Tracking',
          notificationIcon: AndroidResource(name: 'ic_launcher', defType: 'mipmap'),
          enableWakeLock: true,
          enableWifiLock: true,
          setOngoing: true,
        ),
      );
    }

    if (Platform.isIOS) {
      return AppleSettings(
        accuracy: powerSave ? LocationAccuracy.medium : LocationAccuracy.bestForNavigation,
        distanceFilter: distance,
        activityType: ActivityType.automotiveNavigation,
        allowBackgroundLocationUpdates: true,
        showBackgroundLocationIndicator: true,
        pauseLocationUpdatesAutomatically: powerSave,
      );
    }

    return LocationSettings(
      accuracy: LocationAccuracy.high,
      distanceFilter: distance,
    );
  }

  void _startHeartbeat() {
    _heartbeatTimer?.cancel();
    _heartbeatTimer = Timer.periodic(const Duration(seconds: 60), (_) {
      unawaited(_sendHeartbeat());
    });
  }

  void _startAdaptLoop() {
    _adaptTimer?.cancel();
    _adaptTimer = Timer.periodic(const Duration(seconds: 45), (_) async {
      final pos = _lastPosition;
      if (pos == null || !_tracking) return;
      final speedKmh = pos.speed * 3.6;
      // Stationary / slow → power save; moving → high fidelity.
      final wantSave = speedKmh < 3;
      if (wantSave != _powerSave) {
        _powerSave = wantSave;
        notifyListeners();
        await _startPositionStream();
        debugPrint('[GPS] powerSave=$_powerSave (speed=${speedKmh.toStringAsFixed(1)} km/h)');
      }
    });
  }

  Future<void> _sendHeartbeat() async {
    final dio = _dio;
    if (dio == null || !_online) return;
    try {
      await dio.post(ApiEndpoints.mobileHeartbeat);
      _lastHeartbeatAt = DateTime.now();
      notifyListeners();
    } catch (e) {
      debugPrint('[GPS] heartbeat failed: $e');
    }
  }

  Future<void> _postOrQueue(Position pos) async {
    final vehicleId = GpsSessionStore.vehicleId;
    if (vehicleId == null) return;

    final loc = QueuedLocation(
      vehicleId: vehicleId,
      lat: pos.latitude,
      lng: pos.longitude,
      speed: pos.speed * 3.6,
      ts: pos.timestamp.millisecondsSinceEpoch,
      bookingId: GpsSessionStore.bookingId,
    );

    // Throttle posts when power-saving (still queue every fix).
    final minGap = _powerSave ? const Duration(seconds: 20) : const Duration(seconds: 5);
    final canPost = _lastPostedAt == null ||
        DateTime.now().difference(_lastPostedAt!) >= minGap;

    if (_online && canPost) {
      try {
        if (LocationQueue.length > 0) await _drainQueue();
        final dio = _dio;
        if (dio == null) throw StateError('Dio not bound');
        await dio.post(ApiEndpoints.tripLocation, data: {
          'vehicleId': loc.vehicleId,
          'latitude': loc.lat,
          'longitude': loc.lng,
          'speed': loc.speed,
          if (loc.bookingId != null) 'bookingId': loc.bookingId,
        });
        _lastPostedAt = DateTime.now();
        _lastError = null;
        return;
      } catch (e) {
        _lastError = e.toString();
      }
    }

    await LocationQueue.enqueue(loc);
    _queuedCount = LocationQueue.length;
    notifyListeners();
  }

  Future<void> _drainQueue() async {
    final positions = LocationQueue.getAll();
    if (positions.isEmpty) return;
    final dio = _dio;
    if (dio == null) return;
    try {
      await dio.post(ApiEndpoints.tripLocationBatch, data: {
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
      _queuedCount = 0;
      notifyListeners();
    } catch (e) {
      debugPrint('[GPS] drain failed: $e');
    }
  }

  Future<void> drainNow() => _drainQueue();
}
