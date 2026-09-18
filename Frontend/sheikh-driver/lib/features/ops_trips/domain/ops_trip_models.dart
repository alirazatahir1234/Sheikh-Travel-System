class OpsTripListItem {
  const OpsTripListItem({
    required this.id,
    required this.tripNumber,
    required this.tripDate,
    required this.plannedStart,
    required this.status,
    required this.priority,
    required this.tripType,
    this.bookingNumber,
    this.customerName,
    this.driverId,
    this.driverName,
    this.vehicleId,
    this.vehicleName,
    this.routeName,
    this.pickupAddress,
    this.destinationAddress,
    this.plannedEnd,
    this.gpsOnline = false,
  });

  final int id;
  final String tripNumber;
  final DateTime tripDate;
  final DateTime plannedStart;
  final DateTime? plannedEnd;
  final String status;
  final String priority;
  final String tripType;
  final String? bookingNumber;
  final String? customerName;
  final int? driverId;
  final String? driverName;
  final int? vehicleId;
  final String? vehicleName;
  final String? routeName;
  final String? pickupAddress;
  final String? destinationAddress;
  final bool gpsOnline;

  factory OpsTripListItem.fromJson(Map<String, dynamic> json) {
    return OpsTripListItem(
      id: _asInt(json['id'] ?? json['Id']),
      tripNumber:
          json['tripNumber'] as String? ?? json['TripNumber'] as String? ?? '',
      tripDate: _date(json['tripDate'] ?? json['TripDate']) ?? DateTime.now(),
      plannedStart:
          _date(json['plannedStart'] ?? json['PlannedStart']) ?? DateTime.now(),
      plannedEnd: _date(json['plannedEnd'] ?? json['PlannedEnd']),
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      priority: (json['priority'] ?? json['Priority'] ?? 'Normal').toString(),
      tripType: (json['tripType'] ?? json['TripType'] ?? '').toString(),
      bookingNumber:
          json['bookingNumber'] as String? ?? json['BookingNumber'] as String?,
      customerName:
          json['customerName'] as String? ?? json['CustomerName'] as String?,
      driverId: _asIntOrNull(json['driverId'] ?? json['DriverId']),
      driverName: json['driverName'] as String? ?? json['DriverName'] as String?,
      vehicleId: _asIntOrNull(json['vehicleId'] ?? json['VehicleId']),
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      routeName: json['routeName'] as String? ?? json['RouteName'] as String?,
      pickupAddress:
          json['pickupAddress'] as String? ?? json['PickupAddress'] as String?,
      destinationAddress: json['destinationAddress'] as String? ??
          json['DestinationAddress'] as String?,
      gpsOnline: json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
    );
  }
}

class OpsTripDetail {
  const OpsTripDetail({
    required this.id,
    required this.tripNumber,
    required this.tripName,
    required this.status,
    required this.priority,
    required this.tripType,
    required this.tripDate,
    required this.plannedStart,
    required this.passengerCount,
    required this.openAlertCount,
    this.bookingNumber,
    this.customerName,
    this.driverId,
    this.driverName,
    this.vehicleId,
    this.vehicleName,
    this.routeName,
    this.pickupAddress,
    this.destinationAddress,
    this.plannedEnd,
    this.actualStart,
    this.actualEnd,
    this.driverNotes,
    this.gpsOnline = false,
    this.timeline = const [],
  });

  final int id;
  final String tripNumber;
  final String tripName;
  final String status;
  final String priority;
  final String tripType;
  final DateTime tripDate;
  final DateTime plannedStart;
  final DateTime? plannedEnd;
  final DateTime? actualStart;
  final DateTime? actualEnd;
  final int passengerCount;
  final int openAlertCount;
  final String? bookingNumber;
  final String? customerName;
  final int? driverId;
  final String? driverName;
  final int? vehicleId;
  final String? vehicleName;
  final String? routeName;
  final String? pickupAddress;
  final String? destinationAddress;
  final String? driverNotes;
  final bool gpsOnline;
  final List<OpsTripTimelineEvent> timeline;

  factory OpsTripDetail.fromJson(Map<String, dynamic> json) {
    final rawTimeline = json['timeline'] ?? json['Timeline'];
    final timeline = rawTimeline is List
        ? rawTimeline
            .whereType<Map>()
            .map((e) => OpsTripTimelineEvent.fromJson(Map<String, dynamic>.from(e)))
            .toList()
        : const <OpsTripTimelineEvent>[];

    return OpsTripDetail(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      tripNumber:
          json['tripNumber'] as String? ?? json['TripNumber'] as String? ?? '',
      tripName: json['tripName'] as String? ?? json['TripName'] as String? ?? '',
      status: (json['status'] ?? json['Status'] ?? '').toString(),
      priority: (json['priority'] ?? json['Priority'] ?? 'Normal').toString(),
      tripType: (json['tripType'] ?? json['TripType'] ?? '').toString(),
      tripDate: _date(json['tripDate'] ?? json['TripDate']) ?? DateTime.now(),
      plannedStart:
          _date(json['plannedStart'] ?? json['PlannedStart']) ?? DateTime.now(),
      plannedEnd: _date(json['plannedEnd'] ?? json['PlannedEnd']),
      actualStart: _date(json['actualStart'] ?? json['ActualStart']),
      actualEnd: _date(json['actualEnd'] ?? json['ActualEnd']),
      passengerCount:
          json['passengerCount'] as int? ?? json['PassengerCount'] as int? ?? 0,
      openAlertCount:
          json['openAlertCount'] as int? ?? json['OpenAlertCount'] as int? ?? 0,
      bookingNumber:
          json['bookingNumber'] as String? ?? json['BookingNumber'] as String?,
      customerName:
          json['customerName'] as String? ?? json['CustomerName'] as String?,
      driverId: json['driverId'] as int? ?? json['DriverId'] as int?,
      driverName: json['driverName'] as String? ?? json['DriverName'] as String?,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int?,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      routeName: json['routeName'] as String? ?? json['RouteName'] as String?,
      pickupAddress:
          json['pickupAddress'] as String? ?? json['PickupAddress'] as String?,
      destinationAddress: json['destinationAddress'] as String? ??
          json['DestinationAddress'] as String?,
      driverNotes:
          json['driverNotes'] as String? ?? json['DriverNotes'] as String?,
      gpsOnline: json['gpsOnline'] as bool? ?? json['GpsOnline'] as bool? ?? false,
      timeline: timeline,
    );
  }
}

class OpsTripTimelineEvent {
  const OpsTripTimelineEvent({
    required this.id,
    required this.toStatus,
    required this.changedAtUtc,
    this.fromStatus,
    this.changedBy,
    this.note,
  });

  final int id;
  final String? fromStatus;
  final String toStatus;
  final DateTime changedAtUtc;
  final String? changedBy;
  final String? note;

  factory OpsTripTimelineEvent.fromJson(Map<String, dynamic> json) {
    return OpsTripTimelineEvent(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      fromStatus: json['fromStatus']?.toString() ?? json['FromStatus']?.toString(),
      toStatus: (json['toStatus'] ?? json['ToStatus'] ?? '').toString(),
      changedAtUtc: _date(json['changedAtUtc'] ?? json['ChangedAtUtc']) ??
          DateTime.now().toUtc(),
      changedBy: json['changedBy'] as String? ?? json['ChangedBy'] as String?,
      note: json['note'] as String? ?? json['Note'] as String?,
    );
  }
}

class OpsTripsDashboard {
  const OpsTripsDashboard({
    this.total = 0,
    this.scheduled = 0,
    this.inProgress = 0,
    this.completed = 0,
    this.cancelled = 0,
    this.delayed = 0,
  });

  final int total;
  final int scheduled;
  final int inProgress;
  final int completed;
  final int cancelled;
  final int delayed;

  factory OpsTripsDashboard.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) {
      final v = json[a] ?? (b != null ? json[b] : null);
      if (v is int) return v;
      if (v is num) return v.toInt();
      if (v is String) return int.tryParse(v) ?? 0;
      return 0;
    }

    // Prefer backend TripDashboardDto keys (totalTrips / scheduledTrips / …).
    return OpsTripsDashboard(
      total: _firstNonZero([
        n('totalTrips', 'TotalTrips'),
        n('total', 'Total'),
      ]),
      scheduled: _firstNonZero([
        n('scheduledTrips', 'ScheduledTrips'),
        n('scheduled', 'Scheduled'),
      ]),
      inProgress: _firstNonZero([
        n('ongoingTrips', 'OngoingTrips'),
        n('inProgress', 'InProgress'),
        n('started', 'Started') + n('enroute', 'Enroute'),
      ]),
      completed: _firstNonZero([
        n('completedTrips', 'CompletedTrips'),
        n('completed', 'Completed'),
      ]),
      cancelled: _firstNonZero([
        n('cancelledTrips', 'CancelledTrips'),
        n('cancelled', 'Cancelled'),
      ]),
      delayed: _firstNonZero([
        n('delayedTrips', 'DelayedTrips'),
        n('delayed', 'Delayed'),
      ]),
    );
  }

  static int _firstNonZero(List<int> values) {
    for (final v in values) {
      if (v != 0) return v;
    }
    return values.isEmpty ? 0 : values.first;
  }

  static const empty = OpsTripsDashboard();
}

DateTime? _date(Object? raw) {
  if (raw == null) return null;
  if (raw is DateTime) return raw;
  return DateTime.tryParse(raw.toString());
}

int _asInt(Object? raw) => _asIntOrNull(raw) ?? 0;

int? _asIntOrNull(Object? raw) {
  if (raw == null) return null;
  if (raw is int) return raw;
  if (raw is num) return raw.toInt();
  return int.tryParse(raw.toString());
}
