import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/earnings/domain/earnings_models.dart';

void main() {
  group('EarningsSummary.fromJson', () {
    test('parses period cards and daily series', () {
      final s = EarningsSummary.fromJson({
        'tripAllowances': 1200,
        'completedTripCount': 4,
        'fromDate': '2026-07-01T00:00:00Z',
        'toDate': '2026-07-07T00:00:00Z',
        'today': 100,
        'thisWeek': 500,
        'thisMonth': 2000,
        'pending': 150,
        'paid': 350,
        'fuelCost': 80,
        'distanceKm': 120.5,
        'hoursWorked': 9.5,
        'daily': [
          {'date': '2026-07-01T00:00:00Z', 'amount': 50, 'tripCount': 1},
          {'date': '2026-07-02T00:00:00Z', 'amount': 75, 'tripCount': 2},
        ],
      });
      expect(s.today, 100);
      expect(s.thisWeek, 500);
      expect(s.pending, 150);
      expect(s.daily.length, 2);
      expect(s.daily.first.tripCount, 1);
      expect(s.distanceKm, 120.5);
    });

    test('empty() has zeros', () {
      final e = EarningsSummary.empty();
      expect(e.today, 0);
      expect(e.daily, isEmpty);
    });
  });
}
