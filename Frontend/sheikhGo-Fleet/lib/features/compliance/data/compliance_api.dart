import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/compliance_models.dart';

final complianceApiProvider =
    Provider<ComplianceApi>((ref) => ComplianceApi(ref.read(dioProvider)));

class ComplianceApi {
  ComplianceApi(this._dio);
  final Dio _dio;

  Future<List<ComplianceDocument>> list() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.fleetCompliance);
    return ApiResponseParser.dataList(res.data)
        .map(ComplianceDocument.fromJson)
        .toList();
  }

  Future<ComplianceSummary> summary() async {
    try {
      final res = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.maintenanceComplianceSummary,
      );
      return ComplianceSummary.fromJson(ApiResponseParser.dataMap(res.data));
    } catch (_) {
      return ComplianceSummary.empty;
    }
  }
}
