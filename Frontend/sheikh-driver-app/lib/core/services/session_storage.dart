import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

const _tokenKey = 'driver_access_token';

final sessionStorageProvider = Provider((_) => const FlutterSecureStorage());

final sessionTokenProvider =
    StateNotifierProvider<SessionTokenNotifier, String?>((ref) {
  return SessionTokenNotifier(ref.read(sessionStorageProvider));
});

class SessionTokenNotifier extends StateNotifier<String?> {
  SessionTokenNotifier(this._storage) : super(null) {
    _load();
  }

  final FlutterSecureStorage _storage;

  Future<void> _load() async {
    state = await _storage.read(key: _tokenKey);
  }

  Future<void> setToken(String? token) async {
    state = token;
    if (token == null) {
      await _storage.delete(key: _tokenKey);
    } else {
      await _storage.write(key: _tokenKey, value: token);
    }
  }
}
