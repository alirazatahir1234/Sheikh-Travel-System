import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/attendance/domain/attendance_model.dart';

void main() {
  group('AttendanceRecord', () {
    test('parses camelCase and PascalCase JSON', () {
      final a = AttendanceRecord.fromJson({
        'id': 7,
        'attendanceType': 'CheckIn',
        'recordedAt': '2026-07-01T10:00:00Z',
        'latitude': 24.7,
        'longitude': 46.7,
      });
      expect(a.id, 7);
      expect(a.isCheckIn, isTrue);
      expect(a.isCheckOut, isFalse);

      final b = AttendanceRecord.fromJson({
        'Id': 8,
        'AttendanceType': 'Check-Out',
        'RecordedAt': '2026-07-01T18:00:00Z',
      });
      expect(b.id, 8);
      expect(b.isCheckOut, isTrue);
      expect(b.isCheckIn, isFalse);
    });

    test('does not throw on odd map shapes', () {
      final r = AttendanceRecord.fromJson({
        'id': 1.0,
        'attendanceType': 'check_in',
        'recordedAt': 'not-a-date',
      });
      expect(r.id, 1);
      expect(r.isCheckIn, isTrue);
    });
  });
}
