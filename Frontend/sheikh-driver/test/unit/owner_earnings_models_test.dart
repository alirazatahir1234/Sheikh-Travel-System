import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/earnings/domain/owner_earnings_models.dart';

void main() {
  group('OwnerEarningsSummary.fromJson', () {
    test('parses split fields and daily series', () {
      final s = OwnerEarningsSummary.fromJson({
        'fromDate': '2026-10-01T00:00:00Z',
        'toDate': '2026-10-22T00:00:00Z',
        'grossTripRevenue': 10000,
        'driverShare': 3000,
        'ownerRemaining': 7000,
        'fuelCost': 500,
        'completedTripCount': 12,
        'settlementCount': 2,
        'driverShareSource': 'DriverSettlements.Payable',
        'todayOwnerRemaining': 200,
        'thisWeekOwnerRemaining': 1500,
        'thisMonthOwnerRemaining': 7000,
        'daily': [
          {
            'date': '2026-10-21T00:00:00Z',
            'grossRevenue': 1000,
            'driverShare': 300,
            'ownerRemaining': 700,
            'tripCount': 2,
          },
        ],
        'dailyDriverShareIsEstimate': true,
      });
      expect(s.ownerRemaining, 7000);
      expect(s.driverShare, 3000);
      expect(s.daily.single.ownerRemaining, 700);
      expect(s.driverShareSource, 'DriverSettlements.Payable');
      expect(s.dailyDriverShareIsEstimate, isTrue);
    });
  });
}
