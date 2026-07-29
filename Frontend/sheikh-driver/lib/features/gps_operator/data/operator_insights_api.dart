import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';

final operatorInsightsApiProvider =
    Provider<OperatorInsightsApi>((ref) => OperatorInsightsApi(ref.read(dioProvider)));

class OperatorInsightResult {
  const OperatorInsightResult({
    required this.title,
    required this.summary,
    required this.bullets,
  });

  final String title;
  final String summary;
  final List<String> bullets;

  factory OperatorInsightResult.fromJson(Map<String, dynamic> json) {
    final bullets = json['bullets'] ?? json['Bullets'];
    return OperatorInsightResult(
      title: (json['title'] ?? json['Title'] ?? '').toString(),
      summary: (json['summary'] ?? json['Summary'] ?? '').toString(),
      bullets: bullets is List
          ? bullets.map((e) => e.toString()).toList()
          : const [],
    );
  }
}

class OperatorInsightsApi {
  OperatorInsightsApi(this._dio);
  final Dio _dio;

  Future<OperatorInsightResult> fetch(String query) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.gpsOperatorInsights,
      data: {'queryKey': query, 'query': query},
    );
    final map = ApiResponseParser.dataMap(res.data);
    return OperatorInsightResult.fromJson(map);
  }
}
