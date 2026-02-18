import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/mqtt_service.dart';
import '../core/mqtt_topics.dart';
import '../models/climate_data.dart';
import 'mqtt_provider.dart';

// ─── StateNotifier ────────────────────────────────────────────────────────

class ClimateNotifier extends StateNotifier<ClimateData> {
  ClimateNotifier(this._mqtt) : super(ClimateData()) {
    _initFromCache();
    _sub = _mqtt.messageStream.listen(_handleMessage);
  }

  final MqttService _mqtt;
  late final StreamSubscription<AppMqttMessage> _sub;

  void _initFromCache() {
    for (final topic in _topicMap) {
      final cached = _mqtt.getLatestMessage(topic);
      if (cached != null) _applyPayload(topic, cached);
    }
  }

  void _handleMessage(AppMqttMessage msg) {
    if (_topicMap.contains(msg.topic)) {
      _applyPayload(msg.topic, msg.payload);
    }
  }

  void _applyPayload(String topic, String payload) {
    final value = double.tryParse(payload.replaceAll(',', '.'));
    if (value == null) return;
    final point = DataPoint(value: value, lastUpdated: DateTime.now());
    state = switch (topic) {
      MqttTopics.tempKeller => state.copyWith(basementTemperature: point),
      MqttTopics.humKeller => state.copyWith(basementHumidity: point),
      MqttTopics.tempAussen => state.copyWith(outsideTemperature: point),
      MqttTopics.humAussen => state.copyWith(outsideHumidity: point),
      MqttTopics.tempKinderzimmer =>
        state.copyWith(childRoomTemperature: point),
      MqttTopics.humKinderzimmer => state.copyWith(childRoomHumidity: point),
      MqttTopics.tempBad => state.copyWith(bathRoomM1Temperature: point),
      MqttTopics.humBad => state.copyWith(bathRoomM1Humidity: point),
      MqttTopics.tempWohnzimmer => state.copyWith(livingRoomTemperature: point),
      MqttTopics.humWohnzimmer => state.copyWith(livingRoomHumidity: point),
      MqttTopics.tempSchlafzimmer => state.copyWith(bedroomTemperature: point),
      MqttTopics.humSchlafzimmer => state.copyWith(bedroomHumidity: point),
      MqttTopics.cistern => state.copyWith(cisternFillLevel: point),
      _ => state,
    };
  }

  // Topics that this notifier cares about (for cache init)
  static const List<String> _topicMap = [
    MqttTopics.tempKeller,
    MqttTopics.humKeller,
    MqttTopics.tempAussen,
    MqttTopics.humAussen,
    MqttTopics.tempKinderzimmer,
    MqttTopics.humKinderzimmer,
    MqttTopics.tempBad,
    MqttTopics.humBad,
    MqttTopics.tempWohnzimmer,
    MqttTopics.humWohnzimmer,
    MqttTopics.tempSchlafzimmer,
    MqttTopics.humSchlafzimmer,
    MqttTopics.cistern,
  ];

  @override
  void dispose() {
    _sub.cancel();
    super.dispose();
  }
}

// ─── Provider ─────────────────────────────────────────────────────────────

final climateProvider =
    StateNotifierProvider<ClimateNotifier, ClimateData>((ref) {
  return ClimateNotifier(ref.watch(mqttServiceProvider));
});
