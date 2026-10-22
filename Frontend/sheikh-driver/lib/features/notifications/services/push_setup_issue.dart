/// Classified push / FCM setup failure for Settings + optional login SnackBar.
class PushSetupIssue {
  const PushSetupIssue({
    required this.message,
    required this.reason,
    this.showSnackBar = true,
  });

  final String message;
  final String reason;

  /// When false, only Settings shows the hint (no login popup).
  final bool showSnackBar;
}

/// Known failure reasons (analytics + branching).
abstract final class PushSetupReason {
  static const firebaseNotInitialized = 'firebase_not_initialized';
  static const playServicesUnavailable = 'play_services_unavailable';
  static const permissionDenied = 'permission_denied';
  static const noToken = 'no_token';
  static const initFailed = 'init_failed';
  static const enableFailed = 'enable_failed';
  static const registerFailed = 'register_failed';
}

/// Maps Firebase / FCM exceptions to a user-facing [PushSetupIssue].
PushSetupIssue classifyPushFailure(
  Object error, {
  required bool showSnackBar,
}) {
  final text = error.toString().toLowerCase();

  if (_looksLikeMissingFirebaseConfig(text)) {
    return PushSetupIssue(
      message:
          'Push needs Firebase config: add android/app/google-services.json '
          'for package com.sheikhgo.fleet, then rebuild. '
          'See android/app/GOOGLE_SERVICES.md.',
      reason: PushSetupReason.firebaseNotInitialized,
      showSnackBar: showSnackBar,
    );
  }

  if (_looksLikeMissingPlayServices(text)) {
    return PushSetupIssue(
      message:
          'Google Play Services is required for push. Use an Android emulator '
          'with a Google Play system image (not AOSP/Google APIs-only), '
          'or a physical device, then retry in Settings.',
      reason: PushSetupReason.playServicesUnavailable,
      showSnackBar: showSnackBar,
    );
  }

  return PushSetupIssue(
    message: 'Could not set up push notifications ($error). '
        'Try the Settings toggle again.',
    reason: PushSetupReason.initFailed,
    showSnackBar: showSnackBar,
  );
}

bool _looksLikeMissingFirebaseConfig(String text) {
  return text.contains('firebase is not initialized') ||
      text.contains('google-services.json') ||
      text.contains('no firebase app') ||
      text.contains('[core/no-app]') ||
      text.contains('failed to get firebase') ||
      text.contains('default firebaseapp');
}

bool _looksLikeMissingPlayServices(String text) {
  return text.contains('service_not_available') ||
      text.contains('service_missing') ||
      text.contains('service_disabled') ||
      text.contains('service_invalid') ||
      text.contains('missing_instanceid_service') ||
      text.contains('google play services') ||
      text.contains('com.google.android.gms') ||
      text.contains('java.io.ioexception: java.util.concurrent.executionexception');
}
