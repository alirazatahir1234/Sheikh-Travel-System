import '../errors/app_exception.dart';

/// Parses SheikhTravel API envelope `{ success, message, data }`.
class ApiResponseParser {
  ApiResponseParser._();

  /// True when the envelope explicitly reports failure.
  ///
  /// Handles camelCase / PascalCase and string/int bools. Missing `success`
  /// is **not** treated as failure here (legacy payloads); use
  /// [ensureSuccess] / [requireDataMap] for stricter checks.
  static bool isExplicitFailure(Map<String, dynamic>? body) {
    if (body == null) return false;
    final raw = body['success'] ?? body['Success'];
    if (raw == false || raw == 0) return true;
    if (raw is String) {
      final v = raw.trim().toLowerCase();
      return v == 'false' || v == '0';
    }
    return false;
  }

  static String? messageOf(Map<String, dynamic>? body) {
    if (body == null) return null;
    final msg = body['message'] ?? body['Message'] ?? body['title'];
    if (msg == null) return null;
    final text = msg.toString().trim();
    return text.isEmpty ? null : text;
  }

  /// Throws [ValidationException] when `success == false` (HTTP may still be 200).
  static void ensureSuccess(Map<String, dynamic>? body) {
    if (body == null) return;
    if (isExplicitFailure(body)) {
      throw ValidationException(
        messageOf(body) ?? 'Request failed. Please try again.',
      );
    }
  }

  /// Unwraps `data` as a map; fails closed on `success:false` or missing data
  /// when the envelope already reported failure.
  static Map<String, dynamic> requireDataMap(
    Map<String, dynamic>? body, {
    String fallbackMessage = 'Empty response',
  }) {
    if (body == null) {
      throw UnknownException(fallbackMessage);
    }
    ensureSuccess(body);
    final data = body['data'] ?? body['Data'];
    if (data is Map<String, dynamic>) return data;
    if (data is Map) return Map<String, dynamic>.from(data);
    // Do not fall back to the envelope itself — that made failed logins look
    // like success (empty tokens) when callers treated HTTP 200 as OK.
    throw ValidationException(messageOf(body) ?? fallbackMessage);
  }

  static List<Map<String, dynamic>> dataList(Map<String, dynamic>? body) {
    ensureSuccess(body);
    final data = body?['data'] ?? body?['Data'];
    final list = data is List ? data : const [];
    return list
        .whereType<Map>()
        .map((e) => Map<String, dynamic>.from(e))
        .toList();
  }

  static Map<String, dynamic> dataMap(Map<String, dynamic>? body) {
    return requireDataMap(body);
  }

  /// Parses `{ data: { items: [...], totalCount, page, pageSize } }`.
  static List<Map<String, dynamic>> pagedItems(Map<String, dynamic>? body) {
    ensureSuccess(body);
    final data = body?['data'] ?? body?['Data'];
    if (data is Map) {
      final items = data['items'] ?? data['Items'];
      if (items is List) {
        return items
            .whereType<Map>()
            .map((e) => Map<String, dynamic>.from(e))
            .toList();
      }
    }
    if (data is List) {
      return data
          .whereType<Map>()
          .map((e) => Map<String, dynamic>.from(e))
          .toList();
    }
    return const [];
  }

  /// Total count from a paged envelope, or `items.length` when unknown.
  static int pagedTotalCount(Map<String, dynamic>? body, {int fallback = 0}) {
    ensureSuccess(body);
    final data = body?['data'] ?? body?['Data'];
    if (data is Map) {
      final raw = data['totalCount'] ?? data['TotalCount'];
      if (raw is int) return raw;
      if (raw is num) return raw.round();
      final parsed = int.tryParse('$raw');
      if (parsed != null) return parsed;
    }
    return fallback;
  }

  /// Parses `{ data: <int> }` (create endpoints that return a new id).
  static int dataInt(Map<String, dynamic>? body) {
    ensureSuccess(body);
    final data = body?['data'] ?? body?['Data'];
    if (data is int) return data;
    if (data is num) return data.round();
    final parsed = int.tryParse('$data');
    if (parsed != null) return parsed;
    throw ValidationException(messageOf(body) ?? 'Empty response');
  }
}
