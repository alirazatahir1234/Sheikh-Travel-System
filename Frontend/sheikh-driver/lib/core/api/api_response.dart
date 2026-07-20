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
    final list = (body?['data'] as List?) ?? (body as List?) ?? [];
    return list
        .whereType<Map>()
        .map((e) => Map<String, dynamic>.from(e))
        .toList();
  }
}
