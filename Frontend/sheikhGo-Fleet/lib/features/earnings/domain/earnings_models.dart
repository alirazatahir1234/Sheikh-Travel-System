class EarningsDay {
  const EarningsDay({
    required this.date,
    required this.amount,
    required this.tripCount,
  });

  final DateTime date;
  final double amount;
  final int tripCount;

  factory EarningsDay.fromJson(Map<String, dynamic> json) {
    return EarningsDay(
      date: DateTime.tryParse(json['date']?.toString() ?? '') ?? DateTime.now(),
      amount: (json['amount'] as num?)?.toDouble() ?? 0,
      tripCount: (json['tripCount'] as num?)?.toInt() ?? 0,
    );
  }
}

class EarningsSummary {
  const EarningsSummary({
    required this.tripAllowances,
    required this.completedTripCount,
    required this.fromDate,
    required this.toDate,
    required this.today,
    required this.thisWeek,
    required this.thisMonth,
    required this.pending,
    required this.paid,
    required this.fuelCost,
    required this.distanceKm,
    required this.hoursWorked,
    required this.daily,
  });

  final double tripAllowances;
  final int completedTripCount;
  final DateTime fromDate;
  final DateTime toDate;
  final double today;
  final double thisWeek;
  final double thisMonth;
  final double pending;
  final double paid;
  final double fuelCost;
  final double distanceKm;
  final double hoursWorked;
  final List<EarningsDay> daily;

  factory EarningsSummary.fromJson(Map<String, dynamic> json) {
    final dailyRaw = json['daily'] as List<dynamic>? ?? [];
    return EarningsSummary(
      tripAllowances: (json['tripAllowances'] as num?)?.toDouble() ?? 0,
      completedTripCount: (json['completedTripCount'] as num?)?.toInt() ?? 0,
      fromDate: DateTime.tryParse(json['fromDate']?.toString() ?? '') ?? DateTime.now(),
      toDate: DateTime.tryParse(json['toDate']?.toString() ?? '') ?? DateTime.now(),
      today: (json['today'] as num?)?.toDouble() ?? 0,
      thisWeek: (json['thisWeek'] as num?)?.toDouble() ?? 0,
      thisMonth: (json['thisMonth'] as num?)?.toDouble() ?? 0,
      pending: (json['pending'] as num?)?.toDouble() ?? 0,
      paid: (json['paid'] as num?)?.toDouble() ?? 0,
      fuelCost: (json['fuelCost'] as num?)?.toDouble() ?? 0,
      distanceKm: (json['distanceKm'] as num?)?.toDouble() ?? 0,
      hoursWorked: (json['hoursWorked'] as num?)?.toDouble() ?? 0,
      daily: dailyRaw
          .whereType<Map<String, dynamic>>()
          .map(EarningsDay.fromJson)
          .toList(),
    );
  }

  static EarningsSummary empty() => EarningsSummary(
        tripAllowances: 0,
        completedTripCount: 0,
        fromDate: DateTime.now(),
        toDate: DateTime.now(),
        today: 0,
        thisWeek: 0,
        thisMonth: 0,
        pending: 0,
        paid: 0,
        fuelCost: 0,
        distanceKm: 0,
        hoursWorked: 0,
        daily: const [],
      );
}
