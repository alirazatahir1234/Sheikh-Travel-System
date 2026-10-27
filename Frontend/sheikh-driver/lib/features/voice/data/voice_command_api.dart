import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/offline/offline_models.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../domain/voice_speech_locale.dart';

final voiceCommandApiProvider = Provider<VoiceCommandApi>((ref) {
  return VoiceCommandApi(ref.read(dioProvider), ref.read(offlineSyncProvider));
});

class VoiceCommandSubmitResult {
  VoiceCommandSubmitResult({
    required this.voiceCommandId,
    required this.resolvedIntent,
    this.sosAlertId,
    this.queuedOffline = false,
  });

  final int voiceCommandId;
  final String resolvedIntent;
  final int? sosAlertId;
  final bool queuedOffline;
}

class VoiceCommandApi {
  VoiceCommandApi(this._dio, this._offline);

  final Dio _dio;
  final OfflineSyncService _offline;

  Future<VoiceCommandSubmitResult> submit({
    required String transcript,
    required VoiceSpeechLocale language,
    String? intent,
    String? clientOpId,
    double? latitude,
    double? longitude,
  }) async {
    final payload = {
      'transcript': transcript,
      'languageCode': language.code,
      if (intent != null && intent.isNotEmpty) 'intent': intent,
      if (clientOpId != null) 'clientOpId': clientOpId,
      if (latitude != null) 'latitude': latitude,
      if (longitude != null) 'longitude': longitude,
    };

    try {
      return await _offline.runOrQueue(
        type: OfflineOpType.voiceCommand,
        payload: payload,
        online: () async {
          final response = await _dio.post<Map<String, dynamic>>(
            ApiEndpoints.voiceCommands,
            data: payload,
            options: Options(headers: {
              if (clientOpId != null) ...{
                'Idempotency-Key': clientOpId,
                'X-Client-Op-Id': clientOpId,
              },
            }),
          );
          ApiResponseParser.ensureSuccess(response.data);
          final d = ApiResponseParser.requireDataMap(response.data);
          return VoiceCommandSubmitResult(
            voiceCommandId: (d['voiceCommandId'] as num).toInt(),
            resolvedIntent: d['resolvedIntent']?.toString() ?? 'Message',
            sosAlertId: (d['sosAlertId'] as num?)?.toInt(),
          );
        },
        queuedValue: (_) => VoiceCommandSubmitResult(
          voiceCommandId: 0,
          resolvedIntent: intent ?? 'Message',
          queuedOffline: true,
        ),
      );
    } on OfflineQueuedException {
      rethrow;
    }
  }
}
