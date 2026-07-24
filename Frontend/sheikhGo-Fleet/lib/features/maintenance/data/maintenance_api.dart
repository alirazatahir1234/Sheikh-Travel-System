import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/maintenance_models.dart';

final maintenanceApiProvider =
    Provider<MaintenanceApi>((ref) => MaintenanceApi(ref.read(dioProvider)));

class MaintenanceApi {
  MaintenanceApi(this._dio);
  final Dio _dio;

  Future<MaintenanceKpis> dashboardKpis() async {
    final res = await _dio
        .get<Map<String, dynamic>>(ApiEndpoints.maintenanceDashboard);
    final data = ApiResponseParser.dataMap(res.data);
    final kpis = data['kpis'] ?? data['Kpis'];
    if (kpis is Map) {
      return MaintenanceKpis.fromJson(Map<String, dynamic>.from(kpis));
    }
    return MaintenanceKpis.empty;
  }

  Future<List<MaintenanceRequestItem>> listRequests({
    String? status,
    String? search,
    int page = 1,
    int pageSize = 100,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.maintenanceRequests,
      queryParameters: {
        'page': page,
        'pageSize': pageSize,
        if (status != null && status.isNotEmpty) 'status': status,
        if (search != null && search.isNotEmpty) 'search': search,
      },
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(MaintenanceRequestItem.fromJson)
        .toList();
  }

  Future<MaintenanceRequestItem> getRequest(int id) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.maintenanceRequestById(id),
    );
    return MaintenanceRequestItem.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<List<WorkOrderItem>> listWorkOrders({
    String? status,
    String? search,
    int page = 1,
    int pageSize = 100,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.workOrders,
      queryParameters: {
        'page': page,
        'pageSize': pageSize,
        if (status != null && status.isNotEmpty) 'status': status,
        if (search != null && search.isNotEmpty) 'search': search,
      },
    );
    return ApiResponseParser.pagedItems(res.data)
        .map(WorkOrderItem.fromJson)
        .toList();
  }

  Future<WorkOrderItem> getWorkOrder(int id) async {
    final res = await _dio
        .get<Map<String, dynamic>>(ApiEndpoints.workOrderById(id));
    return WorkOrderItem.fromJson(ApiResponseParser.dataMap(res.data));
  }
}
