import 'package:intl/intl.dart';

class Trip {
  const Trip({
    required this.id,
    required this.bookingNumber,
    required this.customerName,
    required this.routeName,
    required this.pickupTime,
    this.dropoffTime,
    required this.status,
    required this.statusName,
    this.vehicleId,
    this.vehicleName,
    required this.totalAmount,
    this.pickupAddress,
    this.pickupLatitude,
    this.pickupLongitude,
    this.dropoffAddress,
    this.dropLatitude,
    this.dropLongitude,
    this.googleMapsUrl,
    this.googleDirectionsUrl,
    this.tripId,
    this.bookingId,
    this.source = 'Booking',
    this.lifecycleStatus = 0,
    this.lifecycleStatusName = '',
    this.nextActions = const [],
    this.paidAmount = 0,
    this.balanceDue = 0,
    this.paymentRequired = false,
    this.paymentStatus = 'Pending',
  });

  /// Sentinel used when [pickupTime] is missing from the API payload.
  static final DateTime missingPickupTime =
      DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);

  final int id;
  final String bookingNumber;
  final String customerName;
  final String routeName;
  final DateTime pickupTime;
  final DateTime? dropoffTime;
  final int status;
  final String statusName;
  final int? vehicleId;
  final String? vehicleName;
  final double totalAmount;
  final String? pickupAddress;
  final double? pickupLatitude;
  final double? pickupLongitude;
  final String? dropoffAddress;
  final double? dropLatitude;
  final double? dropLongitude;
  final String? googleMapsUrl;
  final String? googleDirectionsUrl;
  final int? tripId;
  final int? bookingId;
  final String source;

  /// ERP TripStatus int.
  final int lifecycleStatus;
  final String lifecycleStatusName;
  final List<String> nextActions;
  final double paidAmount;
  final double balanceDue;
  final bool paymentRequired;
  final String paymentStatus;

  // Legacy booking status helpers (kept for list filters)
  bool get isConfirmed =>
      status == 2 ||
      lifecycleStatus == 2 ||
      lifecycleStatus == 3 ||
      lifecycleStatus == 4;
  bool get isStarted =>
      status == 3 ||
      lifecycleStatus == 5 ||
      lifecycleStatus == 6 ||
      lifecycleStatus == 7 ||
      lifecycleStatus == 8;
  bool get isCompleted => status == 4 || lifecycleStatus == 9;
  bool get isCancelled =>
      status == 5 || lifecycleStatus == 10 || lifecycleStatus == 11;
  bool get isActionable =>
      !isCompleted &&
      !isCancelled &&
      (nextActions.isNotEmpty || isConfirmed || isStarted);

  bool get canAccept => nextActions.contains('Accept');
  bool get canArrive => nextActions.contains('Arrived');
  bool get canOnboard => nextActions.contains('Onboard');
  bool get canComplete => nextActions.contains('Complete');

  /// Reject is only valid before acceptance (Accept + Reject).
  /// Backend may still list Reject after Started; never show it once accepted.
  bool get canReject =>
      nextActions.contains('Reject') && nextActions.contains('Accept');

  bool get hasPickupCoords =>
      pickupLatitude != null &&
      pickupLongitude != null &&
      !(pickupLatitude == 0 && pickupLongitude == 0);

  bool get hasDropCoords =>
      dropLatitude != null &&
      dropLongitude != null &&
      !(dropLatitude == 0 && dropLongitude == 0);

  /// True when the trip DTO already has at least one usable lat/lng pair.
  /// Map previews must not invent or geocode coordinates client-side.
  bool get hasMapPreviewCoords => hasPickupCoords || hasDropCoords;

  bool get canNavigate =>
      hasPickupCoords ||
      hasDropCoords ||
      (googleDirectionsUrl != null && googleDirectionsUrl!.isNotEmpty);

  /// After onboard / enroute, external Navigate should target drop-off.
  bool get shouldNavigateToDropoff =>
      lifecycleStatus == 7 || // Enroute
      canComplete;

  /// True when [pickupTime] came from the API (not the missing-date sentinel).
  bool get hasScheduledPickup =>
      pickupTime.millisecondsSinceEpoch !=
      missingPickupTime.millisecondsSinceEpoch;

  /// Pickup instant in the device local timezone (safe for UTC or local ISO).
  DateTime get pickupTimeLocal => pickupTime.toLocal();

  /// Drop-off instant in the device local timezone, if provided by the API.
  DateTime? get dropoffTimeLocal => dropoffTime?.toLocal();

  /// e.g. `Wednesday, 15 October 2026`
  String get scheduledDateLabel => hasScheduledPickup
      ? DateFormat('EEEE, d MMMM yyyy').format(pickupTimeLocal)
      : 'Date unavailable';

  /// Time-only under pickup (full date lives under booking ID).
  /// e.g. `12:36 PM` or `12:36 PM · Overdue`
  String get pickupDateTimeLabel {
    if (!hasScheduledPickup) return 'Time unavailable';
    final time = DateFormat('h:mm a').format(pickupTimeLocal);
    return isPickupOverdue ? '$time · Overdue' : time;
  }

  /// True when scheduled pickup is in the past and the trip is still active.
  bool get isPickupOverdue =>
      hasScheduledPickup &&
      !isCompleted &&
      !isCancelled &&
      pickupTimeLocal.isBefore(DateTime.now());

  /// Scheduled/estimated drop-off time only, or `ETA pending` (never invent).
  String get dropoffDateTimeLabel => dropoffTimeLocal != null
      ? DateFormat('h:mm a').format(dropoffTimeLocal!)
      : 'ETA pending';

  /// Id to use for lifecycle API calls (operational trip id preferred).
  int get actionId => tripId ?? id;

  /// Booking id for ETA / GPS endpoints that require `bookingId`.
  int get etaBookingId => bookingId ?? id;

  /// Stable route id for in-app navigation (list/detail/navigate/payment).
  int get routeId => id;

  bool matchesRouteId(int routeId) =>
      id == routeId || bookingId == routeId || tripId == routeId;

  factory Trip.fromJson(Map<String, dynamic> json) {
    final actionsRaw = json['nextActions'];
    final actions = actionsRaw is List
        ? actionsRaw.map((e) => e.toString()).toList()
        : <String>[];

    return Trip(
      id: (json['id'] as num?)?.toInt() ?? (json['Id'] as num?)?.toInt() ?? 0,
      bookingNumber: json['bookingNumber'] as String? ?? '',
      customerName: json['customerName'] as String? ?? '',
      routeName: json['routeName'] as String? ?? '',
      pickupTime: _parseDateTime(json['pickupTime'] ?? json['PickupTime']) ??
          missingPickupTime,
      dropoffTime: _parseDateTime(json['dropoffTime'] ?? json['DropoffTime']),
      status: json['status'] as int? ?? 0,
      statusName: json['statusName'] as String? ?? '',
      vehicleId: json['vehicleId'] as int?,
      vehicleName: json['vehicleName'] as String?,
      totalAmount: (json['totalAmount'] as num?)?.toDouble() ?? 0.0,
      pickupAddress: json['pickupAddress'] as String?,
      pickupLatitude: (json['pickupLatitude'] as num?)?.toDouble(),
      pickupLongitude: (json['pickupLongitude'] as num?)?.toDouble(),
      dropoffAddress: json['dropoffAddress'] as String?,
      dropLatitude: (json['dropLatitude'] as num?)?.toDouble(),
      dropLongitude: (json['dropLongitude'] as num?)?.toDouble(),
      googleMapsUrl: json['googleMapsUrl'] as String?,
      googleDirectionsUrl: json['googleDirectionsUrl'] as String?,
      tripId: json['tripId'] as int?,
      bookingId: json['bookingId'] as int?,
      source: json['source'] as String? ?? 'Booking',
      lifecycleStatus: json['lifecycleStatus'] as int? ?? 0,
      lifecycleStatusName: json['lifecycleStatusName'] as String? ??
          (json['statusName'] as String? ?? ''),
      nextActions: actions,
      paidAmount: (json['paidAmount'] as num?)?.toDouble() ?? 0.0,
      balanceDue: (json['balanceDue'] as num?)?.toDouble() ?? 0.0,
      paymentRequired: json['paymentRequired'] as bool? ?? false,
      paymentStatus: json['paymentStatus'] as String? ?? 'Pending',
    );
  }

  /// Parses API date strings. Keeps UTC when the payload includes a zone (`Z` /
  /// offset) so [toLocal] converts once; zone-less values stay as local wall time.
  static DateTime? _parseDateTime(Object? raw) {
    if (raw == null) return null;
    if (raw is DateTime) return raw;
    final text = raw.toString().trim();
    if (text.isEmpty) return null;
    return DateTime.tryParse(text);
  }
}
