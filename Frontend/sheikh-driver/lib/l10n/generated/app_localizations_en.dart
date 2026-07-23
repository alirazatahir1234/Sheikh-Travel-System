// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get appTitle => 'SheikhGo Fleet';

  @override
  String get settings => 'Settings';

  @override
  String get account => 'Account';

  @override
  String get appearance => 'Appearance';

  @override
  String get security => 'Security';

  @override
  String get activity => 'Activity';

  @override
  String get appSection => 'App';

  @override
  String get darkMode => 'Dark Mode';

  @override
  String get darkModeSubtitle => 'Switch to dark theme';

  @override
  String get language => 'Language';

  @override
  String get languageSubtitle => 'App display language';

  @override
  String get languageEnglish => 'English';

  @override
  String get languageArabic => 'Arabic';

  @override
  String get biometricLock => 'Biometric lock';

  @override
  String get biometricLockSubtitle =>
      'Require Face ID / fingerprint to open the app';

  @override
  String get biometricUnavailable =>
      'Biometrics are not available on this device';

  @override
  String get biometricEnableFailed => 'Could not enable biometric lock';

  @override
  String get unlockTitle => 'Unlock SheikhGo Fleet';

  @override
  String get unlockSubtitle => 'Authenticate to continue';

  @override
  String get unlockAction => 'Unlock';

  @override
  String get unlockSignOut => 'Sign out';

  @override
  String get offlineQueue => 'Offline queue';

  @override
  String get offlineQueueSubtitle => 'Pending sync & conflicts';

  @override
  String get offlineBannerOffline => 'You are offline';

  @override
  String offlineBannerQueued(int count) {
    return '$count queued';
  }

  @override
  String offlineBannerPending(int count) {
    return '$count action(s) waiting to sync';
  }

  @override
  String get sync => 'Sync';

  @override
  String get syncNow => 'Sync now';

  @override
  String syncedItems(int count) {
    return 'Synced $count item(s)';
  }

  @override
  String get online => 'Online';

  @override
  String get offline => 'Offline';

  @override
  String get offlineOnlineHint =>
      'Pending actions sync automatically when connected';

  @override
  String get offlineOfflineHint =>
      'Actions are saved locally until connection returns';

  @override
  String pendingSection(int count) {
    return 'Pending ($count)';
  }

  @override
  String get nothingPending => 'Nothing waiting to sync.';

  @override
  String get conflictsSection => 'Conflicts (needs review)';

  @override
  String get conflictsHint =>
      'Server rejected these after reconnect. Remove after checking ERP state.';

  @override
  String get retry => 'Retry';

  @override
  String get signOut => 'Sign Out';

  @override
  String get signOutConfirmTitle => 'Sign out';

  @override
  String get signOutConfirmBody => 'Are you sure you want to sign out?';

  @override
  String get cancel => 'Cancel';

  @override
  String environmentLabel(String env) {
    return 'Environment: $env';
  }
}
