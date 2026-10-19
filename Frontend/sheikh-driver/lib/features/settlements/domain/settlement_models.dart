class DriverSettlementItem {
  const DriverSettlementItem({
    required this.id,
    required this.periodStart,
    required this.periodEnd,
    required this.payable,
    required this.status,
    this.paidAt,
    this.reference,
  });

  final int id;
  final DateTime periodStart;
  final DateTime periodEnd;
  final double payable;
  final String status;
  final DateTime? paidAt;
  final String? reference;

  factory DriverSettlementItem.fromJson(Map<String, dynamic> json) {
    DateTime? parseDt(dynamic v) {
      if (v == null) return null;
      return DateTime.tryParse(v.toString());
    }

    return DriverSettlementItem(
      id: (json['id'] as num?)?.toInt() ?? 0,
      periodStart: parseDt(json['periodStart']) ?? DateTime.fromMillisecondsSinceEpoch(0),
      periodEnd: parseDt(json['periodEnd']) ?? DateTime.fromMillisecondsSinceEpoch(0),
      payable: (json['payable'] as num?)?.toDouble() ?? 0,
      status: json['status']?.toString() ?? '',
      paidAt: parseDt(json['paidAt']),
      reference: json['reference']?.toString(),
    );
  }
}
