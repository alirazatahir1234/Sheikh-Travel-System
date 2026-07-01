import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

// Top-level handler — runs in a background isolate when app is terminated.
@pragma('vm:entry-point')
Future<void> _firebaseBackgroundHandler(RemoteMessage message) async {
  // No UI available here — heavy work like DB writes go here.
  debugPrint('FCM background: ${message.messageId}');
}

class FcmService {
  FcmService._();
  static final instance = FcmService._();

  String? _token;
  String? get token => _token;

  Future<void> initialize({required Future<void> Function(String token) onTokenRefresh}) async {
    FirebaseMessaging.onBackgroundMessage(_firebaseBackgroundHandler);

    // Request permission (iOS — Android 13+ also needs this)
    final settings = await FirebaseMessaging.instance.requestPermission(
      alert: true,
      badge: true,
      sound: true,
    );

    if (settings.authorizationStatus == AuthorizationStatus.authorized ||
        settings.authorizationStatus == AuthorizationStatus.provisional) {
      _token = await FirebaseMessaging.instance.getToken();
      if (_token != null) await onTokenRefresh(_token!);

      // Refresh handler
      FirebaseMessaging.instance.onTokenRefresh.listen((newToken) async {
        _token = newToken;
        await onTokenRefresh(newToken);
      });
    }

    // Foreground message banner
    await FirebaseMessaging.instance.setForegroundNotificationPresentationOptions(
      alert: true,
      badge: true,
      sound: true,
    );
  }

  /// Call this from main widget's initState to handle notification taps.
  static void handleMessageTaps({required void Function(RemoteMessage) onTap}) {
    // Notification tap while app in foreground
    FirebaseMessaging.onMessageOpenedApp.listen(onTap);

    // App was launched from a terminated state via notification
    FirebaseMessaging.instance.getInitialMessage().then((message) {
      if (message != null) onTap(message);
    });
  }
}
