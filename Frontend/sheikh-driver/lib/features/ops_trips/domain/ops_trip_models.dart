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
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
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
    int n(String a, [String? b]) =>
        json[a] as int? ?? (b != null ? json[b] as int? : null) ?? 0;
    // Backend may use varying keys — be flexible.
    return OpsTripsDashboard(
      total: n('total', 'Total') != 0
          ? n('total', 'Total')
          : n('totalTrips', 'TotalTrips'),
      scheduled: n('scheduled', 'Scheduled') != 0
          ? n('scheduled', 'Scheduled')
          : n('scheduledTrips', 'ScheduledTrips'),
      inProgress: n('inProgress', 'InProgress') != 0
          ? n('inProgress', 'InProgress')
          : n('ongoingTrips', 'OngoingTrips') != 0
              ? n('ongoingTrips', 'OngoingTrips')
              : n('started', 'Started') + n('enroute', 'Enroute'),
      completed: n('completed', 'Completed') != 0
          ? n('completed', 'Completed')
          : n('completedTrips', 'CompletedTrips'),
      cancelled: n('cancelled', 'Cancelled') != 0
          ? n('cancelled', 'Cancelled')
          : n('cancelledTrips', 'CancelledTrips'),
      delayed: n('delayed', 'Delayed') != 0
          ? n('delayed', 'Delayed')
          : n('delayedTrips', 'DelayedTrips'),
    );
  }

  static const empty = OpsTripsDashboard();
}

DateTime? _date(Object? raw) {
  if (raw == null) return null;
  if (raw is DateTime) return raw;
  return DateTime.tryParse(raw.toString());
}
