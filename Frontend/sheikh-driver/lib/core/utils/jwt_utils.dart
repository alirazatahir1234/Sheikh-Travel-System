import 'dart:convert';

/// Reads JWT payload claims without signature verification (client-side metadata only).
abstract final class JwtUtils {
  static Map<String, dynamic>? decodePayload(String token) {
    final parts = token.split('.');
    if (parts.length < 2) return null;
    try {
      final normalized = base64Url.normalize(parts[1]);
      final decoded = utf8.decode(base64Url.decode(normalized));
      final json = jsonDecode(decoded);
      return json is Map<String, dynamic> ? json : null;
    } catch (_) {
      return null;
    }
  }

  static int? claimInt(String token, String key) {
    final payload = decodePayload(token);
    if (payload == null) return null;
    final value = payload[key];
    if (value is int) return value;
    if (value is String) return int.tryParse(value);
    return null;
  }

  static String? claimString(String token, String key) {
    final payload = decodePayload(token);
    if (payload == null) return null;
    final value = payload[key];
    return value?.toString();
  }
}
