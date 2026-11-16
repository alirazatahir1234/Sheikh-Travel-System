import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../domain/fleet_models.dart';

final adminCrossTenantApiProvider = Provider<AdminCrossTenantApi>(
  (ref) => AdminCrossTenantApi(ref.read(dioProvider)),
);

class AdminCompanyOption {
  const AdminCompanyOption({
    required this.id,
    required this.name,
    this.slug,
    this.activeVehicleCount = 0,
    this.isActive = true,
  });

  final int id;
  final String name;
  final String? slug;
  final int activeVehicleCount;
  final bool isActive;

  factory AdminCompanyOption.fromJson(Map<String, dynamic> json) {
    return AdminCompanyOption(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      name: json['name'] as String? ?? json['Name'] as String? ?? 'Company',
      slug: json['slug'] as String? ?? json['Slug'] as String?,
      activeVehicleCount: json['activeVehicleCount'] as int? ??
          json['ActiveVehicleCount'] as int? ??
          0,
      isActive: json['isActive'] as bool? ?? json['IsActive'] as bool? ?? true,
    );
  }
}

class AdminCrossTenantApi {
  AdminCrossTenantApi(this._dio);
  final Dio _dio;

  Future<List<AdminCompanyOption>> getCompanies() async {
    final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.adminCompanies);
    return ApiResponseParser.dataList(res.data)
        .map(AdminCompanyOption.fromJson)
        .toList();
  }

  Future<List<FleetVehicleLocation>> getVehicles({
    int? companyId,
    int page = 1,
    int pageSize = 200,
  }) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.adminVehicles,
      queryParameters: {
        if (companyId != null) 'companyId': companyId,
        'page': page,
        'pageSize': pageSize,
      },
    );
    final data = ApiResponseParser.dataMap(res.data);
    final items = data['items'] ?? data['Items'];
    final list = items is List ? items : const [];
    return list
        .whereType<Map>()
        .map((e) => _mapRow(Map<String, dynamic>.from(e)))
        .toList();
  }

  FleetVehicleLocation _mapRow(Map<String, dynamic> json) {
    final companyName =
        json['companyName'] as String? ?? json['CompanyName'] as String?;
    final name = json['name'] as String? ?? json['Name'] as String? ?? 'Vehicle';
    final plate = json['registrationNumber'] as String? ??
        json['RegistrationNumber'] as String? ??
        '';
    final online =
        json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false;
    final hasGps =
        json['hasGpsDevice'] as bool? ?? json['HasGpsDevice'] as bool? ?? false;
    final lat = (json['locationLatitude'] ?? json['LocationLatitude']) as num?;
    final lng = (json['locationLongitude'] ?? json['LocationLongitude']) as num?;
    final lastSeen = json['gpsLastSeenAt'] ?? json['GpsLastSeenAt'];
    DateTime? lastUpdated;
    if (lastSeen is String) {
      lastUpdated = DateTime.tryParse(lastSeen);
    }

    final displayName = companyName == null || companyName.isEmpty
        ? name
        : '$name · $companyName';

    return FleetVehicleLocation(
      vehicleId: json['id'] as int? ?? json['Id'] as int? ?? 0,
      vehicleName: displayName,
      registrationNumber: plate,
      status: online
          ? FleetTrackStatus.idle
          : (hasGps ? FleetTrackStatus.offline : FleetTrackStatus.neverSeen),
      latitude: lat?.toDouble(),
      longitude: lng?.toDouble(),
      lastUpdated: lastUpdated,
      driverName:
          json['driverName'] as String? ?? json['DriverName'] as String?,
      hasGps: hasGps,
      vehicleType:
          json['vehicleType'] as String? ?? json['VehicleType'] as String?,
      isGpsValid: lat != null && lng != null,
    );
  }
}
