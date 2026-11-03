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

  /// Server caps a chat turn at 75s; allow a little slack for network.
  static const chatReceiveTimeout = Duration(seconds: 85);
  static const _metaTimeout = Duration(seconds: 12);

  /// Chat is non-idempotent and slow — never auto-retry (duplicates turns
  /// and multiplies the wait). Metadata calls fail fast so the screen never
  /// sits behind a loader.
  static Options _opts(Duration receive) => Options(
        receiveTimeout: receive,
        sendTimeout: const Duration(seconds: 30),
        extra: const {'noRetry': true},
      );

  Future<AiChatTurnResponse> chat({
    required String message,
    String? sessionId,
    String? title,
    bool confirmWrite = false,
    CancelToken? cancelToken,
  }) async {
    final res = await _dio.post<dynamic>(
      ApiEndpoints.aiChat,
      data: {
        'message': message,
        'sessionId': sessionId,
        'title': title,
        'confirmWrite': confirmWrite,
      },
      options: _opts(chatReceiveTimeout),
      cancelToken: cancelToken,
    );
    return AiChatTurnResponse.fromJson(_asMap(res.data));
  }

  Future<List<AiChatSession>> listSessions({CancelToken? cancelToken}) async {
    final res = await _dio.get<dynamic>(
      ApiEndpoints.aiChatSessions,
      options: _opts(_metaTimeout),
      cancelToken: cancelToken,
    );
    return _asList(res.data).map(AiChatSession.fromJson).toList();
  }

  Future<List<AiChatMessage>> getMessages(
    String sessionId, {
    CancelToken? cancelToken,
  }) async {
    final res = await _dio.get<dynamic>(
      ApiEndpoints.aiChatMessages(sessionId),
      options: _opts(const Duration(seconds: 20)),
      cancelToken: cancelToken,
    );
    return _asList(res.data).map(AiChatMessage.fromJson).toList();
  }

  Future<AiProviderHealth> getProviderHealth({CancelToken? cancelToken}) async {
    final res = await _dio.get<dynamic>(
      ApiEndpoints.aiChatProviderHealth,
      options: _opts(_metaTimeout),
      cancelToken: cancelToken,
    );
    return AiProviderHealth.fromJson(_asMap(res.data));
  }

  Future<List<AiToolInfo>> listTools() async {
    final res = await _dio.get<dynamic>(ApiEndpoints.aiChatTools);
    return _asList(res.data).map(AiToolInfo.fromJson).toList();
  }

  /// Human-readable error for the chat banner (never raw DioException dumps).
  static String describeError(Object e) {
    if (e is DioException) {
      final data = e.response?.data;
      if (data is Map && data['message'] is String) {
        return data['message'] as String;
      }
      switch (e.type) {
        case DioExceptionType.connectionTimeout:
        case DioExceptionType.sendTimeout:
        case DioExceptionType.receiveTimeout:
          return 'The assistant took too long to respond. Please try again.';
        case DioExceptionType.connectionError:
          return 'No connection to SheikhGo. Check your internet and retry.';
        case DioExceptionType.cancel:
          return 'Request cancelled.';
        default:
          final code = e.response?.statusCode;
          return code != null
              ? 'Assistant request failed ($code). Please try again.'
              : 'Assistant request failed. Please try again.';
      }
    }
    return 'Something went wrong. Please try again.';
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
