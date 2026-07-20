import 'dart:async';
import 'dart:convert';
import 'package:dio/dio.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../../../core/api/dio_client.dart';
import '../domain/auth_models.dart';
import 'auth_api.dart';
import '../../notifications/services/push_registration_service.dart';
import '../../gps/services/background_gps_tracker.dart';
import '../../../core/security/device_registration_service.dart';
import '../../../core/analytics/analytics_service.dart';

const _sessionKey = 'driver_session';
const _accessTokenKey = 'driver_access_token';
const _refreshTokenKey = 'driver_refresh_token';

final authRepositoryProvider = ChangeNotifierProvider<AuthRepository>(
  (ref) => AuthRepository(
    ref.read(secureStorageProvider),
    ref.read(authApiProvider),
    ref.read(dioProvider),
    ref.read(sessionInvalidationProvider),
  ),
);

class AuthRepository extends ChangeNotifier {
  AuthRepository(
    this._storage,
    this._api,
    this._dio,
    this._sessionInvalidation,
  ) {
    _sessionInvalidation.addListener(_onSessionInvalidated);
    _restoreSession();
  }

  final FlutterSecureStorage _storage;
  final AuthApi _api;
  final Dio _dio;
  final SessionInvalidationNotifier _sessionInvalidation;

  DriverSession? _session;
  bool _loading = true;

  DriverSession? get session => _session;
  bool get isLoggedIn => _session != null;
  bool get isLoading => _loading;

  Future<void> _restoreSession() async {
    final raw = await _storage.read(key: _sessionKey);
    if (raw != null) {
      try {
        _session = DriverSession.fromJson(
          jsonDecode(raw) as Map<String, dynamic>,
        );
      } catch (_) {
        await _storage.delete(key: _sessionKey);
      }
    }
    _loading = false;
    notifyListeners();
  }

  void _onSessionInvalidated() {
    unawaited(_endExpiredSession());
  }

  Future<void> _endExpiredSession() async {
    if (_session == null) return;

    try {
      await BackgroundGpsTracker.instance.stop();
    } catch (_) {}
    _session = null;
    // ignore: unawaited_futures
    AnalyticsService.instance.logout();
    // ignore: unawaited_futures
    AnalyticsService.instance.setDriverId(null);
    notifyListeners();
  }

  Future<void> login(LoginRequest request) async {
    final session = await _api.login(request);
    await _persist(session);
    _session = session;
    _setCrashlyticsIdentity(session);
    // ignore: unawaited_futures
    AnalyticsService.instance.setDriverId(session.driverId);
    // ignore: unawaited_futures
    AnalyticsService.instance.loginSuccess();
    notifyListeners();
    // Register FCM after auth so Authorization header is available.
    // ignore: unawaited_futures
    PushRegistrationService.instance.start(_dio);
    // ignore: unawaited_futures
    DeviceRegistrationService(_dio).registerCurrentDevice();
  }

  void _setCrashlyticsIdentity(DriverSession session) {
    try {
      // Do not attach phone / PII — driver id + tenant are enough for support.
      FirebaseCrashlytics.instance
        ..setUserIdentifier(session.driverId.toString())
        ..setCustomKey('driver_id', session.driverId)
        ..setCustomKey('tenant_id', session.tenantId);
    } catch (_) {
      // Firebase may not be initialized in dev — ignore silently
    }
  }

  Future<void> logout() async {
    final token = _session?.refreshToken ?? '';
    try {
      await BackgroundGpsTracker.instance.stop();
    } catch (_) {}
    try {
      await _api.logout(token);
    } catch (_) {}
    try {
      await _clear();
    } catch (_) {}
    _session = null;
    // ignore: unawaited_futures
    AnalyticsService.instance.logout();
    // ignore: unawaited_futures
    AnalyticsService.instance.setDriverId(null);
    notifyListeners();
  }

  Future<void> _persist(DriverSession session) async {
    await _storage.write(key: _sessionKey, value: jsonEncode(session.toJson()));
    await _storage.write(key: _accessTokenKey, value: session.accessToken);
    await _storage.write(key: _refreshTokenKey, value: session.refreshToken);
  }

  Future<void> _clear() async {
    await _storage.deleteAll();
  }

  @override
  void dispose() {
    _sessionInvalidation.removeListener(_onSessionInvalidated);
    super.dispose();
  }
}
