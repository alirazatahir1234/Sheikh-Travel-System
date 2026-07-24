import 'dart:async';
import 'dart:convert';
import 'package:dio/dio.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/config/app_config.dart';
import '../domain/auth_models.dart';
import 'auth_api.dart';
import '../../notifications/services/push_registration_service.dart';
import '../../gps/services/background_gps_tracker.dart';
import '../../../core/security/device_registration_service.dart';
import '../../../core/analytics/analytics_service.dart';

const _sessionKey = 'fleet_session';
const _legacySessionKey = 'driver_session';
const _accessTokenKey = 'fleet_access_token';
const _refreshTokenKey = 'fleet_refresh_token';
const _legacyAccessTokenKey = 'driver_access_token';
const _legacyRefreshTokenKey = 'driver_refresh_token';
const _migrationDoneKey = 'fleet_session_migrated';

final authRepositoryProvider = ChangeNotifierProvider<AuthRepository>(
  (ref) => AuthRepository(
    ref.read(secureStorageProvider),
    ref.read(authApiProvider),
    ref.read(dioProvider),
    ref.read(sessionInvalidationProvider),
  ),
);

final fleetSessionProvider = Provider<FleetSession?>((ref) {
  return ref.watch(authRepositoryProvider).session;
});

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

  FleetSession? _session;
  bool _loading = true;

  FleetSession? get session => _session;
  bool get isLoggedIn => _session != null;
  bool get isLoading => _loading;

  Future<void> _restoreSession() async {
    await _migrateLegacySessionIfNeeded();

    final raw = await _storage.read(key: _sessionKey);
    if (raw != null) {
      try {
        _session = FleetSession.fromJson(
          jsonDecode(raw) as Map<String, dynamic>,
        );
      } catch (_) {
        await _storage.delete(key: _sessionKey);
      }
    }
    _loading = false;
    notifyListeners();
    if (_session != null && _session!.companyContext == null) {
      // ignore: unawaited_futures
      _hydrateCompanyContext();
    }
  }

  Future<void> _migrateLegacySessionIfNeeded() async {
    final migrated = await _storage.read(key: _migrationDoneKey);
    if (migrated == 'true') return;

    final legacyRaw = await _storage.read(key: _legacySessionKey);
    if (legacyRaw != null) {
      try {
        final legacySession = FleetSession.fromLegacyDriverJson(
          jsonDecode(legacyRaw) as Map<String, dynamic>,
        );
        await _persist(legacySession);
        _session = legacySession;
      } catch (_) {
        await _storage.delete(key: _legacySessionKey);
      }
    }

    final legacyAccess = await _storage.read(key: _legacyAccessTokenKey);
    final legacyRefresh = await _storage.read(key: _legacyRefreshTokenKey);
    if (legacyAccess != null) {
      await _storage.write(key: _accessTokenKey, value: legacyAccess);
      await _storage.delete(key: _legacyAccessTokenKey);
    }
    if (legacyRefresh != null) {
      await _storage.write(key: _refreshTokenKey, value: legacyRefresh);
      await _storage.delete(key: _legacyRefreshTokenKey);
    }

    await _storage.delete(key: _legacySessionKey);
    await _storage.write(key: _migrationDoneKey, value: 'true');
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
    final appName =
        session.authMode == AuthMode.driver || session.isDriverOnly
            ? 'driver'
            : 'fleet';
    // ignore: unawaited_futures
    PushRegistrationService.instance.start(_dio, appName: appName);
    // ignore: unawaited_futures
    DeviceRegistrationService(_dio).registerCurrentDevice();
    // ignore: unawaited_futures
    _hydrateCompanyContext();
  }

  Future<void> _hydrateCompanyContext() async {
    final current = _session;
    if (current == null) return;
    final context = await _api.fetchCompanyContext();
    if (context == null || _session?.userId != current.userId) return;
    final enriched = current.copyWith(companyContext: context);
    await _persist(enriched);
    _session = enriched;
    notifyListeners();
  }

  void _setCrashlyticsIdentity(FleetSession session) {
    try {
      final identifier = session.driverId?.toString() ?? session.userId.toString();
      FirebaseCrashlytics.instance
        ..setUserIdentifier(identifier)
        ..setCustomKey('user_id', session.userId)
        ..setCustomKey('tenant_id', session.tenantId)
        ..setCustomKey('env', AppConfig.environmentLabel);
      if (session.driverId != null) {
        FirebaseCrashlytics.instance.setCustomKey('driver_id', session.driverId!);
      }
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

  Future<void> _persist(FleetSession session) async {
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
