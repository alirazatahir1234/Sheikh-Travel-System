import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/analytics/analytics_service.dart';
import '../../../core/api/api_endpoints.dart';
import 'fcm_service.dart';

/// Non-blocking hint after FCM/token registration fails post-login.
/// Settings (and optional banner) can show this once; clear after dismiss.
final pushRegistrationIssueProvider = StateProvider<String?>((ref) => null);

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

  Future<void> start(Dio dio, {String appName = 'fleet'}) async {
    _dio = dio;
    _appName = appName;
    _hintQueued = false;
    try {
      await FcmService.instance.initialize(onTokenRefresh: _registerToken);
      if (FcmService.instance.token == null) {
        _reportFailure(
          'Push notifications are not enabled on this device. '
          'You can still use the app; enable notifications in system settings if you need alerts.',
          reason: 'no_token',
        );
      }
    } catch (e) {
      debugPrint('[Push] FCM init skipped: $e');
      _reportFailure(
        'Could not set up push notifications. You can still use the app.',
        reason: 'init_failed',
      );
    }
  }

  Future<void> _registerToken(String token) async {
    final dio = _dio;
    if (dio == null) return;
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
