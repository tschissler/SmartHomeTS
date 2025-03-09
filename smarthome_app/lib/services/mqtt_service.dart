import 'package:logger/logger.dart';
import 'package:mqtt_client/mqtt_client.dart';
import 'package:mqtt_client/mqtt_server_client.dart';

class MqttService {
  final MqttServerClient client;
  final Logger logger = Logger();
  Function(String topic, String message)? onMessageReceived;

  MqttService(String broker, String clientId, int port)
      : client = MqttServerClient.withPort(broker, clientId, port);

  Future<void> connect(List<String> topics) async {
    client.logging(on: false);
    client.keepAlivePeriod = 200;
    client.onDisconnected = onDisconnected;
    client.onConnected = onConnected;
    client.onSubscribed = onSubscribed;

    final connMess = MqttConnectMessage()
        .withClientIdentifier('Mqtt_MyClientUniqueId')
        .withWillQos(MqttQos.atLeastOnce);
    client.connectionMessage = connMess;
    try {
      await client.connect();
    } catch (e) {
      logger.i('Exception: $e');
      client.disconnect();
    }

    if (client.connectionStatus!.state == MqttConnectionState.connected) {
      logger.i('MQTT client connected');
    } else {
      logger.i('ERROR: MQTT client connection failed - disconnecting, status is ${client.connectionStatus}');
      client.disconnect();
    }

    for (var topic in topics) {
      subscribe(topic);
    }

    listenToMessages();
  }

  void subscribe(String topic) {
    if (client.connectionStatus!.state == MqttConnectionState.connected) {
      client.subscribe(topic, MqttQos.atLeastOnce);
      logger.i('Subscribing to $topic');
    } else {
      logger.i('Cannot subscribe, client is not connected');
    }
  }

  void onConnected() {
    logger.i('Connected');
  }

  void onDisconnected() {
    logger.i('Disconnected');
  }

  void onSubscribed(String topic) {
    logger.i('Subscribed to $topic');
  }

  void listenToMessages() {
    client.updates!.listen((List<MqttReceivedMessage<MqttMessage>> c) {
      final MqttPublishMessage recMess = c[0].payload as MqttPublishMessage;
      final String pt = MqttPublishPayload.bytesToStringAsString(recMess.payload.message);

      // print('Received message: $pt from topic: ${c[0].topic}');
      if (onMessageReceived != null) {
        onMessageReceived!(c[0].topic, pt);
      }
    });
  }
}