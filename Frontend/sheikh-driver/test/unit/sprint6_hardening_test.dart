import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/core/config/app_config.dart';
import 'package:sheikh_go_driver/features/settings/services/app_version_service.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
import 'package:flutter/material.dart';

void main() {
  test('AppConfig exposes environment label', () {
    expect(AppConfig.environmentLabel, isNotEmpty);
    expect(AppConfig.expectedPackageId, 'com.sheikhgo.fleet');
  });

  test('AppVersionService.isOutdated compares semver', () {
    expect(AppVersionService.isOutdated('1.0.0', '1.0.1'), isTrue);
    expect(AppVersionService.isOutdated('1.1.0', '1.0.9'), isFalse);
  });

  testWidgets('EN and AR localizations load', (tester) async {
    late AppLocalizations en;
    late AppLocalizations ar;
    await tester.pumpWidget(
      MaterialApp(
        locale: const Locale('en'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Builder(builder: (context) {
          en = AppLocalizations.of(context);
          return const SizedBox();
        }),
      ),
    );
    await tester.pumpWidget(
      MaterialApp(
        locale: const Locale('ar'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Builder(builder: (context) {
          ar = AppLocalizations.of(context);
          return const SizedBox();
        }),
      ),
    );
    expect(en.settings, 'Settings');
    expect(ar.settings, 'الإعدادات');
    expect(en.biometricLock, isNotEmpty);
    expect(ar.offlineQueue, isNotEmpty);
  });
}
