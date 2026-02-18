import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/mqtt_service.dart';

final mqttServiceProvider = Provider<MqttService>((ref) {
  final service = MqttService();
  ref.onDispose(service.dispose);
  return service;
});
