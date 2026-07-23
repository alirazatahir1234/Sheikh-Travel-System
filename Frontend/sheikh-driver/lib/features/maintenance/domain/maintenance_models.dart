class MaintenanceKpis {
  const MaintenanceKpis({
    required this.totalVehicles,
    required this.dueForService,
    required this.underMaintenance,
    required this.overdueServices,
    required this.monthlyMaintenanceCost,
    required this.activeWorkOrders,
    required this.pendingRequests,
  });

  final int totalVehicles;
  final int dueForService;
  final int underMaintenance;
  final int overdueServices;
  final double monthlyMaintenanceCost;
  final int activeWorkOrders;
  final int pendingRequests;

  factory MaintenanceKpis.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) =>
        json[a] as int? ?? (b != null ? json[b] as int? : null) ?? 0;
    final cost = json['monthlyMaintenanceCost'] as num? ??
        json['MonthlyMaintenanceCost'] as num? ??
        0;
    return MaintenanceKpis(
      totalVehicles: n('totalVehicles', 'TotalVehicles'),
      dueForService: n('dueForService', 'DueForService'),
      underMaintenance: n('underMaintenance', 'UnderMaintenance'),
      overdueServices: n('overdueServices', 'OverdueServices'),
      monthlyMaintenanceCost: cost.toDouble(),
      activeWorkOrders: n('activeWorkOrders', 'ActiveWorkOrders'),
      pendingRequests: n('pendingRequests', 'PendingRequests'),
    );
  }

  static const empty = MaintenanceKpis(
    totalVehicles: 0,
    dueForService: 0,
    underMaintenance: 0,
    overdueServices: 0,
    monthlyMaintenanceCost: 0,
    activeWorkOrders: 0,
    pendingRequests: 0,
  );
}

class MaintenanceRequestItem {
  const MaintenanceRequestItem({
    required this.id,
    required this.requestNumber,
    required this.vehicleId,
    required this.requestDate,
    required this.requestType,
    required this.priority,
    required this.issueCategory,
    required this.description,
    required this.status,
    required this.createdAt,
    this.vehicleName,
    this.vehicleRegistration,
    this.driverId,
    this.driverName,
    this.workOrderId,
    this.rejectionReason,
  });

  final int id;
  final String requestNumber;
  final int vehicleId;
  final String? vehicleName;
  final String? vehicleRegistration;
  final int? driverId;
  final String? driverName;
  final DateTime requestDate;
  final String requestType;
  final String priority;
  final String issueCategory;
  final String description;
  final String status;
  final int? workOrderId;
  final DateTime createdAt;
  final String? rejectionReason;

  factory MaintenanceRequestItem.fromJson(Map<String, dynamic> json) {
    return MaintenanceRequestItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      requestNumber: json['requestNumber'] as String? ??
          json['RequestNumber'] as String? ??
          '',
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      vehicleRegistration: json['vehicleRegistration'] as String? ??
          json['VehicleRegistration'] as String?,
      driverId: json['driverId'] as int? ?? json['DriverId'] as int?,
      driverName: json['driverName'] as String? ?? json['DriverName'] as String?,
      requestDate: _date(json['requestDate'] ?? json['RequestDate']) ??
          DateTime.now(),
      requestType:
          json['requestType'] as String? ?? json['RequestType'] as String? ?? '',
      priority: json['priority'] as String? ?? json['Priority'] as String? ?? '',
      issueCategory: json['issueCategory'] as String? ??
          json['IssueCategory'] as String? ??
          '',
      description:
          json['description'] as String? ?? json['Description'] as String? ?? '',
      status: json['status'] as String? ?? json['Status'] as String? ?? '',
      workOrderId: json['workOrderId'] as int? ?? json['WorkOrderId'] as int?,
      createdAt:
          _date(json['createdAt'] ?? json['CreatedAt']) ?? DateTime.now(),
      rejectionReason: json['rejectionReason'] as String? ??
          json['RejectionReason'] as String?,
    );
  }
}

class WorkOrderItem {
  const WorkOrderItem({
    required this.id,
    required this.workOrderNumber,
    required this.vehicleId,
    required this.status,
    required this.totalCost,
    required this.createdAt,
    this.vehicleName,
    this.vehicleRegistration,
    this.serviceTypeName,
    this.priority,
    this.maintenanceType,
    this.workshopName,
    this.technicianName,
    this.startDate,
    this.estimatedCompletionDate,
    this.completedAt,
  });

  final int id;
  final String workOrderNumber;
  final int vehicleId;
  final String? vehicleName;
  final String? vehicleRegistration;
  final String? serviceTypeName;
  final String status;
  final String? priority;
  final String? maintenanceType;
  final double totalCost;
  final String? workshopName;
  final String? technicianName;
  final DateTime? startDate;
  final DateTime? estimatedCompletionDate;
  final DateTime? completedAt;
  final DateTime createdAt;

  factory WorkOrderItem.fromJson(Map<String, dynamic> json) {
    return WorkOrderItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      workOrderNumber: json['workOrderNumber'] as String? ??
          json['WorkOrderNumber'] as String? ??
          '',
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      vehicleRegistration: json['vehicleRegistration'] as String? ??
          json['VehicleRegistration'] as String?,
      serviceTypeName: json['serviceTypeName'] as String? ??
          json['ServiceTypeName'] as String?,
      status: json['status'] as String? ?? json['Status'] as String? ?? '',
      priority: json['priority'] as String? ?? json['Priority'] as String?,
      maintenanceType: json['maintenanceType'] as String? ??
          json['MaintenanceType'] as String?,
      totalCost: (json['totalCost'] as num? ?? json['TotalCost'] as num? ?? 0)
          .toDouble(),
      workshopName:
          json['workshopName'] as String? ?? json['WorkshopName'] as String?,
      technicianName:
          json['technicianName'] as String? ?? json['TechnicianName'] as String?,
      startDate: _date(json['startDate'] ?? json['StartDate']),
      estimatedCompletionDate: _date(
          json['estimatedCompletionDate'] ?? json['EstimatedCompletionDate']),
      completedAt: _date(json['completedAt'] ?? json['CompletedAt']),
      createdAt:
          _date(json['createdAt'] ?? json['CreatedAt']) ?? DateTime.now(),
    );
  }
}

DateTime? _date(Object? raw) {
  if (raw == null) return null;
  if (raw is DateTime) return raw;
  return DateTime.tryParse(raw.toString());
}
