import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/settings/services/app_version_service.dart';

void main() {
  group('AppVersionService.isOutdated', () {
    test('same version is not outdated', () {
      expect(AppVersionService.isOutdated('1.0.0', '1.0.0'), isFalse);
    });

    test('current newer than minimum is not outdated', () {
      expect(AppVersionService.isOutdated('1.2.0', '1.0.0'), isFalse);
      expect(AppVersionService.isOutdated('2.0.0', '1.9.9'), isFalse);
      expect(AppVersionService.isOutdated('1.0.1', '1.0.0'), isFalse);
    });

    test('current older than minimum is outdated', () {
      expect(AppVersionService.isOutdated('1.0.0', '1.0.1'), isTrue);
      expect(AppVersionService.isOutdated('1.0.0', '1.1.0'), isTrue);
      expect(AppVersionService.isOutdated('0.9.9', '1.0.0'), isTrue);
    });

    test('patch version comparison works correctly', () {
      expect(AppVersionService.isOutdated('1.0.9', '1.0.10'), isTrue);
      expect(AppVersionService.isOutdated('1.0.10', '1.0.9'), isFalse);
    });

    test('major version bump detected', () {
      expect(AppVersionService.isOutdated('1.99.99', '2.0.0'), isTrue);
    });

    test('currentVersion constant is defined and parseable', () {
      expect(AppVersionService.currentVersion, isNotEmpty);
      expect(
        AppVersionService.isOutdated(AppVersionService.currentVersion, '0.0.1'),
        isFalse,
      );
    });
  });
}
