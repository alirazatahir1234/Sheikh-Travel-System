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

  static const defaultPageSize = 50;

  Future<MaintenanceDashboard> getDashboard() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.maintenanceDashboard);
    return MaintenanceDashboard.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<MaintenanceKpis> dashboardKpis() async {
    final dash = await getDashboard();
    return dash.kpis;
  }

  Future<MaintenanceRequestStats> requestStats() async {
    final res = await _dio
        .get<Map<String, dynamic>>(ApiEndpoints.maintenanceRequestStats);
    return MaintenanceRequestStats.fromJson(
      ApiResponseParser.dataMap(res.data),
    );
  }

  Future<WorkOrderStats> workOrderStats() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.workOrderStats);
    return WorkOrderStats.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<PagedList<MaintenanceRequestItem>> listRequests({
    String? status,
    String? search,
    int page = 1,
    int pageSize = defaultPageSize,
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
    final items = ApiResponseParser.pagedItems(res.data)
        .map(MaintenanceRequestItem.fromJson)
        .toList();
    return PagedList(
      items: items,
      totalCount: ApiResponseParser.pagedTotalCount(
        res.data,
        fallback: items.length,
      ),
      page: page,
      pageSize: pageSize,
    );
  }

  Future<MaintenanceRequestItem> getRequest(int id) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.maintenanceRequestById(id),
    );
    return MaintenanceRequestItem.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<int> createRequest({
    required int vehicleId,
    int? driverId,
    required String requestType,
    required String priority,
    required String issueCategory,
    required String description,
    String? breakdownLocation,
    String? driverRemarks,
  }) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.maintenanceRequests,
      data: {
        'vehicleId': vehicleId,
        if (driverId != null) 'driverId': driverId,
        'requestType': requestType,
        'priority': priority,
        'issueCategory': issueCategory,
        'description': description,
        if (breakdownLocation != null) 'breakdownLocation': breakdownLocation,
        if (driverRemarks != null) 'driverRemarks': driverRemarks,
      },
    );
    return ApiResponseParser.dataInt(res.data);
  }

  Future<void> approveRequest(int id) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.maintenanceRequestApprove(id),
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<void> rejectRequest(int id, {required String reason}) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.maintenanceRequestReject(id),
      data: {'reason': reason},
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<int> convertRequest(
    int id, {
    String? serviceTypeName,
    DateTime? startDate,
  }) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.maintenanceRequestConvert(id),
      data: {
        if (serviceTypeName != null && serviceTypeName.isNotEmpty)
          'serviceTypeName': serviceTypeName,
        if (startDate != null) 'startDate': startDate.toUtc().toIso8601String(),
      },
    );
    return ApiResponseParser.dataInt(res.data);
  }

  Future<PagedList<WorkOrderItem>> listWorkOrders({
    String? status,
    String? search,
    int page = 1,
    int pageSize = defaultPageSize,
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
    final items = ApiResponseParser.pagedItems(res.data)
        .map(WorkOrderItem.fromJson)
        .toList();
    return PagedList(
      items: items,
      totalCount: ApiResponseParser.pagedTotalCount(
        res.data,
        fallback: items.length,
      ),
      page: page,
      pageSize: pageSize,
    );
  }

  Future<WorkOrderItem> getWorkOrder(int id) async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.workOrderById(id));
    return WorkOrderItem.fromJson(ApiResponseParser.dataMap(res.data));
  }

  Future<int> createWorkOrder({
    required int vehicleId,
    int? requestId,
    int? scheduleId,
    String? serviceTypeName,
    String? maintenanceType,
    String? priority,
    String? notes,
    double laborCost = 0,
    double partsCost = 0,
  }) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.workOrders,
      data: {
        'vehicleId': vehicleId,
        if (requestId != null) 'requestId': requestId,
        if (scheduleId != null) 'scheduleId': scheduleId,
        if (serviceTypeName != null) 'serviceTypeName': serviceTypeName,
        if (maintenanceType != null) 'maintenanceType': maintenanceType,
        if (priority != null) 'priority': priority,
        if (notes != null) 'notes': notes,
        'laborCost': laborCost,
        'partsCost': partsCost,
      },
    );
    return ApiResponseParser.dataInt(res.data);
  }

  Future<void> updateWorkOrderStatus(
    int id, {
    required String status,
    String? technicianNotes,
  }) async {
    final res = await _dio.put<Map<String, dynamic>>(
      ApiEndpoints.workOrderStatus(id),
      data: {
        'status': status,
        if (technicianNotes != null) 'technicianNotes': technicianNotes,
      },
    );
    ApiResponseParser.ensureSuccess(res.data);
  }

  Future<List<MaintenanceScheduleItem>> listSchedules({
    String? status,
    String? search,
    int? vehicleId,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.maintenanceSchedules,
      queryParameters: {
        if (status != null && status.isNotEmpty) 'status': status,
        if (search != null && search.isNotEmpty) 'search': search,
        if (vehicleId != null) 'vehicleId': vehicleId,
      },
    );
    return ApiResponseParser.dataList(res.data)
        .map(MaintenanceScheduleItem.fromJson)
        .toList();
  }

  Future<int> createWorkOrderFromSchedule(int scheduleId) async {
    final res = await _dio.post<Map<String, dynamic>>(
      ApiEndpoints.maintenanceScheduleWorkOrder(scheduleId),
    );
    return ApiResponseParser.dataInt(res.data);
  }
}
