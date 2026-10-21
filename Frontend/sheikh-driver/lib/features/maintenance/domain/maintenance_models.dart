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

class MaintenanceAlertItem {
  const MaintenanceAlertItem({
    required this.id,
    required this.alertType,
    required this.severity,
    required this.title,
    required this.message,
    required this.createdAt,
    this.vehicleId,
    this.vehicleName,
    this.vehicleRegistration,
  });

  final int id;
  final int? vehicleId;
  final String? vehicleName;
  final String? vehicleRegistration;
  final String alertType;
  final String severity;
  final String title;
  final String message;
  final DateTime createdAt;

  factory MaintenanceAlertItem.fromJson(Map<String, dynamic> json) {
    return MaintenanceAlertItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int?,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      vehicleRegistration: json['vehicleRegistration'] as String? ??
          json['VehicleRegistration'] as String?,
      alertType:
          json['alertType'] as String? ?? json['AlertType'] as String? ?? '',
      severity: json['severity'] as String? ?? json['Severity'] as String? ?? '',
      title: json['title'] as String? ?? json['Title'] as String? ?? '',
      message: json['message'] as String? ?? json['Message'] as String? ?? '',
      createdAt:
          _date(json['createdAt'] ?? json['CreatedAt']) ?? DateTime.now(),
    );
  }
}

class UpcomingServiceItem {
  const UpcomingServiceItem({
    required this.vehicleId,
    required this.vehicleName,
    required this.serviceType,
    required this.priority,
    this.scheduleId,
    this.vehicleRegistration,
    this.dueDate,
    this.dueMileage,
  });

  final int? scheduleId;
  final int vehicleId;
  final String vehicleName;
  final String? vehicleRegistration;
  final String serviceType;
  final DateTime? dueDate;
  final double? dueMileage;
  final String priority;

  factory UpcomingServiceItem.fromJson(Map<String, dynamic> json) {
    return UpcomingServiceItem(
      scheduleId: json['scheduleId'] as int? ?? json['ScheduleId'] as int?,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String? ?? '',
      vehicleRegistration: json['vehicleRegistration'] as String? ??
          json['VehicleRegistration'] as String?,
      serviceType: json['serviceType'] as String? ??
          json['ServiceType'] as String? ??
          '',
      dueDate: _date(json['dueDate'] ?? json['DueDate']),
      dueMileage: (json['dueMileage'] as num? ?? json['DueMileage'] as num?)
          ?.toDouble(),
      priority: json['priority'] as String? ?? json['Priority'] as String? ?? '',
    );
  }
}

class PredictiveMaintenanceAlert {
  const PredictiveMaintenanceAlert({
    required this.vehicleId,
    required this.vehicleName,
    required this.category,
    required this.severity,
    required this.status,
    required this.title,
    required this.message,
    this.vehicleRegistration,
    this.predictedDueDate,
    this.dueMileage,
    this.progressPercent,
    this.remainingKm,
  });

  final int vehicleId;
  final String vehicleName;
  final String? vehicleRegistration;
  final String category;
  final String severity;
  final String status;
  final String title;
  final String message;
  final DateTime? predictedDueDate;
  final double? dueMileage;
  final int? progressPercent;
  final double? remainingKm;

  factory PredictiveMaintenanceAlert.fromJson(Map<String, dynamic> json) {
    return PredictiveMaintenanceAlert(
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      vehicleName: json['vehicleName'] as String? ??
          json['VehicleName'] as String? ??
          '',
      vehicleRegistration: json['vehicleRegistration'] as String? ??
          json['VehicleRegistration'] as String?,
      category:
          json['category'] as String? ?? json['Category'] as String? ?? '',
      severity:
          json['severity'] as String? ?? json['Severity'] as String? ?? '',
      status: json['status'] as String? ?? json['Status'] as String? ?? '',
      title: json['title'] as String? ?? json['Title'] as String? ?? '',
      message: json['message'] as String? ?? json['Message'] as String? ?? '',
      predictedDueDate:
          _date(json['predictedDueDate'] ?? json['PredictedDueDate']),
      dueMileage: (json['dueMileage'] as num? ?? json['DueMileage'] as num?)
          ?.toDouble(),
      progressPercent: json['progressPercent'] as int? ??
          json['ProgressPercent'] as int?,
      remainingKm:
          (json['remainingKm'] as num? ?? json['RemainingKm'] as num?)
              ?.toDouble(),
    );
  }

  String get categoryLabel => switch (category) {
        'OilChange' => 'Oil',
        'TireWear' => 'Tires',
        'EngineLife' => 'Engine',
        _ => category,
      };
}

class MaintenanceDashboard {
  const MaintenanceDashboard({
    required this.kpis,
    this.criticalAlerts = const [],
    this.recentWorkOrders = const [],
    this.upcomingServices = const [],
    this.predictiveAlerts = const [],
  });

  final MaintenanceKpis kpis;
  final List<MaintenanceAlertItem> criticalAlerts;
  final List<WorkOrderItem> recentWorkOrders;
  final List<UpcomingServiceItem> upcomingServices;
  final List<PredictiveMaintenanceAlert> predictiveAlerts;

  factory MaintenanceDashboard.fromJson(Map<String, dynamic> json) {
    final kpisRaw = json['kpis'] ?? json['Kpis'];
    final alertsRaw = json['criticalAlerts'] ?? json['CriticalAlerts'];
    final recentRaw = json['recentWorkOrders'] ?? json['RecentWorkOrders'];
    final upcomingRaw = json['upcomingServices'] ?? json['UpcomingServices'];
    final predictiveRaw =
        json['predictiveAlerts'] ?? json['PredictiveAlerts'];

    return MaintenanceDashboard(
      kpis: kpisRaw is Map
          ? MaintenanceKpis.fromJson(Map<String, dynamic>.from(kpisRaw))
          : MaintenanceKpis.empty,
      criticalAlerts: _mapList(alertsRaw, MaintenanceAlertItem.fromJson),
      recentWorkOrders: _mapList(recentRaw, WorkOrderItem.fromJson),
      upcomingServices: _mapList(upcomingRaw, UpcomingServiceItem.fromJson),
      predictiveAlerts:
          _mapList(predictiveRaw, PredictiveMaintenanceAlert.fromJson),
    );
  }

  static const empty = MaintenanceDashboard(kpis: MaintenanceKpis.empty);
}

class MaintenanceRequestStats {
  const MaintenanceRequestStats({
    required this.open,
    required this.approved,
    required this.inProgress,
    required this.pendingApproval,
  });

  final int open;
  final int approved;
  final int inProgress;
  final int pendingApproval;

  factory MaintenanceRequestStats.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) =>
        json[a] as int? ?? (b != null ? json[b] as int? : null) ?? 0;
    return MaintenanceRequestStats(
      open: n('open', 'Open'),
      approved: n('approved', 'Approved'),
      inProgress: n('inProgress', 'InProgress'),
      pendingApproval: n('pendingApproval', 'PendingApproval'),
    );
  }

  static const empty = MaintenanceRequestStats(
    open: 0,
    approved: 0,
    inProgress: 0,
    pendingApproval: 0,
  );
}

class WorkOrderStats {
  const WorkOrderStats({
    required this.open,
    required this.inProgress,
    required this.completed,
    required this.cancelled,
  });

  final int open;
  final int inProgress;
  final int completed;
  final int cancelled;

  factory WorkOrderStats.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) =>
        json[a] as int? ?? (b != null ? json[b] as int? : null) ?? 0;
    return WorkOrderStats(
      open: n('open', 'Open'),
      inProgress: n('inProgress', 'InProgress'),
      completed: n('completed', 'Completed'),
      cancelled: n('cancelled', 'Cancelled'),
    );
  }

  static const empty = WorkOrderStats(
    open: 0,
    inProgress: 0,
    completed: 0,
    cancelled: 0,
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
    this.breakdownLocation,
    this.driverRemarks,
    this.workOrderId,
    this.rejectionReason,
    this.approvedBy,
    this.approvedAt,
    this.rejectedBy,
    this.rejectedAt,
    this.branchName,
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
  final String? breakdownLocation;
  final String? driverRemarks;
  final String status;
  final int? workOrderId;
  final DateTime createdAt;
  final String? rejectionReason;
  final String? approvedBy;
  final DateTime? approvedAt;
  final String? rejectedBy;
  final DateTime? rejectedAt;
  final String? branchName;

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
      breakdownLocation: json['breakdownLocation'] as String? ??
          json['BreakdownLocation'] as String?,
      driverRemarks: json['driverRemarks'] as String? ??
          json['DriverRemarks'] as String?,
      status: json['status'] as String? ?? json['Status'] as String? ?? '',
      workOrderId: json['workOrderId'] as int? ?? json['WorkOrderId'] as int?,
      createdAt:
          _date(json['createdAt'] ?? json['CreatedAt']) ?? DateTime.now(),
      rejectionReason: json['rejectionReason'] as String? ??
          json['RejectionReason'] as String?,
      approvedBy:
          json['approvedBy'] as String? ?? json['ApprovedBy'] as String?,
      approvedAt: _date(json['approvedAt'] ?? json['ApprovedAt']),
      rejectedBy:
          json['rejectedBy'] as String? ?? json['RejectedBy'] as String?,
      rejectedAt: _date(json['rejectedAt'] ?? json['RejectedAt']),
      branchName:
          json['branchName'] as String? ?? json['BranchName'] as String?,
    );
  }

  bool get canApprove =>
      status == 'Open' || status == 'PendingApproval';
  bool get canConvert =>
      status == 'Approved' || status == 'Open' || status == 'PendingApproval';
}

class WorkOrderPartUsage {
  const WorkOrderPartUsage({
    required this.partId,
    required this.partName,
    required this.quantity,
    required this.unitCost,
    required this.totalCost,
  });

  final int partId;
  final String partName;
  final int quantity;
  final double unitCost;
  final double totalCost;

  factory WorkOrderPartUsage.fromJson(Map<String, dynamic> json) {
    return WorkOrderPartUsage(
      partId: json['partId'] as int? ?? json['PartId'] as int? ?? 0,
      partName:
          json['partName'] as String? ?? json['PartName'] as String? ?? '',
      quantity: json['quantity'] as int? ?? json['Quantity'] as int? ?? 0,
      unitCost:
          (json['unitCost'] as num? ?? json['UnitCost'] as num? ?? 0).toDouble(),
      totalCost:
          (json['totalCost'] as num? ?? json['TotalCost'] as num? ?? 0)
              .toDouble(),
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
    this.requestId,
    this.vehicleName,
    this.vehicleRegistration,
    this.driverName,
    this.serviceTypeName,
    this.priority,
    this.maintenanceType,
    this.laborCost = 0,
    this.partsCost = 0,
    this.workshopName,
    this.technicianName,
    this.notes,
    this.technicianNotes,
    this.startDate,
    this.estimatedCompletionDate,
    this.completedAt,
    this.partsUsage = const [],
  });

  final int id;
  final String workOrderNumber;
  final int? requestId;
  final int vehicleId;
  final String? vehicleName;
  final String? vehicleRegistration;
  final String? driverName;
  final String? serviceTypeName;
  final String status;
  final String? priority;
  final String? maintenanceType;
  final double laborCost;
  final double partsCost;
  final double totalCost;
  final String? workshopName;
  final String? technicianName;
  final String? notes;
  final String? technicianNotes;
  final DateTime? startDate;
  final DateTime? estimatedCompletionDate;
  final DateTime? completedAt;
  final DateTime createdAt;
  final List<WorkOrderPartUsage> partsUsage;

  factory WorkOrderItem.fromJson(Map<String, dynamic> json) {
    final partsRaw = json['partsUsage'] ?? json['PartsUsage'];
    return WorkOrderItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      workOrderNumber: json['workOrderNumber'] as String? ??
          json['WorkOrderNumber'] as String? ??
          '',
      requestId: json['requestId'] as int? ?? json['RequestId'] as int?,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      vehicleRegistration: json['vehicleRegistration'] as String? ??
          json['VehicleRegistration'] as String?,
      driverName: json['driverName'] as String? ?? json['DriverName'] as String?,
      serviceTypeName: json['serviceTypeName'] as String? ??
          json['ServiceTypeName'] as String?,
      status: json['status'] as String? ?? json['Status'] as String? ?? '',
      priority: json['priority'] as String? ?? json['Priority'] as String?,
      maintenanceType: json['maintenanceType'] as String? ??
          json['MaintenanceType'] as String?,
      laborCost:
          (json['laborCost'] as num? ?? json['LaborCost'] as num? ?? 0)
              .toDouble(),
      partsCost:
          (json['partsCost'] as num? ?? json['PartsCost'] as num? ?? 0)
              .toDouble(),
      totalCost: (json['totalCost'] as num? ?? json['TotalCost'] as num? ?? 0)
          .toDouble(),
      workshopName:
          json['workshopName'] as String? ?? json['WorkshopName'] as String?,
      technicianName:
          json['technicianName'] as String? ?? json['TechnicianName'] as String?,
      notes: json['notes'] as String? ?? json['Notes'] as String?,
      technicianNotes: json['technicianNotes'] as String? ??
          json['TechnicianNotes'] as String?,
      startDate: _date(json['startDate'] ?? json['StartDate']),
      estimatedCompletionDate: _date(
          json['estimatedCompletionDate'] ?? json['EstimatedCompletionDate']),
      completedAt: _date(json['completedAt'] ?? json['CompletedAt']),
      createdAt:
          _date(json['createdAt'] ?? json['CreatedAt']) ?? DateTime.now(),
      partsUsage: _mapList(partsRaw, WorkOrderPartUsage.fromJson),
    );
  }

  bool get canComplete =>
      status != 'Completed' &&
      status != 'Closed' &&
      status != 'Cancelled';
}

class MaintenanceScheduleItem {
  const MaintenanceScheduleItem({
    required this.id,
    required this.vehicleId,
    required this.serviceTypeName,
    required this.intervalType,
    required this.intervalValue,
    required this.status,
    required this.priority,
    required this.isActive,
    this.vehicleName,
    this.vehicleRegistration,
    this.currentMileage = 0,
    this.nextServiceMileage,
    this.dueDate,
    this.lastServiceDate,
  });

  final int id;
  final int vehicleId;
  final String? vehicleName;
  final String? vehicleRegistration;
  final double currentMileage;
  final double? nextServiceMileage;
  final DateTime? dueDate;
  final String serviceTypeName;
  final String intervalType;
  final int intervalValue;
  final String status;
  final String priority;
  final bool isActive;
  final DateTime? lastServiceDate;

  factory MaintenanceScheduleItem.fromJson(Map<String, dynamic> json) {
    return MaintenanceScheduleItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      vehicleRegistration: json['vehicleRegistration'] as String? ??
          json['VehicleRegistration'] as String?,
      currentMileage: (json['currentMileage'] as num? ??
              json['CurrentMileage'] as num? ??
              0)
          .toDouble(),
      nextServiceMileage: (json['nextServiceMileage'] as num? ??
              json['NextServiceMileage'] as num?)
          ?.toDouble(),
      dueDate: _date(json['dueDate'] ?? json['DueDate']),
      serviceTypeName: json['serviceTypeName'] as String? ??
          json['ServiceTypeName'] as String? ??
          '',
      intervalType: json['intervalType'] as String? ??
          json['IntervalType'] as String? ??
          '',
      intervalValue:
          json['intervalValue'] as int? ?? json['IntervalValue'] as int? ?? 0,
      status: json['status'] as String? ?? json['Status'] as String? ?? '',
      priority: json['priority'] as String? ?? json['Priority'] as String? ?? '',
      isActive: json['isActive'] as bool? ?? json['IsActive'] as bool? ?? true,
      lastServiceDate:
          _date(json['lastServiceDate'] ?? json['LastServiceDate']),
    );
  }
}

class PagedList<T> {
  const PagedList({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
  });

  final List<T> items;
  final int totalCount;
  final int page;
  final int pageSize;

  bool get hasMore => items.length < totalCount;

  static PagedList<T> empty<T>() =>
      PagedList<T>(items: const [], totalCount: 0, page: 1, pageSize: 50);
}

List<T> _mapList<T>(
  Object? raw,
  T Function(Map<String, dynamic>) map,
) {
  if (raw is! List) return <T>[];
  return raw
      .whereType<Map>()
      .map((e) => map(Map<String, dynamic>.from(e)))
      .toList();
}

DateTime? _date(Object? raw) {
  if (raw == null) return null;
  if (raw is DateTime) return raw;
  return DateTime.tryParse(raw.toString());
}
