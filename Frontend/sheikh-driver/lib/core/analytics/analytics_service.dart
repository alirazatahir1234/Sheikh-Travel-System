import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:flutter/foundation.dart';

/// Thin Analytics wrapper — no-ops when Firebase is unavailable.
class AnalyticsService {
  AnalyticsService._();
  static final instance = AnalyticsService._();

  FirebaseAnalytics? _analytics;
  bool _ready = false;

  Future<void> init() async {
    try {
      _analytics = FirebaseAnalytics.instance;
      const enable = !kDebugMode ||
          bool.fromEnvironment('ENABLE_ANALYTICS_DEBUG', defaultValue: false);
      await _analytics!.setAnalyticsCollectionEnabled(enable);
      _ready = enable;
    } catch (e) {
      debugPrint('[Analytics] Init skipped: $e');
      _ready = false;
    }
  }

  Future<void> setDriverId(int? driverId) async {
    if (!_ready || _analytics == null) return;
    try {
      await _analytics!.setUserId(id: driverId?.toString());
    } catch (_) {}
  }

  Future<void> log(String name, [Map<String, Object>? params]) async {
    if (!_ready || _analytics == null) return;
    try {
      await _analytics!.logEvent(name: name, parameters: params);
    } catch (e) {
      debugPrint('[Analytics] $name failed: $e');
    }
  }

  Future<void> loginSuccess() => log('login');
  Future<void> logout() => log('logout');
  Future<void> tripAction(String action, {int? tripId}) => log(
        'trip_action',
        {
          'action': action,
          if (tripId != null) 'trip_id': tripId,
        },
      );
  Future<void> sosSent() => log('sos_sent');
  Future<void> screenView(String name) => log('screen_view', {'screen': name});
}
