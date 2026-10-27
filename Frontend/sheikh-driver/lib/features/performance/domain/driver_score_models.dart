class DriverGpsScore {
  const DriverGpsScore({
    required this.driverId,
    required this.score,
    required this.rating,
    required this.rank,
    required this.totalDrivers,
    required this.safetyScore,
    required this.fuelEfficiencyScore,
    required this.onTimeScore,
    required this.needsTraining,
    required this.rewardEligible,
    required this.tripCount,
    required this.isPartial,
  });

  final int driverId;
  final int score;
  final String rating;
  final int rank;
  final int totalDrivers;
  final int safetyScore;
  final int fuelEfficiencyScore;
  final int onTimeScore;
  final bool needsTraining;
  final bool rewardEligible;
  final int tripCount;
  final bool isPartial;

  factory DriverGpsScore.fromJson(Map<String, dynamic> json) {
    final factors = json['factors'] as Map<String, dynamic>? ?? {};
    return DriverGpsScore(
      driverId: (json['driverId'] as num?)?.toInt() ?? 0,
      score: (json['score'] as num?)?.toInt() ?? 0,
      rating: json['rating']?.toString() ?? '—',
      rank: (json['rank'] as num?)?.toInt() ?? 0,
      totalDrivers: (json['totalDrivers'] as num?)?.toInt() ?? 0,
      safetyScore: (json['safetyScore'] as num?)?.toInt() ?? 0,
      fuelEfficiencyScore: (json['fuelEfficiencyScore'] as num?)?.toInt() ?? 0,
      onTimeScore: (json['onTimeScore'] as num?)?.toInt() ?? 0,
      needsTraining: json['needsTraining'] == true,
      rewardEligible: json['rewardEligible'] == true,
      tripCount: (factors['tripCount'] as num?)?.toInt() ?? 0,
      isPartial: json['isPartial'] == true,
    );
  }
}
