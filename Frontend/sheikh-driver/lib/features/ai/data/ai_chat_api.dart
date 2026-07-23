import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../domain/ai_chat_models.dart';

final aiChatApiProvider =
    Provider<AiChatApi>((ref) => AiChatApi(ref.read(dioProvider)));

/// AiController returns DTOs via `Ok(...)` (not the `{ success, data }` envelope).
class AiChatApi {
  AiChatApi(this._dio);
  final Dio _dio;

  Future<AiChatTurnResponse> chat({
    required String message,
    String? sessionId,
    String? title,
    bool confirmWrite = false,
  }) async {
    final res = await _dio.post<dynamic>(
      ApiEndpoints.aiChat,
      data: {
        'message': message,
        'sessionId': sessionId,
        'title': title,
        'confirmWrite': confirmWrite,
      },
      options: Options(
        receiveTimeout: const Duration(seconds: 90),
        sendTimeout: const Duration(seconds: 30),
      ),
    );
    return AiChatTurnResponse.fromJson(_asMap(res.data));
  }

  Future<List<AiChatSession>> listSessions() async {
    final res = await _dio.get<dynamic>(ApiEndpoints.aiChatSessions);
    return _asList(res.data).map(AiChatSession.fromJson).toList();
  }

  Future<List<AiChatMessage>> getMessages(String sessionId) async {
    final res =
        await _dio.get<dynamic>(ApiEndpoints.aiChatMessages(sessionId));
    return _asList(res.data).map(AiChatMessage.fromJson).toList();
  }

  Future<AiProviderHealth> getProviderHealth() async {
    final res = await _dio.get<dynamic>(ApiEndpoints.aiChatProviderHealth);
    return AiProviderHealth.fromJson(_asMap(res.data));
  }

  Future<List<AiToolInfo>> listTools() async {
    final res = await _dio.get<dynamic>(ApiEndpoints.aiChatTools);
    return _asList(res.data).map(AiToolInfo.fromJson).toList();
  }

  static Map<String, dynamic> _asMap(dynamic data) {
    if (data is Map) {
      final m = Map<String, dynamic>.from(data);
      final nested = m['data'];
      if (nested is Map &&
          (m.containsKey('success') || m.containsKey('message'))) {
        return Map<String, dynamic>.from(nested);
      }
      return m;
    }
    throw Exception('Unexpected AI response');
  }

  static List<Map<String, dynamic>> _asList(dynamic data) {
    if (data is List) {
      return data
          .whereType<Map>()
          .map((e) => Map<String, dynamic>.from(e))
          .toList();
    }
    if (data is Map) {
      final m = Map<String, dynamic>.from(data);
      final nested = m['data'];
      if (nested is List) {
        return nested
            .whereType<Map>()
            .map((e) => Map<String, dynamic>.from(e))
            .toList();
      }
    }
    return const [];
  }
}
