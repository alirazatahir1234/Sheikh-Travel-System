import 'dart:io';
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import '../../../core/api/api_endpoints.dart';
import 'fcm_service.dart';

/// Registers FCM device tokens with the SheikhGo AI push pipeline.
class PushRegistrationService {
  PushRegistrationService._();
  static final instance = PushRegistrationService._();

  Dio? _dio;
  String _appName = 'fleet';

  Future<void> start(Dio dio, {String appName = 'fleet'}) async {
    _dio = dio;
    _appName = appName;
    try {
      await FcmService.instance.initialize(onTokenRefresh: _registerToken);
    } catch (e) {
      debugPrint('[Push] FCM init skipped: $e');
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
    } catch (e) {
      debugPrint('[Push] Token registration failed: $e');
    }
  }
}
