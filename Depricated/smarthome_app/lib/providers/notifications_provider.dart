import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:logger/logger.dart';

import '../core/mqtt_service.dart';
import '../core/notification_service.dart';
import '../models/notification_message.dart';
import 'mqtt_provider.dart';

class NotificationsState {
  const NotificationsState({this.active = const []});

  final List<NotificationMessage> active;

  NotificationsState copyWith({List<NotificationMessage>? active}) =>
      NotificationsState(active: active ?? this.active);
}

class NotificationsNotifier
    extends StateNotifier<NotificationsState> {
  NotificationsNotifier(this._mqtt) : super(const NotificationsState()) {
    _loadFromCache();
    _sub = _mqtt.messageStream.listen(_onMessage);
  }

  static final _log = Logger(printer: SimplePrinter());

  final MqttService _mqtt;
  StreamSubscription<AppMqttMessage>? _sub;

  void _loadFromCache() {
    final cached = _mqtt.getCachedByPrefix('Nachrichten/');
    for (final entry in cached.entries) {
      _onMessage(AppMqttMessage(entry.key, entry.value));
    }
  }

  void _onMessage(AppMqttMessage msg) {
    if (!msg.topic.startsWith('Nachrichten/')) return;
    try {
      final json = jsonDecode(msg.payload) as Map<String, dynamic>;
      final notification = NotificationMessage.fromJson(msg.topic, json);

      if (notification.bestaetigt) {
        // Remove from active list and cancel the system notification.
        final updated =
            state.active.where((n) => n.topic != msg.topic).toList();
        state = state.copyWith(active: updated);
        NotificationService.cancel(notification.notificationId);
      } else {
        // Add or update the message in the active list.
        final exists = state.active.any((n) => n.topic == msg.topic);
        final updated = [
          ...state.active.where((n) => n.topic != msg.topic),
          notification,
        ];
        state = state.copyWith(active: updated);
        // Only show system notification for genuinely new messages.
        if (!exists) {
          NotificationService.show(notification);
        }
      }
    } catch (e) {
      _log.e('Failed to parse notification on ${msg.topic}: $e\nPayload: ${msg.payload}');
    }
  }

  Future<void> acknowledge(NotificationMessage msg) async {
    final payload = jsonEncode(msg.copyWith(bestaetigt: true).toJson());
    await _mqtt.publish(msg.topic, payload, retained: true);
  }

  @override
  void dispose() {
    _sub?.cancel();
    super.dispose();
  }
}

final notificationsProvider =
    StateNotifierProvider<NotificationsNotifier, NotificationsState>((ref) {
  final mqtt = ref.watch(mqttServiceProvider);
  return NotificationsNotifier(mqtt);
});
