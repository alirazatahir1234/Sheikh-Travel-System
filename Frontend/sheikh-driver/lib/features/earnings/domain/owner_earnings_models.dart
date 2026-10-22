class OwnerEarningsDay {
  const OwnerEarningsDay({
    required this.date,
    required this.grossRevenue,
    required this.driverShare,
    required this.ownerRemaining,
    required this.tripCount,
  });

  final DateTime date;
  final double grossRevenue;
  final double driverShare;
  final double ownerRemaining;
  final int tripCount;

  factory OwnerEarningsDay.fromJson(Map<String, dynamic> json) {
    return OwnerEarningsDay(
      date: DateTime.tryParse(json['date']?.toString() ?? '') ?? DateTime.now(),
      grossRevenue: (json['grossRevenue'] as num?)?.toDouble() ?? 0,
      driverShare: (json['driverShare'] as num?)?.toDouble() ?? 0,
      ownerRemaining: (json['ownerRemaining'] as num?)?.toDouble() ?? 0,
      tripCount: (json['tripCount'] as num?)?.toInt() ?? 0,
    );
  }
}

class OwnerEarningsSummary {
  const OwnerEarningsSummary({
    required this.fromDate,
    required this.toDate,
    required this.grossTripRevenue,
    required this.driverShare,
    required this.ownerRemaining,
    required this.fuelCost,
    required this.completedTripCount,
    required this.settlementCount,
    required this.driverShareSource,
    required this.todayOwnerRemaining,
    required this.thisWeekOwnerRemaining,
    required this.thisMonthOwnerRemaining,
    required this.daily,
    this.dailyDriverShareIsEstimate = false,
  });

  final DateTime fromDate;
  final DateTime toDate;
  final double grossTripRevenue;
  final double driverShare;
  final double ownerRemaining;
  final double fuelCost;
  final int completedTripCount;
  final int settlementCount;
  final String driverShareSource;
  final double todayOwnerRemaining;
  final double thisWeekOwnerRemaining;
  final double thisMonthOwnerRemaining;
  final List<OwnerEarningsDay> daily;

  /// True when daily bars allocate settlement payables by period gross weight.
  final bool dailyDriverShareIsEstimate;

  factory OwnerEarningsSummary.fromJson(Map<String, dynamic> json) {
    final dailyRaw = json['daily'] as List<dynamic>? ?? [];
    return OwnerEarningsSummary(
      fromDate: DateTime.tryParse(json['fromDate']?.toString() ?? '') ??
          DateTime.now(),
      toDate:
          DateTime.tryParse(json['toDate']?.toString() ?? '') ?? DateTime.now(),
      grossTripRevenue: (json['grossTripRevenue'] as num?)?.toDouble() ?? 0,
      driverShare: (json['driverShare'] as num?)?.toDouble() ?? 0,
      ownerRemaining: (json['ownerRemaining'] as num?)?.toDouble() ?? 0,
      fuelCost: (json['fuelCost'] as num?)?.toDouble() ?? 0,
      completedTripCount: (json['completedTripCount'] as num?)?.toInt() ?? 0,
      settlementCount: (json['settlementCount'] as num?)?.toInt() ?? 0,
      driverShareSource: json['driverShareSource']?.toString() ?? 'None',
      todayOwnerRemaining:
          (json['todayOwnerRemaining'] as num?)?.toDouble() ?? 0,
      thisWeekOwnerRemaining:
          (json['thisWeekOwnerRemaining'] as num?)?.toDouble() ?? 0,
      thisMonthOwnerRemaining:
          (json['thisMonthOwnerRemaining'] as num?)?.toDouble() ?? 0,
      daily: dailyRaw
          .whereType<Map>()
          .map((e) => OwnerEarningsDay.fromJson(Map<String, dynamic>.from(e)))
          .toList(),
      dailyDriverShareIsEstimate:
          json['dailyDriverShareIsEstimate'] as bool? ??
              json['DailyDriverShareIsEstimate'] as bool? ??
              false,
    );
  }
}
