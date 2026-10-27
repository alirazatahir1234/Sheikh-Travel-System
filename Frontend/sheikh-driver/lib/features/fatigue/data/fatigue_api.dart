import 'package:dio/dio.dart';
import '../../../core/api/api_endpoints.dart';

class FatigueActivePrompt {
  FatigueActivePrompt({
    required this.id,
    required this.status,
    required this.ackDeadlineUtc,
    required this.secondsRemaining,
    required this.promptTitle,
    required this.promptBody,
  });

  final int id;
  final String status;
  final DateTime ackDeadlineUtc;
  final int secondsRemaining;
  final String promptTitle;
  final String promptBody;

  factory FatigueActivePrompt.fromJson(Map<String, dynamic> json) {
    final d = json['data'] as Map<String, dynamic>? ?? json;
    return FatigueActivePrompt(
      id: (d['id'] as num).toInt(),
      status: d['status'] as String? ?? 'AwaitingAck',
      ackDeadlineUtc: DateTime.parse(d['ackDeadlineUtc'] as String),
      secondsRemaining: (d['secondsRemaining'] as num?)?.toInt() ?? 0,
      promptTitle: d['promptTitle'] as String? ?? 'Fatigue check',
      promptBody: d['promptBody'] as String? ?? '',
    );
  }
}

class FatigueApi {
  FatigueApi(this._dio);

  final Dio _dio;

  Future<FatigueActivePrompt?> fetchActivePrompt() async {
    final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.fatigueActive);
    final body = res.data;
    if (body == null || body['data'] == null) return null;
    return FatigueActivePrompt.fromJson(body);
  }

  Future<void> acknowledge(int eventId) async {
    await _dio.post(ApiEndpoints.fatigueAck(eventId));
  }

  Future<void> submitCameraSignal({
    required String signalType,
    required double confidence,
    double? earScore,
    String? expressionHint,
    double? latitude,
    double? longitude,
    double? obdSpeedKmh,
  }) async {
    await _dio.post(ApiEndpoints.fatigueSignal, data: {
      'signalType': signalType,
      'source': 'mobile_camera_prototype',
      'confidence': confidence,
      if (earScore != null) 'earScore': earScore,
      if (expressionHint != null) 'expressionHint': expressionHint,
      if (latitude != null) 'latitude': latitude,
      if (longitude != null) 'longitude': longitude,
      if (obdSpeedKmh != null) 'obdSpeedKmh': obdSpeedKmh,
    });
  }
}
