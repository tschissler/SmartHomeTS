import 'dart:async';
import 'dart:convert';

import 'package:logger/logger.dart';
import 'package:mqtt_client/mqtt_client.dart';
import 'package:mqtt_client/mqtt_server_client.dart';

import 'mqtt_topics.dart';

class AppMqttMessage {
  final String topic;
  final String payload;

  const AppMqttMessage(this.topic, this.payload);
}

class MqttService {
  static final _log = Logger(printer: SimplePrinter());

  // Nullable – stays null on platforms where dart:io is unavailable (web).
  MqttServerClient? _client;
  final _messageController = StreamController<AppMqttMessage>.broadcast();
  final Map<String, String> _latestMessages = {};
  StreamSubscription<List<MqttReceivedMessage<MqttMessage?>>>? _updatesSub;

  Stream<AppMqttMessage> get messageStream => _messageController.stream;

  /// Returns the last known payload for a topic (from retained or recent messages).
  String? getLatestMessage(String topic) => _latestMessages[topic];

  /// Returns all cached topic→payload pairs whose topic starts with [prefix].
  Map<String, String> getCachedByPrefix(String prefix) => Map.fromEntries(
      _latestMessages.entries.where((e) => e.key.startsWith(prefix)));

  MqttService() {
    try {
      _client = MqttServerClient.withPort(
          'mosquitto.intern', 'Smarthome.App.Flutter', 1883);
      _client!.logging(on: false);
      _client!.keepAlivePeriod = 60;
      _client!.autoReconnect = true;
      _client!.onConnected = _onConnected;
      _client!.onAutoReconnected = _onAutoReconnected;
      _client!.onDisconnected = _onDisconnected;
      _connect();
    } on UnsupportedError catch (e) {
      // dart:io is not available on web – MQTT is disabled on this platform.
      _log.w('MQTT not available on this platform: $e');
    } catch (e) {
      _log.e('MQTT init error: $e');
    }
  }

  Future<void> _connect() async {
    final c = _client;
    if (c == null) return;
    final connMsg = MqttConnectMessage()
        .withClientIdentifier('Smarthome.App.Flutter')
        .startClean();
    c.connectionMessage = connMsg;
    try {
      await c.connect();
    } catch (e) {
      _log.e('MQTT connect error: $e');
    }
  }

  void _subscribeAll() {
    final c = _client;
    if (c == null) return;
    for (final topic in MqttTopics.subscriptions) {
      c.subscribe(topic, MqttQos.atLeastOnce);
    }
  }

  void _onConnected() {
    _log.i('Connected to MQTT broker');
    _subscribeAll();
    _updatesSub?.cancel();
    _updatesSub = _client!.updates!
        .listen((List<MqttReceivedMessage<MqttMessage?>> events) {
      for (final event in events) {
        final msg = event.payload as MqttPublishMessage;
        try {
          final payload =
              MqttPublishPayload.bytesToStringAsString(msg.payload.message);
          if (payload.isNotEmpty) {
            _latestMessages[event.topic] = payload;
            _messageController.add(AppMqttMessage(event.topic, payload));
          }
        } catch (_) {
          // UTF-8 failed – try Latin-1 (legacy .NET publishers using Windows-1252)
          try {
            final payload = latin1.decode(msg.payload.message.toList());
            if (payload.isNotEmpty) {
              _latestMessages[event.topic] = payload;
              _messageController.add(AppMqttMessage(event.topic, payload));
            }
          } catch (e) {
            _log.w('Skipping undecodable payload on topic ${event.topic}: $e');
          }
        }
      }
    });
  }

  void _onAutoReconnected() {
    _log.i('MQTT auto-reconnected – re-subscribing');
    _subscribeAll();
  }

  void _onDisconnected() {
    _log.w('MQTT disconnected');
  }

  Future<void> publish(
    String topic,
    String payload, {
    bool retained = true,
    MqttQos qos = MqttQos.atLeastOnce,
  }) async {
    final c = _client;
    if (c == null ||
        c.connectionStatus?.state != MqttConnectionState.connected) {
      _log.w('Cannot publish – not connected');
      return;
    }
    final builder = MqttClientPayloadBuilder()..addString(payload);
    c.publishMessage(topic, qos, builder.payload!, retain: retained);
  }

  void dispose() {
    _updatesSub?.cancel();
    _client?.disconnect();
    _messageController.close();
  }
}
