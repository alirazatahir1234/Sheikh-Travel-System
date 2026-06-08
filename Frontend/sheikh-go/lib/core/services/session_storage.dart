import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../models/auth_session.dart';

const _sessionKey = 'sheikh_go_session';
const _rememberedLoginKey = 'sheikh_go_remembered_login';

final secureStorageProvider = Provider((_) => const FlutterSecureStorage());

final sessionProvider =
    StateNotifierProvider<SessionNotifier, AsyncValue<AuthSession?>>((ref) {
  return SessionNotifier(ref.read(secureStorageProvider));
});

final rememberedLoginProvider = FutureProvider<String?>((ref) async {
  return ref.read(secureStorageProvider).read(key: _rememberedLoginKey);
});

class SessionNotifier extends StateNotifier<AsyncValue<AuthSession?>> {
  SessionNotifier(this._storage) : super(const AsyncValue.loading()) {
    _load();
  }

  final FlutterSecureStorage _storage;

  Future<void> _load() async {
    try {
      final raw = await _storage.read(key: _sessionKey);
      if (raw == null || raw.isEmpty) {
        state = const AsyncValue.data(null);
        return;
      }
      state = AsyncValue.data(
        AuthSession.fromJson(jsonDecode(raw) as Map<String, dynamic>),
      );
    } catch (e, st) {
      state = AsyncValue.error(e, st);
    }
  }

  Future<void> setSession(AuthSession? session) async {
    if (session == null) {
      await _storage.delete(key: _sessionKey);
      state = const AsyncValue.data(null);
      return;
    }
    await _storage.write(
      key: _sessionKey,
      value: jsonEncode(session.toJson()),
    );
    state = AsyncValue.data(session);
  }

  Future<void> setRememberedLogin(String? value) async {
    if (value == null || value.isEmpty) {
      await _storage.delete(key: _rememberedLoginKey);
      return;
    }
    await _storage.write(key: _rememberedLoginKey, value: value);
  }
}
