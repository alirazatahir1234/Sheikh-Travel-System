import 'package:flutter/widgets.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'theme_provider.dart';

const localePrefsKey = 'appLocale';
const biometricLockPrefsKey = 'biometricLockEnabled';

final localeCodeProvider = StateProvider<String>((ref) => 'en');

final appLocaleProvider = Provider<Locale>((ref) {
  final code = ref.watch(localeCodeProvider);
  return Locale(code == 'ar' ? 'ar' : 'en');
});

final biometricLockEnabledProvider = StateProvider<bool>((ref) => false);

/// When true, the user may use the app. Cleared on background when biometric lock is on.
final appUnlockedProvider = StateProvider<bool>((ref) => true);

Future<void> setAppLocale(WidgetRef ref, String code) async {
  final normalized = code == 'ar' ? 'ar' : 'en';
  ref.read(localeCodeProvider.notifier).state = normalized;
  await ref.read(prefsBoxProvider).put(localePrefsKey, normalized);
}

Future<void> setBiometricLockEnabled(WidgetRef ref, bool enabled) async {
  ref.read(biometricLockEnabledProvider.notifier).state = enabled;
  await ref.read(prefsBoxProvider).put(biometricLockPrefsKey, enabled);
  if (enabled) {
    ref.read(appUnlockedProvider.notifier).state = true;
  }
}
