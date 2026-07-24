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
    this.canAcknowledge = false,
    this.canResolve = false,
    this.canArchive = false,
    this.canDelete = false,
  });

  final int id;
  final int? ruleId;
  final int vehicleId;
  final String? vehicleName;
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
  final bool canAcknowledge;
  final bool canResolve;
  final bool canArchive;
  final bool canDelete;

  bool get isUnread => readAt == null && !isArchived;
  bool get isArchived => status.toLowerCase() == 'archived';
  bool get isResolved => status.toLowerCase() == 'resolved';
  /// True when the alert is still actionable (not resolved/archived).
  bool get isOpen => !isResolved && !isArchived;
  bool get isRead => readAt != null;
  bool get canMarkRead => isUnread && !isArchived;

  factory GpsAlertEvent.fromJson(Map<String, dynamic> json) {
    return GpsAlertEvent(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      ruleId: json['ruleId'] as int? ?? json['RuleId'] as int?,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int? ?? 0,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
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
      geofenceId: json['geofenceId'] as int? ?? json['GeofenceId'] as int?,
      geofenceName:
          json['geofenceName'] as String? ?? json['GeofenceName'] as String?,
      driverId: json['driverId'] as int? ?? json['DriverId'] as int?,
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
      canAcknowledge: json['canAcknowledge'] as bool? ??
          json['CanAcknowledge'] as bool? ??
          false,
      canResolve:
          json['canResolve'] as bool? ?? json['CanResolve'] as bool? ?? false,
      canArchive:
          json['canArchive'] as bool? ?? json['CanArchive'] as bool? ?? false,
      canDelete:
          json['canDelete'] as bool? ?? json['CanDelete'] as bool? ?? false,
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
  });

  final int total;
  final int today;
  final int unread;
  final int active;
  final int resolved;
  final int critical;
  final int archived;

  factory GpsAlertStats.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) =>
        json[a] as int? ?? (b != null ? json[b] as int? : null) ?? 0;
    return GpsAlertStats(
      total: n('total', 'Total'),
      today: n('today', 'Today'),
      unread: n('unread', 'Unread'),
      active: n('active', 'Active'),
      resolved: n('resolved', 'Resolved'),
      critical: n('critical', 'Critical'),
      archived: n('archived', 'Archived'),
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
