import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';

final sosApiProvider = Provider<SosApi>((ref) {
  return SosApi(ref.read(dioProvider));
});

class SosSubmitResult {
  SosSubmitResult({
    required this.id,
    required this.createdAt,
    this.confirmationMessage,
    this.dispatchMode,
    this.assignedTeamName,
    this.etaMinutes,
  });

  final int id;
  final DateTime createdAt;
  final String? confirmationMessage;
  final String? dispatchMode;
  final String? assignedTeamName;
  final int? etaMinutes;
}

class SosStatusResult {
  SosStatusResult({
    required this.id,
    required this.status,
    this.confirmationMessage,
    this.assignedTeamName,
    this.etaMinutes,
  });

  final int id;
  final String status;
  final String? confirmationMessage;
  final String? assignedTeamName;
  final int? etaMinutes;
}

class SosApi {
  SosApi(this._dio);

  final Dio _dio;
  Future<SosSubmitResult> sendSos({
    double? latitude,
    double? longitude,
    String? message,
  }) async {
    final clientOpId = 'sos-${DateTime.now().toUtc().millisecondsSinceEpoch}';
    final response = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.sos,
      data: {
        if (latitude != null) 'latitude': latitude,
        if (longitude != null) 'longitude': longitude,
        if (message != null && message.isNotEmpty) 'message': message,
      },
      options: Options(headers: {
        'Idempotency-Key': clientOpId,
        'X-Client-Op-Id': clientOpId,
      }),
    );
    ApiResponseParser.ensureSuccess(response.data);
    final d = ApiResponseParser.requireDataMap(response.data);
    return SosSubmitResult(
      id: (d['id'] as num).toInt(),
      createdAt: DateTime.parse(d['createdAt'].toString()),
      confirmationMessage: d['confirmationMessage']?.toString(),
      dispatchMode: d['dispatchMode']?.toString(),
      assignedTeamName: d['assignedTeamName']?.toString(),
      etaMinutes: (d['etaMinutes'] as num?)?.toInt(),
    );
  }

  Future<SosStatusResult> fetchStatus(int sosId) async {
    final response = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.sosStatus(sosId),
    );
    ApiResponseParser.ensureSuccess(response.data);
    final d = ApiResponseParser.requireDataMap(response.data);
    return SosStatusResult(
      id: (d['id'] as num).toInt(),
      status: d['status']?.toString() ?? 'Open',
      confirmationMessage: d['confirmationMessage']?.toString(),
      assignedTeamName: d['assignedTeamName']?.toString(),
      etaMinutes: (d['etaMinutes'] as num?)?.toInt(),
    );
  }
}
