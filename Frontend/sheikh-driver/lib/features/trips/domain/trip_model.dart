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
  });

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

  bool get isConfirmed => status == 2;
  bool get isStarted => status == 3;
  bool get isCompleted => status == 4;
  bool get isCancelled => status == 5;
  bool get isActionable => isConfirmed || isStarted;

  factory Trip.fromJson(Map<String, dynamic> json) {
    return Trip(
      id: json['id'] as int,
      bookingNumber: json['bookingNumber'] as String? ?? '',
      customerName: json['customerName'] as String? ?? '',
      routeName: json['routeName'] as String? ?? '',
      pickupTime: DateTime.tryParse(json['pickupTime']?.toString() ?? '') ?? DateTime.now(),
      dropoffTime: json['dropoffTime'] != null
          ? DateTime.tryParse(json['dropoffTime'].toString())
          : null,
      status: json['status'] as int? ?? 0,
      statusName: json['statusName'] as String? ?? '',
      vehicleId: json['vehicleId'] as int?,
      vehicleName: json['vehicleName'] as String?,
      totalAmount: (json['totalAmount'] as num?)?.toDouble() ?? 0.0,
    );
  }
}
