class GpsAlertEvent {
  const GpsAlertEvent({
    required this.id,
    required this.vehicleId,
    required this.eventType,
    required this.latitude,
    required this.longitude,
    required this.speed,
    required this.message,
    required this.timestamp,
    required this.isAcknowledged,
    required this.severity,
    required this.status,
    this.ruleId,
    this.vehicleName,
    this.registrationNumber,
    this.geofenceId,
    this.geofenceName,
    this.driverId,
    this.driverName,
    this.readAt,
    this.readBy,
    this.acknowledgedAt,
    this.acknowledgedBy,
    this.resolvedAt,
    this.resolvedBy,
    this.resolutionNotes,
    this.archivedAt,
    this.archivedBy,
    this.assignedToUserId,
    this.assignedToName,
    this.assignedAt,
    this.assignedBy,
    this.canAcknowledge = false,
    this.canResolve = false,
    this.canArchive = false,
    this.canDelete = false,
    this.canAssign = false,
  });

  final int id;
  final int? ruleId;
  final int vehicleId;
  final String? vehicleName;
  final String? registrationNumber;
  final String eventType;
  final double latitude;
  final double longitude;
  final double speed;
  final String message;
  final DateTime timestamp;
  final bool isAcknowledged;
  final String severity;
  final String status;
  final int? geofenceId;
  final String? geofenceName;
  final int? driverId;
  final String? driverName;
  final DateTime? readAt;
  final String? readBy;
  final DateTime? acknowledgedAt;
  final String? acknowledgedBy;
  final DateTime? resolvedAt;
  final String? resolvedBy;
  final String? resolutionNotes;
  final DateTime? archivedAt;
  final String? archivedBy;
  final int? assignedToUserId;
  final String? assignedToName;
  final DateTime? assignedAt;
  final String? assignedBy;
  final bool canAcknowledge;
  final bool canResolve;
  final bool canArchive;
  final bool canDelete;
  final bool canAssign;

  bool get isUnread => readAt == null && !isArchived;
  bool get isArchived => status.toLowerCase() == 'archived';
  bool get isResolved => status.toLowerCase() == 'resolved';

  /// True when the alert is still actionable (not resolved/archived).
  bool get isOpen => !isResolved && !isArchived;
  bool get isRead => readAt != null;
  bool get canMarkRead => isUnread && !isArchived;
  bool get isAssigned =>
      assignedToUserId != null ||
      (assignedToName != null && assignedToName!.trim().isNotEmpty);

  String get vehicleLabel {
    final plate = registrationNumber?.trim();
    if (plate != null && plate.isNotEmpty) return plate;
    final name = vehicleName?.trim();
    if (name != null && name.isNotEmpty) return name;
    return 'Vehicle #$vehicleId';
  }

  factory GpsAlertEvent.fromJson(Map<String, dynamic> json) {
    int i(dynamic v) => (v as num?)?.toInt() ?? 0;
    int? iOrNull(dynamic v) => (v as num?)?.toInt();
    return GpsAlertEvent(
      id: i(json['id'] ?? json['Id']),
      ruleId: iOrNull(json['ruleId'] ?? json['RuleId']),
      vehicleId: i(json['vehicleId'] ?? json['VehicleId']),
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      registrationNumber: json['registrationNumber'] as String? ??
          json['RegistrationNumber'] as String?,
      eventType:
          json['eventType'] as String? ?? json['EventType'] as String? ?? '',
      latitude:
          (json['latitude'] as num? ?? json['Latitude'] as num? ?? 0).toDouble(),
      longitude: (json['longitude'] as num? ?? json['Longitude'] as num? ?? 0)
          .toDouble(),
      speed: (json['speed'] as num? ?? json['Speed'] as num? ?? 0).toDouble(),
      message: json['message'] as String? ?? json['Message'] as String? ?? '',
      timestamp: DateTime.tryParse(
            (json['timestamp'] ?? json['Timestamp'] ?? '').toString(),
          ) ??
          DateTime.now().toUtc(),
      isAcknowledged: json['isAcknowledged'] as bool? ??
          json['IsAcknowledged'] as bool? ??
          false,
      severity: json['severity'] as String? ?? json['Severity'] as String? ?? '',
      status: json['status'] as String? ?? json['Status'] as String? ?? '',
      geofenceId: iOrNull(json['geofenceId'] ?? json['GeofenceId']),
      geofenceName:
          json['geofenceName'] as String? ?? json['GeofenceName'] as String?,
      driverId: iOrNull(json['driverId'] ?? json['DriverId']),
      driverName: json['driverName'] as String? ?? json['DriverName'] as String?,
      readAt: DateTime.tryParse((json['readAt'] ?? json['ReadAt'] ?? '').toString()),
      readBy: json['readBy'] as String? ?? json['ReadBy'] as String?,
      acknowledgedAt: DateTime.tryParse(
        (json['acknowledgedAt'] ?? json['AcknowledgedAt'] ?? '').toString(),
      ),
      acknowledgedBy:
          json['acknowledgedBy'] as String? ?? json['AcknowledgedBy'] as String?,
      resolvedAt: DateTime.tryParse(
        (json['resolvedAt'] ?? json['ResolvedAt'] ?? '').toString(),
      ),
      resolvedBy: json['resolvedBy'] as String? ?? json['ResolvedBy'] as String?,
      resolutionNotes: json['resolutionNotes'] as String? ??
          json['ResolutionNotes'] as String?,
      archivedAt: DateTime.tryParse(
        (json['archivedAt'] ?? json['ArchivedAt'] ?? '').toString(),
      ),
      archivedBy: json['archivedBy'] as String? ?? json['ArchivedBy'] as String?,
      assignedToUserId:
          iOrNull(json['assignedToUserId'] ?? json['AssignedToUserId']),
      assignedToName: json['assignedToName'] as String? ??
          json['AssignedToName'] as String?,
      assignedAt: DateTime.tryParse(
        (json['assignedAt'] ?? json['AssignedAt'] ?? '').toString(),
      ),
      assignedBy: json['assignedBy'] as String? ?? json['AssignedBy'] as String?,
      canAcknowledge: json['canAcknowledge'] as bool? ??
          json['CanAcknowledge'] as bool? ??
          false,
      canResolve:
          json['canResolve'] as bool? ?? json['CanResolve'] as bool? ?? false,
      canArchive:
          json['canArchive'] as bool? ?? json['CanArchive'] as bool? ?? false,
      canDelete:
          json['canDelete'] as bool? ?? json['CanDelete'] as bool? ?? false,
      canAssign:
          json['canAssign'] as bool? ?? json['CanAssign'] as bool? ?? false,
    );
  }

  GpsAlertEvent copyWith({
    bool? isAcknowledged,
    String? status,
    DateTime? acknowledgedAt,
    String? acknowledgedBy,
    DateTime? resolvedAt,
    String? resolvedBy,
    String? resolutionNotes,
    int? assignedToUserId,
    String? assignedToName,
    DateTime? assignedAt,
    String? assignedBy,
    bool? canAcknowledge,
    bool? canResolve,
    bool? canArchive,
    bool? canAssign,
    bool clearAssignee = false,
  }) {
    return GpsAlertEvent(
      id: id,
      ruleId: ruleId,
      vehicleId: vehicleId,
      vehicleName: vehicleName,
      registrationNumber: registrationNumber,
      eventType: eventType,
      latitude: latitude,
      longitude: longitude,
      speed: speed,
      message: message,
      timestamp: timestamp,
      isAcknowledged: isAcknowledged ?? this.isAcknowledged,
      severity: severity,
      status: status ?? this.status,
      geofenceId: geofenceId,
      geofenceName: geofenceName,
      driverId: driverId,
      driverName: driverName,
      readAt: readAt,
      readBy: readBy,
      acknowledgedAt: acknowledgedAt ?? this.acknowledgedAt,
      acknowledgedBy: acknowledgedBy ?? this.acknowledgedBy,
      resolvedAt: resolvedAt ?? this.resolvedAt,
      resolvedBy: resolvedBy ?? this.resolvedBy,
      resolutionNotes: resolutionNotes ?? this.resolutionNotes,
      archivedAt: archivedAt,
      archivedBy: archivedBy,
      assignedToUserId:
          clearAssignee ? null : (assignedToUserId ?? this.assignedToUserId),
      assignedToName:
          clearAssignee ? null : (assignedToName ?? this.assignedToName),
      assignedAt: clearAssignee ? null : (assignedAt ?? this.assignedAt),
      assignedBy: clearAssignee ? null : (assignedBy ?? this.assignedBy),
      canAcknowledge: canAcknowledge ?? this.canAcknowledge,
      canResolve: canResolve ?? this.canResolve,
      canArchive: canArchive ?? this.canArchive,
      canDelete: canDelete,
      canAssign: canAssign ?? this.canAssign,
    );
  }
}

class GpsAlertStats {
  const GpsAlertStats({
    required this.total,
    required this.today,
    required this.unread,
    required this.active,
    required this.resolved,
    required this.critical,
    required this.archived,
    this.warning = 0,
    this.info = 0,
    this.unacknowledged = 0,
  });

  final int total;
  final int today;
  final int unread;
  final int active;
  final int resolved;
  final int critical;
  final int archived;
  final int warning;
  final int info;
  final int unacknowledged;

  factory GpsAlertStats.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) {
      num? v = json[a] as num?;
      v ??= b != null ? json[b] as num? : null;
      return v?.toInt() ?? 0;
    }
    return GpsAlertStats(
      total: n('total', 'Total'),
      today: n('today', 'Today'),
      unread: n('unread', 'Unread'),
      active: n('active', 'Active'),
      resolved: n('resolved', 'Resolved'),
      critical: n('critical', 'Critical'),
      archived: n('archived', 'Archived'),
      warning: n('warning', 'Warning'),
      info: n('info', 'Info'),
      unacknowledged: n('unacknowledged', 'Unacknowledged'),
    );
  }

  static const empty = GpsAlertStats(
    total: 0,
    today: 0,
    unread: 0,
    active: 0,
    resolved: 0,
    critical: 0,
    archived: 0,
  );
}
