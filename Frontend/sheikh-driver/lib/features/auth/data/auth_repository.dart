import 'dart:convert';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../../../core/api/dio_client.dart';
import '../domain/auth_models.dart';
import 'auth_api.dart';

const _sessionKey = 'driver_session';
const _accessTokenKey = 'driver_access_token';
const _refreshTokenKey = 'driver_refresh_token';

final authRepositoryProvider = ChangeNotifierProvider<AuthRepository>(
  (ref) => AuthRepository(ref.read(secureStorageProvider), ref.read(authApiProvider)),
);

class AuthRepository extends ChangeNotifier {
  AuthRepository(this._storage, this._api) {
    _restoreSession();
  }

  final FlutterSecureStorage _storage;
  final AuthApi _api;

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

  Future<void> login(LoginRequest request) async {
    final session = await _api.login(request);
    await _persist(session);
    _session = session;
    _setCrashlyticsIdentity(session);
    notifyListeners();
  }

  void _setCrashlyticsIdentity(DriverSession session) {
    try {
      FirebaseCrashlytics.instance
        ..setUserIdentifier(session.driverId.toString())
        ..setCustomKey('driver_id', session.driverId)
        ..setCustomKey('tenant_id', session.tenantId)
        ..setCustomKey('phone', session.phone);
    } catch (_) {
      // Firebase may not be initialized in dev — ignore silently
    }
  }

  Future<void> logout() async {
    final token = _session?.refreshToken ?? '';
    await _api.logout(token);
    await _clear();
    _session = null;
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
}
