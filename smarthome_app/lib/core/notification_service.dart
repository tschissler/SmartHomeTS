import 'dart:io';

import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:logger/logger.dart';

import '../models/notification_message.dart';

/// Wraps [FlutterLocalNotificationsPlugin] for Android.
/// On other platforms (Windows, web) system notifications are silently skipped;
/// the in-app notification panel always works regardless.
class NotificationService {
  NotificationService._();

  static final _log = Logger(printer: SimplePrinter());
  static final _plugin = FlutterLocalNotificationsPlugin();
  static bool _initialized = false;

  static const _channelId = 'smarthome_alerts';
  static const _channelName = 'SmartHome Alerts';

  static Future<void> initialize() async {
    if (_initialized) return;
    if (!Platform.isAndroid) {
      _log.i('NotificationService: system notifications not supported on this platform, skipping');
      return;
    }
    try {
      const androidSettings =
          AndroidInitializationSettings('@mipmap/ic_launcher');
      const settings = InitializationSettings(android: androidSettings);
      await _plugin.initialize(settings);

      // Create notification channel (Android 8+)
      await _plugin
          .resolvePlatformSpecificImplementation<
              AndroidFlutterLocalNotificationsPlugin>()
          ?.createNotificationChannel(
            const AndroidNotificationChannel(
              _channelId,
              _channelName,
              description: 'Benachrichtigungen vom SmartHome-System',
              importance: Importance.high,
            ),
          );

      // Request permission (Android 13+)
      await _plugin
          .resolvePlatformSpecificImplementation<
              AndroidFlutterLocalNotificationsPlugin>()
          ?.requestNotificationsPermission();

      _initialized = true;
      _log.i('NotificationService initialized');
    } catch (e) {
      _log.w('NotificationService init failed (non-critical): $e');
    }
  }

  static Future<void> show(NotificationMessage msg) async {
    if (!_initialized) return;
    try {
      const details = NotificationDetails(
        android: AndroidNotificationDetails(
          _channelId,
          _channelName,
          channelDescription: 'Benachrichtigungen vom SmartHome-System',
          importance: Importance.high,
          priority: Priority.high,
          icon: '@mipmap/ic_launcher',
        ),
      );
      await _plugin.show(
        msg.notificationId,
        '${msg.typeLabel}: ${msg.title}',
        msg.text,
        details,
      );
    } catch (e) {
      _log.w('Could not show notification: $e');
    }
  }

  static Future<void> cancel(int id) async {
    if (!_initialized) return;
    try {
      await _plugin.cancel(id);
    } catch (e) {
      _log.w('Could not cancel notification $id: $e');
    }
  }
}
