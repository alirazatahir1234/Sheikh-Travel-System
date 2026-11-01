import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hive/hive.dart';

import '../../../core/analytics/analytics_service.dart';
import '../../../core/api/api_endpoints.dart';
import 'fcm_service.dart';
import 'push_setup_issue.dart';

/// Hive key for user preference: push notifications enabled.
const pushNotificationsPrefsKey = 'pushNotificationsEnabled';

/// Non-blocking hint after FCM/token registration fails post-login.
/// Settings always shows [PushSetupIssue.message]; SnackBar only when
/// [PushSetupIssue.showSnackBar] is true.
final pushRegistrationIssueProvider =
    StateProvider<PushSetupIssue?>((ref) => null);

/// Mirrors the Settings push toggle (persisted in Hive).
final pushNotificationsEnabledProvider = StateProvider<bool>((ref) => true);

/// Registers FCM device tokens with the SheikhGo AI push pipeline.
class PushRegistrationService {
  PushRegistrationService._();
  static final instance = PushRegistrationService._();

  Dio? _dio;
  String _appName = 'fleet';
  ProviderContainer? _container;
  bool _hintQueued = false;

  /// Optional Riverpod container so failures can surface in UI without failing login.
  void bindContainer(ProviderContainer container) {
    _container = container;
  }

  bool get isPreferenceEnabled {
    try {
      final box = Hive.box('prefs');
      return box.get(pushNotificationsPrefsKey, defaultValue: true) as bool? ??
          true;
    } catch (_) {
      return true;
    }
  }

  Future<void> _persistPreference(bool enabled) async {
    try {
      await Hive.box('prefs').put(pushNotificationsPrefsKey, enabled);
    } catch (e) {
      debugPrint('[Push] Could not persist preference: $e');
    }
    final c = _container;
    if (c != null) {
      try {
        c.read(pushNotificationsEnabledProvider.notifier).state = enabled;
      } catch (_) {}
    }
  }

  /// Called after login. Honours the Settings preference.
  ///
  /// Missing Firebase config / Play Services gaps are recorded for Settings
  /// without a scary login SnackBar (see [PushSetupIssue.showSnackBar]).
  Future<void> start(Dio dio, {String appName = 'fleet'}) async {
    _dio = dio;
    _appName = appName;
    _hintQueued = false;

    if (!isPreferenceEnabled) {
      debugPrint('[Push] Skipped — user disabled push in Settings');
      return;
    }

    final notReady = FcmService.instance.firebaseNotReadyIssue(
      // Dev/misconfig is expected until google-services.json is present —
      // do not popup on every login; Settings shows the actionable hint.
      showSnackBar: false,
    );
    if (notReady != null) {
      debugPrint('[Push] FCM skipped — Firebase not initialized');
      _reportIssue(notReady);
      return;
    }

    try {
      await FcmService.instance.initialize(onTokenRefresh: _registerToken);
      if (FcmService.instance.token == null) {
        final allowed = await FcmService.instance.hasPermission();
        _reportIssue(
          PushSetupIssue(
            message: allowed
                ? 'Permission granted, but no FCM token yet. '
                    'Confirm google-services.json matches package '
                    'com.sheikhgo.fleet, use a Google Play emulator image, '
                    'then retry in Settings.'
                : 'Push notifications are not enabled on this device. '
                    'Turn on the toggle in Settings and allow notifications '
                    'when prompted.',
            reason: allowed
                ? PushSetupReason.noToken
                : PushSetupReason.permissionDenied,
            // Permission prompt is user-actionable; soft-hint only for token gaps.
            showSnackBar: !allowed,
          ),
        );
      } else {
        _container?.read(pushRegistrationIssueProvider.notifier).state = null;
      }
    } catch (e) {
      debugPrint('[Push] FCM init skipped: $e');
      // Login path: classify accurately; avoid false "check Firebase config"
      // when the real issue is missing Play Services on an AOSP emulator.
      _reportIssue(classifyPushFailure(e, showSnackBar: false));
    }
  }

  /// Settings toggle → ON: request OS permission, get FCM token, register with API.
  /// Returns true when the toggle should stay on (permission granted and/or token ok).
  Future<bool> enableFromSettings({
    Dio? dio,
    String? appName,
  }) async {
    if (dio != null) _dio = dio;
    if (appName != null) _appName = appName;
    _hintQueued = false;

    await _persistPreference(true);

    final notReady = FcmService.instance.firebaseNotReadyIssue(
      showSnackBar: true,
    );
    if (notReady != null) {
      await _persistPreference(false);
      _reportIssue(notReady);
      return false;
    }

    try {
      final allowed = await FcmService.instance.requestPermission();
      if (!allowed) {
        await _persistPreference(false);
        _reportIssue(
          const PushSetupIssue(
            message: 'Notification permission was denied. Enable it in system '
                'Settings, then turn this toggle on again.',
            reason: PushSetupReason.permissionDenied,
          ),
        );
        return false;
      }

      final token = await FcmService.instance.enable(
        onTokenRefresh: _registerToken,
      );
      if (token == null) {
        await _persistPreference(false);
        _reportIssue(
          const PushSetupIssue(
            message: 'Permission granted, but no FCM token. Confirm '
                'google-services.json matches package com.sheikhgo.fleet, '
                'use a Google Play system-image emulator (or a device), '
                'then rebuild and retry. See android/app/GOOGLE_SERVICES.md.',
            reason: PushSetupReason.noToken,
          ),
        );
        return false;
      }

      _container?.read(pushRegistrationIssueProvider.notifier).state = null;
      return true;
    } catch (e) {
      debugPrint('[Push] enableFromSettings failed: $e');
      _reportIssue(classifyPushFailure(e, showSnackBar: true));
      await _persistPreference(false);
      return false;
    }
  }

  /// Settings toggle → OFF: delete FCM token and stop auto-registration.
  Future<void> disableFromSettings() async {
    await _persistPreference(false);
    try {
      await FcmService.instance.disable();
    } catch (e) {
      debugPrint('[Push] disableFromSettings failed: $e');
    }
    clearHint();
  }

  Future<void> _registerToken(String token) async {
    final dio = _dio;
    if (dio == null) return;
    if (!isPreferenceEnabled) return;
    try {
      await dio.post(ApiEndpoints.deviceToken, data: {
        'token': token,
        'platform': Platform.isIOS ? 'ios' : 'android',
        'appName': _appName,
      });
      await dio.post(ApiEndpoints.mobileHeartbeat);
      debugPrint('[Push] Device token registered (appName=$_appName)');
      _container?.read(pushRegistrationIssueProvider.notifier).state = null;
    } catch (e) {
      debugPrint('[Push] Token registration failed: $e');
      _reportIssue(
        const PushSetupIssue(
          message:
              'Push registration failed. Notifications may not arrive until '
              'the next login.',
          reason: PushSetupReason.registerFailed,
          // Backend blips should not hijack login UX.
          showSnackBar: false,
        ),
      );
    }
  }

  void _reportIssue(PushSetupIssue issue) {
    debugPrint('[Push] Failure (${issue.reason}): ${issue.message}');
    // ignore: unawaited_futures
    AnalyticsService.instance.log('push_registration_failed', {
      'reason': issue.reason,
      'snackbar': issue.showSnackBar,
    });
    if (_hintQueued) return;
    _hintQueued = true;
    final c = _container;
    if (c == null) return;
    try {
      c.read(pushRegistrationIssueProvider.notifier).state = issue;
    } catch (e) {
      debugPrint('[Push] Could not publish UI hint: $e');
    }
  }

  void clearHint() {
    _hintQueued = false;
    _container?.read(pushRegistrationIssueProvider.notifier).state = null;
  }
}
