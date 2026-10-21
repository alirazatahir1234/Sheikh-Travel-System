/// Parses SheikhTravel API envelope `{ success, message, data }`.
class ApiResponseParser {
  ApiResponseParser._();

  static void ensureSuccess(Map<String, dynamic>? body) {
    if (body == null) return;
    if (body['success'] == false) {
      throw Exception(
        body['message']?.toString() ?? 'Request failed. Please try again.',
      );
    }
  }

  static List<Map<String, dynamic>> dataList(Map<String, dynamic>? body) {
    ensureSuccess(body);
    final data = body?['data'];
    final list = data is List ? data : const [];
    return list
        .whereType<Map>()
        .map((e) => Map<String, dynamic>.from(e))
        .toList();
  }

  static Map<String, dynamic> dataMap(Map<String, dynamic>? body) {
    ensureSuccess(body);
    final data = body?['data'];
    if (data is Map<String, dynamic>) return data;
    if (data is Map) return Map<String, dynamic>.from(data);
    throw Exception(body?['message']?.toString() ?? 'Empty response');
  }

  /// Parses `{ data: { items: [...], totalCount, page, pageSize } }`.
  static List<Map<String, dynamic>> pagedItems(Map<String, dynamic>? body) {
    ensureSuccess(body);
    final data = body?['data'];
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
    final data = body?['data'];
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
    final data = body?['data'];
    if (data is int) return data;
    if (data is num) return data.round();
    final parsed = int.tryParse('$data');
    if (parsed != null) return parsed;
    throw Exception(body?['message']?.toString() ?? 'Empty response');
  }
}
