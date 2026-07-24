class PaymentSummary {
  const PaymentSummary({
    required this.tripId,
    required this.bookingId,
    required this.bookingNumber,
    required this.totalAmount,
    required this.paidAmount,
    required this.balanceDue,
    required this.paymentRequired,
    required this.paymentStatus,
  });

  final int tripId;
  final int bookingId;
  final String bookingNumber;
  final double totalAmount;
  final double paidAmount;
  final double balanceDue;
  final bool paymentRequired;
  final String paymentStatus;

  factory PaymentSummary.fromJson(Map<String, dynamic> json) => PaymentSummary(
        tripId: (json['tripId'] as num?)?.toInt() ?? 0,
        bookingId: (json['bookingId'] as num?)?.toInt() ?? 0,
        bookingNumber: json['bookingNumber'] as String? ?? '',
        totalAmount: (json['totalAmount'] as num?)?.toDouble() ?? 0,
        paidAmount: (json['paidAmount'] as num?)?.toDouble() ?? 0,
        balanceDue: (json['balanceDue'] as num?)?.toDouble() ?? 0,
        paymentRequired: json['paymentRequired'] as bool? ?? false,
        paymentStatus: json['paymentStatus'] as String? ?? 'Pending',
      );
}
