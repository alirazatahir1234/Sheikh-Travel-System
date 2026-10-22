// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get appTitle => 'SheikhGo Tracking';

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
  String get unlockTitle => 'Unlock SheikhGo Tracking';

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
  String get retryAllFailed => 'Retry all failed';

  @override
  String get retryStarted => 'Retry started — check remaining items';

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

  @override
  String get changePassword => 'Change Password';

  @override
  String get pushNotifications => 'Push notifications';

  @override
  String get pushNotificationsOn => 'Trip alerts and dispatch messages';

  @override
  String get pushNotificationsOff => 'Off — you will not receive push alerts';

  @override
  String get dismissWarning => 'Dismiss warning';

  @override
  String get securityStatus => 'Security status';

  @override
  String get securityStatusSubtitle => 'Root, jailbreak, emulator, pinning';

  @override
  String get backgroundGps => 'Background GPS';

  @override
  String get backgroundGpsSubtitle => 'Stop live tracking / open live map';

  @override
  String get backgroundGpsStopped => 'Background GPS stopped';

  @override
  String get activityTimeline => 'Activity Timeline';

  @override
  String get activityTimelineSubtitle =>
      'View your trip and attendance history';

  @override
  String get attendance => 'Attendance';

  @override
  String get attendanceSubtitle => 'Clock in / clock out';

  @override
  String get privacyPolicy => 'Privacy Policy';

  @override
  String get termsOfService => 'Terms of Service';

  @override
  String get version => 'Version';

  @override
  String get checkForUpdates => 'Check for Updates';

  @override
  String get openInBrowser => 'Open in browser';

  @override
  String get viewHostedVersion => 'View hosted version';

  @override
  String legalEffectiveDate(String date) {
    return 'Effective $date';
  }

  @override
  String get termsShort => 'Terms';

  @override
  String get privacyShort => 'Privacy';

  @override
  String get clockedInSuccess => 'Clocked in successfully';

  @override
  String get clockedOutSuccess => 'Clocked out successfully';

  @override
  String get noAttendanceRecords => 'No attendance records';

  @override
  String get checkIn => 'Check In';

  @override
  String get checkOut => 'Check Out';

  @override
  String get checkInCaps => 'CHECK IN';

  @override
  String get checkOutCaps => 'CHECK OUT';

  @override
  String get checkedIn => 'Checked In';

  @override
  String get notCheckedIn => 'Not checked in';

  @override
  String get stopTrackingTitle => 'Stop background GPS?';

  @override
  String get stopTrackingBody =>
      'Live location sharing with dispatch will stop until you start tracking again.';

  @override
  String get stop => 'Stop';
}
