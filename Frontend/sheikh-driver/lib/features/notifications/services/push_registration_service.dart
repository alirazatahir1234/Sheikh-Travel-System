import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hive/hive.dart';

import '../../../core/analytics/analytics_service.dart';
import '../../../core/api/api_endpoints.dart';
import 'fcm_service.dart';

/// Hive key for user preference: push notifications enabled.
const pushNotificationsPrefsKey = 'pushNotificationsEnabled';

/// Non-blocking hint after FCM/token registration fails post-login.
/// Settings (and optional banner) can show this once; clear after dismiss.
final pushRegistrationIssueProvider = StateProvider<String?>((ref) => null);

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
  Future<void> start(Dio dio, {String appName = 'fleet'}) async {
    _dio = dio;
    _appName = appName;
    _hintQueued = false;

    if (!isPreferenceEnabled) {
      debugPrint('[Push] Skipped — user disabled push in Settings');
      return;
    }

    try {
      await FcmService.instance.initialize(onTokenRefresh: _registerToken);
      if (FcmService.instance.token == null) {
        _reportFailure(
          'Push notifications are not enabled on this device. '
          'Turn on the toggle in Settings and allow notifications when prompted.',
          reason: 'no_token',
        );
      } else {
        _container?.read(pushRegistrationIssueProvider.notifier).state = null;
      }
    } catch (e) {
      debugPrint('[Push] FCM init skipped: $e');
      _reportFailure(
        'Could not set up push notifications. Check Firebase config '
        '(google-services.json) and try the Settings toggle again.',
        reason: 'init_failed',
      );
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

    try {
      final allowed = await FcmService.instance.requestPermission();
      if (!allowed) {
        await _persistPreference(false);
        _reportFailure(
          'Notification permission was denied. Enable it in system Settings, '
          'then turn this toggle on again.',
          reason: 'permission_denied',
        );
        return false;
      }

      final token = await FcmService.instance.enable(
        onTokenRefresh: _registerToken,
      );
      if (token == null) {
        // OS permission OK but FCM token missing (often missing google-services.json).
        _reportFailure(
          'Notifications permission is on, but push delivery needs Firebase '
          '(add android/app/google-services.json). Alerts may not arrive yet.',
          reason: 'no_token',
        );
        // Keep toggle ON — user opted in; OS permission is granted.
        return true;
      }

      _container?.read(pushRegistrationIssueProvider.notifier).state = null;
      return true;
    } catch (e) {
      debugPrint('[Push] enableFromSettings failed: $e');
      _reportFailure(
        'Could not enable push: $e',
        reason: 'enable_failed',
      );
      // Keep preference on if OS permission was granted so retry is possible.
      final allowed = await FcmService.instance.hasPermission();
      if (!allowed) {
        await _persistPreference(false);
        return false;
      }
      return true;
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
      _reportFailure(
        'Push registration failed. Notifications may not arrive until the next login.',
        reason: 'register_failed',
      );
    }
  }

  void _reportFailure(String message, {required String reason}) {
    debugPrint('[Push] Failure ($reason): $message');
    // ignore: unawaited_futures
    AnalyticsService.instance.log('push_registration_failed', {'reason': reason});
    if (_hintQueued) return;
    _hintQueued = true;
    final c = _container;
    if (c == null) return;
    try {
      c.read(pushRegistrationIssueProvider.notifier).state = message;
    } catch (e) {
      debugPrint('[Push] Could not publish UI hint: $e');
    }
  }

  void clearHint() {
    _hintQueued = false;
    _container?.read(pushRegistrationIssueProvider.notifier).state = null;
  }
}
