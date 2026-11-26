import 'package:flutter_test/flutter_test.dart';
import 'package:intl/intl.dart';
import 'package:sheikh_go_driver/core/time/tenant_time.dart';
import 'package:sheikh_go_driver/features/fleet/presentation/playback/playback_helpers.dart';

/// BWY 217, 24 Nov 2026: a fix at 21:15 UTC must read 2:15 AM in Pakistan and 1:15 AM in Dubai,
/// whatever timezone the phone itself is set to.
void main() {
  final tripUtc = DateTime.utc(2026, 11, 23, 21, 15);
  final hm = DateFormat('HH:mm');

  setUp(TenantTime.resetForTest);

  test('defaults to Asia/Karachi', () {
    expect(kDefaultTimeZone, 'Asia/Karachi');
    expect(TenantTime.companyTimeZone, 'Asia/Karachi');
    expect(TenantTime.format(tripUtc, hm), '02:15');
  });

  test('uses a UAE company zone when configured', () {
    TenantTime.setCompanyTimeZone('Asia/Dubai');
    expect(TenantTime.format(tripUtc, hm), '01:15');
  });

  test('ignores invalid zones', () {
    TenantTime.setCompanyTimeZone('Mars/Olympus');
    expect(TenantTime.companyTimeZone, 'Asia/Karachi');
    expect(TenantTime.resolve('Mars/Olympus'), 'Asia/Karachi');
  });

  test('explicit zone beats on-screen vehicle zone, which beats company zone', () {
    TenantTime.setCompanyTimeZone('Asia/Karachi');
    TenantTime.setScreenTimeZone('Asia/Dubai');
    expect(TenantTime.resolve(null), 'Asia/Dubai');
    expect(TenantTime.resolve('Asia/Riyadh'), 'Asia/Riyadh');
    TenantTime.setScreenTimeZone(null);
    expect(TenantTime.resolve(null), 'Asia/Karachi');
  });

  test('picker wall time round-trips to the same instant', () {
    final picked = TenantTime.fromWallTime(2026, 11, 24, hour: 2, minute: 15);
    expect(picked.isUtc, isTrue);
    expect(picked, tripUtc);
    expect(
      TenantTime.fromWallTime(2026, 11, 24, hour: 1, minute: 15, zoneId: 'Asia/Dubai'),
      tripUtc,
    );
  });

  test('day bounds follow the company midnight', () {
    final (start, end) = TenantTime.dayBoundsUtc(tripUtc);
    expect(start, DateTime.utc(2026, 11, 23, 19));
    expect(end, DateTime.utc(2026, 11, 24, 19));

    final (dubaiStart, _) = TenantTime.dayBoundsUtc(tripUtc, 'Asia/Dubai');
    expect(dubaiStart, DateTime.utc(2026, 11, 23, 20));
  });

  test('playback helpers format in the company / vehicle zone', () {
    expect(formatPlaybackClock(tripUtc), '2:15 AM');
    expect(formatPlaybackClock(tripUtc, 'Asia/Dubai'), '1:15 AM');
    expect(formatPlaybackDateTime(tripUtc), '24 Nov, 2:15 AM');

    TenantTime.setScreenTimeZone('Asia/Dubai');
    expect(formatPlaybackClock(tripUtc), '1:15 AM');
  });

  test('stop window uses the zone for day and clock', () {
    final label = formatStopWindow(
      startTime: tripUtc,
      endTime: tripUtc.add(const Duration(minutes: 15)),
    );
    expect(label, 'Nov 24 · 2:15 AM – 2:30 AM');
  });
}
