class BookingListItem {
  const BookingListItem({
    required this.id,
    required this.bookingNumber,
    required this.customerId,
    required this.routeId,
    required this.pickupTime,
    required this.passengerCount,
    required this.totalAmount,
    required this.status,
    required this.createdAt,
    this.customerName,
    this.routeName,
    this.vehicleId,
    this.vehicleName,
    this.driverId,
    this.driverName,
    this.dropoffTime,
    this.notes,
  });

  final int id;
  final String bookingNumber;
  final int customerId;
  final String? customerName;
  final int routeId;
  final String? routeName;
  final int? vehicleId;
  final String? vehicleName;
  final int? driverId;
  final String? driverName;
  final DateTime pickupTime;
  final DateTime? dropoffTime;
  final int passengerCount;
  final double totalAmount;
  final String status;
  final String? notes;
  final DateTime createdAt;

  bool get needsDispatch =>
      status.toLowerCase() == 'pending' ||
      status.toLowerCase() == 'confirmed';

  bool get isUnassigned => driverId == null || vehicleId == null;

  factory BookingListItem.fromJson(Map<String, dynamic> json) {
    return BookingListItem(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      bookingNumber: json['bookingNumber'] as String? ??
          json['BookingNumber'] as String? ??
          '',
      customerId: json['customerId'] as int? ?? json['CustomerId'] as int? ?? 0,
      customerName:
          json['customerName'] as String? ?? json['CustomerName'] as String?,
      routeId: json['routeId'] as int? ?? json['RouteId'] as int? ?? 0,
      routeName: json['routeName'] as String? ?? json['RouteName'] as String?,
      vehicleId: json['vehicleId'] as int? ?? json['VehicleId'] as int?,
      vehicleName:
          json['vehicleName'] as String? ?? json['VehicleName'] as String?,
      driverId: json['driverId'] as int? ?? json['DriverId'] as int?,
      driverName: json['driverName'] as String? ?? json['DriverName'] as String?,
      pickupTime: _date(json['pickupTime'] ?? json['PickupTime']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
      dropoffTime: _date(json['dropoffTime'] ?? json['DropoffTime']),
      passengerCount:
          json['passengerCount'] as int? ?? json['PassengerCount'] as int? ?? 0,
      totalAmount:
          (json['totalAmount'] as num? ?? json['TotalAmount'] as num? ?? 0)
              .toDouble(),
      status: '${json['status'] ?? json['Status'] ?? ''}',
      notes: json['notes'] as String? ?? json['Notes'] as String?,
      createdAt: _date(json['createdAt'] ?? json['CreatedAt']) ??
          DateTime.fromMillisecondsSinceEpoch(0),
    );
  }
}

typedef BookingDetail = BookingListItem;

DateTime? _date(Object? raw) {
  if (raw == null) return null;
  if (raw is DateTime) return raw;
  return DateTime.tryParse(raw.toString());
}
