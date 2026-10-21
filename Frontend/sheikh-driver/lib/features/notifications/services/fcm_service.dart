import 'dart:async';
import 'dart:io';

import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:permission_handler/permission_handler.dart';
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

  bool _listenersBound = false;
  bool _tapHandlersBound = false;
  bool _backgroundHandlerBound = false;

  final _foregroundController =
      StreamController<ForegroundBannerEvent>.broadcast();
  final _refreshController = StreamController<void>.broadcast();

  Stream<ForegroundBannerEvent> get foregroundBanners =>
      _foregroundController.stream;
  Stream<void> get inboxRefresh => _refreshController.stream;

  /// OS + FCM authorization currently granted.
  Future<bool> hasPermission() async {
    try {
      if (Platform.isAndroid) {
        final status = await Permission.notification.status;
        if (status.isGranted || status.isLimited) return true;
      }
      final settings = await FirebaseMessaging.instance.getNotificationSettings();
      return settings.authorizationStatus == AuthorizationStatus.authorized ||
          settings.authorizationStatus == AuthorizationStatus.provisional;
    } catch (e) {
      debugPrint('[FCM] hasPermission failed: $e');
      if (Platform.isAndroid) {
        try {
          return await Permission.notification.isGranted;
        } catch (_) {
          return false;
        }
      }
      return false;
    }
  }

  /// Requests Android POST_NOTIFICATIONS + APNs/FCM alert permission.
  Future<bool> requestPermission() async {
    try {
      if (Platform.isAndroid) {
        final status = await Permission.notification.request();
        if (status.isPermanentlyDenied) {
          debugPrint('[FCM] Notification permission permanently denied');
          return false;
        }
        if (!status.isGranted && !status.isLimited) {
          // On API < 33 permission_handler may return denied; still try FCM.
          if (status.isDenied) {
            debugPrint('[FCM] Android notification status=$status — trying FCM');
          }
        }
      }

      final settings = await FirebaseMessaging.instance.requestPermission(
        alert: true,
        badge: true,
        sound: true,
      );
      final ok = settings.authorizationStatus == AuthorizationStatus.authorized ||
          settings.authorizationStatus == AuthorizationStatus.provisional;
      if (!ok && Platform.isAndroid) {
        // Android 13+: OS grant is enough even if FCM reports denied before init.
        return await Permission.notification.isGranted;
      }
      return ok;
    } catch (e) {
      debugPrint('[FCM] requestPermission failed: $e');
      if (Platform.isAndroid) {
        try {
          return await Permission.notification.isGranted;
        } catch (_) {
          return false;
        }
      }
      return false;
    }
  }

  /// Initialize listeners + obtain token when permission is granted.
  /// Safe to call repeatedly (e.g. Settings toggle ON after a failed first boot).
  Future<void> initialize({
    required Future<void> Function(String token) onTokenRefresh,
  }) async {
    try {
      await _ensureBackgroundHandler();
      await _bindForegroundListeners();

      final allowed = await requestPermission();
      if (!allowed) {
        debugPrint('[FCM] Permission not granted — no token');
        return;
      }

      await FirebaseMessaging.instance
          .setForegroundNotificationPresentationOptions(
        alert: true,
        badge: true,
        sound: true,
      );

      _token = await FirebaseMessaging.instance.getToken();
      if (_token != null) await onTokenRefresh(_token!);

      FirebaseMessaging.instance.onTokenRefresh.listen((newToken) async {
        _token = newToken;
        await onTokenRefresh(newToken);
      });
    } catch (e) {
      debugPrint('[FCM] initialize failed: $e');
      rethrow;
    }
  }

  /// Re-request permission and refresh the FCM token (Settings enable path).
  Future<String?> enable({
    required Future<void> Function(String token) onTokenRefresh,
  }) async {
    await initialize(onTokenRefresh: onTokenRefresh);
    return _token;
  }

  /// Stop receiving push on this device (Settings disable path).
  Future<void> disable() async {
    try {
      await FirebaseMessaging.instance.deleteToken();
    } catch (e) {
      debugPrint('[FCM] deleteToken failed: $e');
    }
    _token = null;
  }

  Future<void> _ensureBackgroundHandler() async {
    if (_backgroundHandlerBound) return;
    try {
      FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);
      _backgroundHandlerBound = true;
    } catch (e) {
      debugPrint('[FCM] background handler bind failed: $e');
    }
  }

  Future<void> _bindForegroundListeners() async {
    if (_listenersBound) return;
    try {
      FirebaseMessaging.onMessage.listen((message) {
        final title = message.notification?.title ??
            message.data['title']?.toString() ??
            'SheikhGo Tracking';
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
      _listenersBound = true;
    } catch (e) {
      debugPrint('[FCM] foreground listeners bind failed: $e');
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
