import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/intl.dart' as intl;

import 'app_localizations_ar.dart';
import 'app_localizations_en.dart';

// ignore_for_file: type=lint

/// Callers can lookup localized strings with an instance of AppLocalizations
/// returned by `AppLocalizations.of(context)`.
///
/// Applications need to include `AppLocalizations.delegate()` in their app's
/// `localizationDelegates` list, and the locales they support in the app's
/// `supportedLocales` list. For example:
///
/// ```dart
/// import 'generated/app_localizations.dart';
///
/// return MaterialApp(
///   localizationsDelegates: AppLocalizations.localizationsDelegates,
///   supportedLocales: AppLocalizations.supportedLocales,
///   home: MyApplicationHome(),
/// );
/// ```
///
/// ## Update pubspec.yaml
///
/// Please make sure to update your pubspec.yaml to include the following
/// packages:
///
/// ```yaml
/// dependencies:
///   # Internationalization support.
///   flutter_localizations:
///     sdk: flutter
///   intl: any # Use the pinned version from flutter_localizations
///
///   # Rest of dependencies
/// ```
///
/// ## iOS Applications
///
/// iOS applications define key application metadata, including supported
/// locales, in an Info.plist file that is built into the application bundle.
/// To configure the locales supported by your app, you’ll need to edit this
/// file.
///
/// First, open your project’s ios/Runner.xcworkspace Xcode workspace file.
/// Then, in the Project Navigator, open the Info.plist file under the Runner
/// project’s Runner folder.
///
/// Next, select the Information Property List item, select Add Item from the
/// Editor menu, then select Localizations from the pop-up menu.
///
/// Select and expand the newly-created Localizations item then, for each
/// locale your application supports, add a new item and select the locale
/// you wish to add from the pop-up menu in the Value field. This list should
/// be consistent with the languages listed in the AppLocalizations.supportedLocales
/// property.
abstract class AppLocalizations {
  AppLocalizations(String locale)
      : localeName = intl.Intl.canonicalizedLocale(locale.toString());

  final String localeName;

  static AppLocalizations of(BuildContext context) {
    return Localizations.of<AppLocalizations>(context, AppLocalizations)!;
  }

  static const LocalizationsDelegate<AppLocalizations> delegate =
      _AppLocalizationsDelegate();

  /// A list of this localizations delegate along with the default localizations
  /// delegates.
  ///
  /// Returns a list of localizations delegates containing this delegate along with
  /// GlobalMaterialLocalizations.delegate, GlobalCupertinoLocalizations.delegate,
  /// and GlobalWidgetsLocalizations.delegate.
  ///
  /// Additional delegates can be added by appending to this list in
  /// MaterialApp. This list does not have to be used at all if a custom list
  /// of delegates is preferred or required.
  static const List<LocalizationsDelegate<dynamic>> localizationsDelegates =
      <LocalizationsDelegate<dynamic>>[
    delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
  ];

  /// A list of this localizations delegate's supported locales.
  static const List<Locale> supportedLocales = <Locale>[
    Locale('ar'),
    Locale('en')
  ];

  /// No description provided for @appTitle.
  ///
  /// In en, this message translates to:
  /// **'SheikhGo Tracking'**
  String get appTitle;

  /// No description provided for @settings.
  ///
  /// In en, this message translates to:
  /// **'Settings'**
  String get settings;

  /// No description provided for @account.
  ///
  /// In en, this message translates to:
  /// **'Account'**
  String get account;

  /// No description provided for @appearance.
  ///
  /// In en, this message translates to:
  /// **'Appearance'**
  String get appearance;

  /// No description provided for @security.
  ///
  /// In en, this message translates to:
  /// **'Security'**
  String get security;

  /// No description provided for @activity.
  ///
  /// In en, this message translates to:
  /// **'Activity'**
  String get activity;

  /// No description provided for @appSection.
  ///
  /// In en, this message translates to:
  /// **'App'**
  String get appSection;

  /// No description provided for @darkMode.
  ///
  /// In en, this message translates to:
  /// **'Dark Mode'**
  String get darkMode;

  /// No description provided for @darkModeSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Switch to dark theme'**
  String get darkModeSubtitle;

  /// No description provided for @language.
  ///
  /// In en, this message translates to:
  /// **'Language'**
  String get language;

  /// No description provided for @languageSubtitle.
  ///
  /// In en, this message translates to:
  /// **'App display language'**
  String get languageSubtitle;

  /// No description provided for @languageEnglish.
  ///
  /// In en, this message translates to:
  /// **'English'**
  String get languageEnglish;

  /// No description provided for @languageArabic.
  ///
  /// In en, this message translates to:
  /// **'Arabic'**
  String get languageArabic;

  /// No description provided for @biometricLock.
  ///
  /// In en, this message translates to:
  /// **'Biometric lock'**
  String get biometricLock;

  /// No description provided for @biometricLockSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Require Face ID / fingerprint to open the app'**
  String get biometricLockSubtitle;

  /// No description provided for @biometricUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Biometrics are not available on this device'**
  String get biometricUnavailable;

  /// No description provided for @biometricEnableFailed.
  ///
  /// In en, this message translates to:
  /// **'Could not enable biometric lock'**
  String get biometricEnableFailed;

  /// No description provided for @unlockTitle.
  ///
  /// In en, this message translates to:
  /// **'Unlock SheikhGo Tracking'**
  String get unlockTitle;

  /// No description provided for @unlockSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Authenticate to continue'**
  String get unlockSubtitle;

  /// No description provided for @unlockAction.
  ///
  /// In en, this message translates to:
  /// **'Unlock'**
  String get unlockAction;

  /// No description provided for @unlockSignOut.
  ///
  /// In en, this message translates to:
  /// **'Sign out'**
  String get unlockSignOut;

  /// No description provided for @offlineQueue.
  ///
  /// In en, this message translates to:
  /// **'Sync & Connectivity'**
  String get offlineQueue;

  /// No description provided for @offlineQueueSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Pending, synced & failed items'**
  String get offlineQueueSubtitle;

  /// No description provided for @syncConnectivity.
  ///
  /// In en, this message translates to:
  /// **'Sync & Connectivity'**
  String get syncConnectivity;

  /// No description provided for @syncConnectivitySubtitle.
  ///
  /// In en, this message translates to:
  /// **'Pending, synced & failed items'**
  String get syncConnectivitySubtitle;

  /// No description provided for @syncPendingSubtitle.
  ///
  /// In en, this message translates to:
  /// **'{count} pending action(s)'**
  String syncPendingSubtitle(int count);

  /// No description provided for @notificationsSection.
  ///
  /// In en, this message translates to:
  /// **'Notifications'**
  String get notificationsSection;

  /// No description provided for @securityPrivacy.
  ///
  /// In en, this message translates to:
  /// **'Security & Privacy'**
  String get securityPrivacy;

  /// No description provided for @securityPermissions.
  ///
  /// In en, this message translates to:
  /// **'Security & Permissions'**
  String get securityPermissions;

  /// No description provided for @securityPermissionsSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Roles and access for your account'**
  String get securityPermissionsSubtitle;

  /// No description provided for @securityPermissionsFootnote.
  ///
  /// In en, this message translates to:
  /// **'Access is granted by your company administrator. The server enforces permissions on every request.'**
  String get securityPermissionsFootnote;

  /// No description provided for @securityLevelHint.
  ///
  /// In en, this message translates to:
  /// **'Access level: {level}'**
  String securityLevelHint(String level);

  /// No description provided for @rolesSection.
  ///
  /// In en, this message translates to:
  /// **'Roles'**
  String get rolesSection;

  /// No description provided for @permissionCategories.
  ///
  /// In en, this message translates to:
  /// **'Permission categories'**
  String get permissionCategories;

  /// No description provided for @securityDiagnostics.
  ///
  /// In en, this message translates to:
  /// **'Security Diagnostics'**
  String get securityDiagnostics;

  /// No description provided for @securityDiagnosticsSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Root, jailbreak, emulator, pinning'**
  String get securityDiagnosticsSubtitle;

  /// No description provided for @locationTracking.
  ///
  /// In en, this message translates to:
  /// **'Location & Tracking'**
  String get locationTracking;

  /// No description provided for @locationTrackingSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Permissions, status & battery guidance'**
  String get locationTrackingSubtitle;

  /// No description provided for @locationTrackingActive.
  ///
  /// In en, this message translates to:
  /// **'Live tracking is active'**
  String get locationTrackingActive;

  /// No description provided for @locationTrackingIdle.
  ///
  /// In en, this message translates to:
  /// **'Tracking is not active on this device'**
  String get locationTrackingIdle;

  /// No description provided for @locationPermissions.
  ///
  /// In en, this message translates to:
  /// **'Permissions'**
  String get locationPermissions;

  /// No description provided for @locationWhenInUse.
  ///
  /// In en, this message translates to:
  /// **'When in use'**
  String get locationWhenInUse;

  /// No description provided for @locationAlways.
  ///
  /// In en, this message translates to:
  /// **'Always (background)'**
  String get locationAlways;

  /// No description provided for @locationServices.
  ///
  /// In en, this message translates to:
  /// **'Device location'**
  String get locationServices;

  /// No description provided for @onLabel.
  ///
  /// In en, this message translates to:
  /// **'On'**
  String get onLabel;

  /// No description provided for @offLabel.
  ///
  /// In en, this message translates to:
  /// **'Off'**
  String get offLabel;

  /// No description provided for @openSystemSettings.
  ///
  /// In en, this message translates to:
  /// **'Open system Settings'**
  String get openSystemSettings;

  /// No description provided for @trackingStatus.
  ///
  /// In en, this message translates to:
  /// **'Tracking status'**
  String get trackingStatus;

  /// No description provided for @status.
  ///
  /// In en, this message translates to:
  /// **'Status'**
  String get status;

  /// No description provided for @active.
  ///
  /// In en, this message translates to:
  /// **'Active'**
  String get active;

  /// No description provided for @inactive.
  ///
  /// In en, this message translates to:
  /// **'Inactive'**
  String get inactive;

  /// No description provided for @lastUpdate.
  ///
  /// In en, this message translates to:
  /// **'Last update'**
  String get lastUpdate;

  /// No description provided for @notAvailable.
  ///
  /// In en, this message translates to:
  /// **'Not available'**
  String get notAvailable;

  /// No description provided for @vehicleIdLabel.
  ///
  /// In en, this message translates to:
  /// **'Vehicle'**
  String get vehicleIdLabel;

  /// No description provided for @queuedPoints.
  ///
  /// In en, this message translates to:
  /// **'Queued GPS points'**
  String get queuedPoints;

  /// No description provided for @batteryGuidance.
  ///
  /// In en, this message translates to:
  /// **'Battery guidance'**
  String get batteryGuidance;

  /// No description provided for @batteryGuidanceBody.
  ///
  /// In en, this message translates to:
  /// **'Keep location set to Always while on duty. Avoid aggressive battery savers for SheikhGo. Use Low Power modes only when dispatch confirms tracking may pause.'**
  String get batteryGuidanceBody;

  /// No description provided for @openLiveMap.
  ///
  /// In en, this message translates to:
  /// **'Open live map'**
  String get openLiveMap;

  /// No description provided for @trackingMandatoryHint.
  ///
  /// In en, this message translates to:
  /// **'Live tracking is required for active trips. Contact dispatch if you need to pause company tracking.'**
  String get trackingMandatoryHint;

  /// No description provided for @stopTracking.
  ///
  /// In en, this message translates to:
  /// **'Stop tracking'**
  String get stopTracking;

  /// No description provided for @profileAccount.
  ///
  /// In en, this message translates to:
  /// **'PROFILE & ACCOUNT'**
  String get profileAccount;

  /// No description provided for @appInformation.
  ///
  /// In en, this message translates to:
  /// **'App Information'**
  String get appInformation;

  /// No description provided for @offlineBannerOffline.
  ///
  /// In en, this message translates to:
  /// **'You are offline'**
  String get offlineBannerOffline;

  /// No description provided for @offlineBannerQueued.
  ///
  /// In en, this message translates to:
  /// **'{count} queued'**
  String offlineBannerQueued(int count);

  /// No description provided for @offlineBannerPending.
  ///
  /// In en, this message translates to:
  /// **'{count} action(s) waiting to sync'**
  String offlineBannerPending(int count);

  /// No description provided for @sync.
  ///
  /// In en, this message translates to:
  /// **'Sync'**
  String get sync;

  /// No description provided for @syncNow.
  ///
  /// In en, this message translates to:
  /// **'Sync now'**
  String get syncNow;

  /// No description provided for @syncedItems.
  ///
  /// In en, this message translates to:
  /// **'Synced {count} item(s)'**
  String syncedItems(int count);

  /// No description provided for @online.
  ///
  /// In en, this message translates to:
  /// **'Online'**
  String get online;

  /// No description provided for @offline.
  ///
  /// In en, this message translates to:
  /// **'Offline'**
  String get offline;

  /// No description provided for @offlineOnlineHint.
  ///
  /// In en, this message translates to:
  /// **'Pending actions sync automatically when connected'**
  String get offlineOnlineHint;

  /// No description provided for @offlineOfflineHint.
  ///
  /// In en, this message translates to:
  /// **'Actions are saved locally until connection returns'**
  String get offlineOfflineHint;

  /// No description provided for @pendingSection.
  ///
  /// In en, this message translates to:
  /// **'Pending ({count})'**
  String pendingSection(int count);

  /// No description provided for @nothingPending.
  ///
  /// In en, this message translates to:
  /// **'Nothing waiting to sync.'**
  String get nothingPending;

  /// No description provided for @conflictsSection.
  ///
  /// In en, this message translates to:
  /// **'Conflicts (needs review)'**
  String get conflictsSection;

  /// No description provided for @conflictsHint.
  ///
  /// In en, this message translates to:
  /// **'Server rejected these after reconnect. Remove after checking ERP state.'**
  String get conflictsHint;

  /// No description provided for @retry.
  ///
  /// In en, this message translates to:
  /// **'Retry'**
  String get retry;

  /// No description provided for @retryAllFailed.
  ///
  /// In en, this message translates to:
  /// **'Retry all failed'**
  String get retryAllFailed;

  /// No description provided for @retryStarted.
  ///
  /// In en, this message translates to:
  /// **'Retry started — check remaining items'**
  String get retryStarted;

  /// No description provided for @signOut.
  ///
  /// In en, this message translates to:
  /// **'Sign Out'**
  String get signOut;

  /// No description provided for @signOutConfirmTitle.
  ///
  /// In en, this message translates to:
  /// **'Sign out'**
  String get signOutConfirmTitle;

  /// No description provided for @signOutConfirmBody.
  ///
  /// In en, this message translates to:
  /// **'Are you sure you want to sign out?'**
  String get signOutConfirmBody;

  /// No description provided for @cancel.
  ///
  /// In en, this message translates to:
  /// **'Cancel'**
  String get cancel;

  /// No description provided for @environmentLabel.
  ///
  /// In en, this message translates to:
  /// **'Environment: {env}'**
  String environmentLabel(String env);

  /// No description provided for @changePassword.
  ///
  /// In en, this message translates to:
  /// **'Change Password'**
  String get changePassword;

  /// No description provided for @pushNotifications.
  ///
  /// In en, this message translates to:
  /// **'Push notifications'**
  String get pushNotifications;

  /// No description provided for @pushNotificationsOn.
  ///
  /// In en, this message translates to:
  /// **'Trip alerts and dispatch messages'**
  String get pushNotificationsOn;

  /// No description provided for @pushNotificationsOff.
  ///
  /// In en, this message translates to:
  /// **'Off — you will not receive push alerts'**
  String get pushNotificationsOff;

  /// No description provided for @dismissWarning.
  ///
  /// In en, this message translates to:
  /// **'Dismiss warning'**
  String get dismissWarning;

  /// No description provided for @securityStatus.
  ///
  /// In en, this message translates to:
  /// **'Security Diagnostics'**
  String get securityStatus;

  /// No description provided for @securityStatusSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Root, jailbreak, emulator, pinning'**
  String get securityStatusSubtitle;

  /// No description provided for @backgroundGps.
  ///
  /// In en, this message translates to:
  /// **'Location & Tracking'**
  String get backgroundGps;

  /// No description provided for @backgroundGpsSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Permissions, status & battery guidance'**
  String get backgroundGpsSubtitle;

  /// No description provided for @backgroundGpsStopped.
  ///
  /// In en, this message translates to:
  /// **'Background GPS stopped'**
  String get backgroundGpsStopped;

  /// No description provided for @activityTimeline.
  ///
  /// In en, this message translates to:
  /// **'Activity Timeline'**
  String get activityTimeline;

  /// No description provided for @activityTimelineSubtitle.
  ///
  /// In en, this message translates to:
  /// **'View your trip and attendance history'**
  String get activityTimelineSubtitle;

  /// No description provided for @attendance.
  ///
  /// In en, this message translates to:
  /// **'Attendance'**
  String get attendance;

  /// No description provided for @attendanceSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Clock in / clock out'**
  String get attendanceSubtitle;

  /// No description provided for @privacyPolicy.
  ///
  /// In en, this message translates to:
  /// **'Privacy Policy'**
  String get privacyPolicy;

  /// No description provided for @termsOfService.
  ///
  /// In en, this message translates to:
  /// **'Terms of Service'**
  String get termsOfService;

  /// No description provided for @version.
  ///
  /// In en, this message translates to:
  /// **'App Version'**
  String get version;

  /// No description provided for @checkForUpdates.
  ///
  /// In en, this message translates to:
  /// **'Check for Updates'**
  String get checkForUpdates;

  /// No description provided for @openInBrowser.
  ///
  /// In en, this message translates to:
  /// **'Open in browser'**
  String get openInBrowser;

  /// No description provided for @viewHostedVersion.
  ///
  /// In en, this message translates to:
  /// **'View hosted version'**
  String get viewHostedVersion;

  /// No description provided for @legalEffectiveDate.
  ///
  /// In en, this message translates to:
  /// **'Effective {date}'**
  String legalEffectiveDate(String date);

  /// No description provided for @termsShort.
  ///
  /// In en, this message translates to:
  /// **'Terms'**
  String get termsShort;

  /// No description provided for @privacyShort.
  ///
  /// In en, this message translates to:
  /// **'Privacy'**
  String get privacyShort;

  /// No description provided for @clockedInSuccess.
  ///
  /// In en, this message translates to:
  /// **'Clocked in successfully'**
  String get clockedInSuccess;

  /// No description provided for @clockedOutSuccess.
  ///
  /// In en, this message translates to:
  /// **'Clocked out successfully'**
  String get clockedOutSuccess;

  /// No description provided for @noAttendanceRecords.
  ///
  /// In en, this message translates to:
  /// **'No attendance records'**
  String get noAttendanceRecords;

  /// No description provided for @checkIn.
  ///
  /// In en, this message translates to:
  /// **'Check In'**
  String get checkIn;

  /// No description provided for @checkOut.
  ///
  /// In en, this message translates to:
  /// **'Check Out'**
  String get checkOut;

  /// No description provided for @checkInCaps.
  ///
  /// In en, this message translates to:
  /// **'CHECK IN'**
  String get checkInCaps;

  /// No description provided for @checkOutCaps.
  ///
  /// In en, this message translates to:
  /// **'CHECK OUT'**
  String get checkOutCaps;

  /// No description provided for @checkedIn.
  ///
  /// In en, this message translates to:
  /// **'Checked In'**
  String get checkedIn;

  /// No description provided for @notCheckedIn.
  ///
  /// In en, this message translates to:
  /// **'Not checked in'**
  String get notCheckedIn;

  /// No description provided for @stopTrackingTitle.
  ///
  /// In en, this message translates to:
  /// **'Stop background GPS?'**
  String get stopTrackingTitle;

  /// No description provided for @stopTrackingBody.
  ///
  /// In en, this message translates to:
  /// **'Live location sharing with dispatch will stop until you start tracking again.'**
  String get stopTrackingBody;

  /// No description provided for @stop.
  ///
  /// In en, this message translates to:
  /// **'Stop'**
  String get stop;
}

class _AppLocalizationsDelegate
    extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  Future<AppLocalizations> load(Locale locale) {
    return SynchronousFuture<AppLocalizations>(lookupAppLocalizations(locale));
  }

  @override
  bool isSupported(Locale locale) =>
      <String>['ar', 'en'].contains(locale.languageCode);

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

AppLocalizations lookupAppLocalizations(Locale locale) {
  // Lookup logic when only language code is specified.
  switch (locale.languageCode) {
    case 'ar':
      return AppLocalizationsAr();
    case 'en':
      return AppLocalizationsEn();
  }

  throw FlutterError(
      'AppLocalizations.delegate failed to load unsupported locale "$locale". This is likely '
      'an issue with the localizations generation tool. Please file an issue '
      'on GitHub with a reproducible sample app and the gen-l10n configuration '
      'that was used.');
}
