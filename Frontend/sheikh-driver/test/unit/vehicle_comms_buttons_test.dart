import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fleet/presentation/widgets/vehicle_comms_buttons.dart';

void main() {
  group('whatsAppDigits', () {
    test('converts PK local mobile to country code', () {
      expect(whatsAppDigits('03202223333'), '923202223333');
      expect(whatsAppDigits('0320-222-3333'), '923202223333');
    });

    test('keeps explicit international numbers', () {
      expect(whatsAppDigits('+92 320 2223333'), '923202223333');
      expect(whatsAppDigits('00923202223333'), '923202223333');
      expect(whatsAppDigits('923202223333'), '923202223333');
    });

    test('returns null for empty or too-short values', () {
      expect(whatsAppDigits(null), isNull);
      expect(whatsAppDigits(''), isNull);
      expect(whatsAppDigits('123'), isNull);
    });
  });

  group('telUri', () {
    test('builds tel: URI from driver phone', () {
      expect(telUri('03202223333')?.toString(), 'tel:03202223333');
      expect(telUri('+92 320 2223333')?.toString(), 'tel:+923202223333');
    });

    test('returns null when missing', () {
      expect(telUri(null), isNull);
      expect(telUri('   '), isNull);
    });
  });
}
