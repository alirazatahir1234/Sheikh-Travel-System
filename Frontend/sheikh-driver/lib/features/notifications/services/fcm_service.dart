import 'dart:async';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import '../domain/notification_models.dart';
import 'notification_deep_link.dart';

@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  debugPrint('FCM background: ${message.messageId}');
}

typedef NotificationTapCallback = void Function(String? route);

class FcmService {
  FcmService._();
  static final instance = FcmService._();

  String? _token;
  String? get token => _token;

  bool _initialized = false;
  bool _tapHandlersBound = false;

  final _foregroundController =
      StreamController<ForegroundBannerEvent>.broadcast();
  final _refreshController = StreamController<void>.broadcast();

  Stream<ForegroundBannerEvent> get foregroundBanners =>
      _foregroundController.stream;
  Stream<void> get inboxRefresh => _refreshController.stream;

  Future<void> initialize({
    required Future<void> Function(String token) onTokenRefresh,
  }) async {
    if (_initialized) return;
    _initialized = true;

    try {
      FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);

      final settings = await FirebaseMessaging.instance.requestPermission(
        alert: true,
        badge: true,
        sound: true,
      );

      if (settings.authorizationStatus == AuthorizationStatus.authorized ||
          settings.authorizationStatus == AuthorizationStatus.provisional) {
        _token = await FirebaseMessaging.instance.getToken();
        if (_token != null) await onTokenRefresh(_token!);

        FirebaseMessaging.instance.onTokenRefresh.listen((newToken) async {
          _token = newToken;
          await onTokenRefresh(newToken);
        });
      }

      await FirebaseMessaging.instance
          .setForegroundNotificationPresentationOptions(
        alert: true,
        badge: true,
        sound: true,
      );

      FirebaseMessaging.onMessage.listen((message) {
        final title = message.notification?.title ??
            message.data['title']?.toString() ??
            'SheikhGo';
        final body = message.notification?.body ??
            message.data['body']?.toString() ??
            message.data['message']?.toString() ??
            '';
        final route = NotificationDeepLink.fromData(
          Map<String, dynamic>.from(message.data),
        );
        final notifId =
            int.tryParse(message.data['notificationId']?.toString() ?? '');
        _foregroundController.add(
          ForegroundBannerEvent(
            title: title,
            body: body,
            route: route,
            notificationId: notifId,
          ),
        );
        _refreshController.add(null);
      });
    } catch (e) {
      debugPrint('[FCM] initialize failed: $e');
    }
  }

  static void handleMessageTaps({required NotificationTapCallback onTap}) {
    if (instance._tapHandlersBound) return;
    instance._tapHandlersBound = true;

    try {
      FirebaseMessaging.onMessageOpenedApp.listen((message) {
        onTap(NotificationDeepLink.fromData(
            Map<String, dynamic>.from(message.data)));
      });

      FirebaseMessaging.instance.getInitialMessage().then((message) {
        if (message != null) {
          onTap(NotificationDeepLink.fromData(
              Map<String, dynamic>.from(message.data)));
        }
      });
    } catch (e) {
      // Firebase may be unavailable in tests or when init was skipped.
      debugPrint('[FCM] Tap handlers skipped: $e');
      instance._tapHandlersBound = false;
    }
  }
}
