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
import '../../gps/services/gps_background_service.dart';
import '../../gps/services/gps_session_store.dart';
import '../../gps/services/signalr_service.dart';
import '../../../core/security/device_registration_service.dart';
import '../../../core/analytics/analytics_service.dart';

const _sessionKey = 'fleet_session';
const _legacySessionKey = 'driver_session';
const _accessTokenKey = 'fleet_access_token';
const _refreshTokenKey = 'fleet_refresh_token';
const _legacyAccessTokenKey = 'driver_access_token';
const _legacyRefreshTokenKey = 'driver_refresh_token';
const _migrationDoneKey = 'fleet_session_migrated';
const _rememberMeKey = 'fleet_remember_me';

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
  bool _endingSession = false;

  FleetSession? get session => _session;
  bool get isLoggedIn => _session != null;
  bool get isLoading => _loading;

  Future<void> _restoreSession() async {
    try {
      await _migrateLegacySessionIfNeeded();

      final remember = await _storage.read(key: _rememberMeKey);
      if (remember == 'false') {
        await _clearEphemeralAuth();
        return;
      }

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
    } catch (e, st) {
      // Keychain denial (macOS -128) or storage failures must not crash startup.
      debugPrint('[Auth] Session restore failed: $e\n$st');
      _session = null;
      try {
        await _storage.delete(key: _sessionKey);
      } catch (_) {}
    } finally {
      _loading = false;
      notifyListeners();
    }
    if (_session != null && _session!.companyContext == null) {
      // ignore: unawaited_futures
      _hydrateCompanyContext();
    }
  }

  Future<void> _migrateLegacySessionIfNeeded() async {
    try {
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
    } catch (e) {
      debugPrint('[Auth] Legacy session migration skipped: $e');
    }
  }

  void _onSessionInvalidated() {
    final reason = _sessionInvalidation.reason;
    if (reason == null) return;
    unawaited(_terminateSession(reason: reason, notifyApi: false));
  }

  Future<void> _terminateSession({
    required SessionTerminationReason reason,
    required bool notifyApi,
  }) async {
    if (_endingSession) return;
    _endingSession = true;
    final current = _session;
    final refreshToken = current?.refreshToken ?? '';
    try {
      await BackgroundGpsTracker.instance.stop();
    } catch (_) {}
    try {
      await SignalRService.instance.disconnect();
    } catch (_) {}
    try {
      await GpsSessionStore.clear();
    } catch (_) {}
    try {
      await GpsBackgroundService.cancelDrainTask();
    } catch (_) {}
    if (notifyApi && refreshToken.isNotEmpty) {
      try {
        await _api.logout(refreshToken);
      } catch (_) {}
    }
    try {
      await _clear();
    } catch (_) {}
    _session = null;
    _sessionExpired = reason != SessionTerminationReason.manual;
    // ignore: unawaited_futures
    AnalyticsService.instance.logout();
    // ignore: unawaited_futures
    AnalyticsService.instance.setDriverId(null);
    _sessionInvalidation.completeTermination();
    _endingSession = false;
    notifyListeners();
  }

  bool _sessionExpired = false;
  bool get sessionExpired => _sessionExpired;

  void clearSessionExpiredFlag() {
    if (!_sessionExpired) return;
    _sessionExpired = false;
    notifyListeners();
  }

  Future<void> login(LoginRequest request) async {
    final session = await _api.login(request);
    await _storage.write(
      key: _rememberMeKey,
      value: request.rememberMe ? 'true' : 'false',
    );
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
    _sessionInvalidation.requestTermination(SessionTerminationReason.manual);
    await _terminateSession(
      reason: SessionTerminationReason.manual,
      notifyApi: true,
    );
  }

  Future<void> _persist(FleetSession session) async {
    await _storage.write(key: _accessTokenKey, value: session.accessToken);
    await _storage.write(key: _refreshTokenKey, value: session.refreshToken);
    final remember = await _storage.read(key: _rememberMeKey);
    if (remember == 'true') {
      await _storage.write(
        key: _sessionKey,
        value: jsonEncode(session.toJson()),
      );
    } else {
      await _storage.delete(key: _sessionKey);
    }
  }

  Future<void> _clearEphemeralAuth() async {
    await _storage.delete(key: _sessionKey);
    await _storage.delete(key: _accessTokenKey);
    await _storage.delete(key: _refreshTokenKey);
    _session = null;
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
