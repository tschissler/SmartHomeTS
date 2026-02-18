import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/mqtt_service.dart';
import '../core/mqtt_topics.dart';
import '../models/car_status.dart';
import '../models/charging_settings.dart';
import '../models/charging_situation.dart';
import 'mqtt_provider.dart';

// ─── State class ──────────────────────────────────────────────────────────

class ChargingState {
  final ChargingSituation situation;
  final ChargingSettings settings;
  final CarStatus? bmwStatus;
  final CarStatus? miniStatus;
  final CarStatus? vwStatus;

  const ChargingState({
    this.situation = const ChargingSituation(),
    this.settings = const ChargingSettings(),
    this.bmwStatus,
    this.miniStatus,
    this.vwStatus,
  });

  ChargingState copyWith({
    ChargingSituation? situation,
    ChargingSettings? settings,
    CarStatus? bmwStatus,
    CarStatus? miniStatus,
    CarStatus? vwStatus,
  }) {
    return ChargingState(
      situation: situation ?? this.situation,
      settings: settings ?? this.settings,
      bmwStatus: bmwStatus ?? this.bmwStatus,
      miniStatus: miniStatus ?? this.miniStatus,
      vwStatus: vwStatus ?? this.vwStatus,
    );
  }
}

// ─── StateNotifier ────────────────────────────────────────────────────────

class ChargingNotifier extends StateNotifier<ChargingState> {
  ChargingNotifier(this._mqtt) : super(const ChargingState()) {
    _initFromCache();
    _sub = _mqtt.messageStream.listen(_handleMessage);
  }

  final MqttService _mqtt;
  late final StreamSubscription<AppMqttMessage> _sub;

  void _initFromCache() {
    _tryApply(MqttTopics.chargingSituation, _applySituation);
    _tryApply(MqttTopics.chargingSettings, _applySettings);
    _tryApply(MqttTopics.chargingBmw, _applyBmw);
    _tryApply(MqttTopics.chargingMini, _applyMini);
    _tryApply(MqttTopics.chargingVw, _applyVw);
  }

  void _tryApply(String topic, void Function(String) fn) {
    final cached = _mqtt.getLatestMessage(topic);
    if (cached != null) fn(cached);
  }

  void _handleMessage(AppMqttMessage msg) {
    switch (msg.topic) {
      case MqttTopics.chargingSituation:
        _applySituation(msg.payload);
      case MqttTopics.chargingSettings:
        _applySettings(msg.payload);
      case MqttTopics.chargingBmw:
        _applyBmw(msg.payload);
      case MqttTopics.chargingMini:
        _applyMini(msg.payload);
      case MqttTopics.chargingVw:
        _applyVw(msg.payload);
    }
  }

  void _applySituation(String payload) {
    try {
      state = state.copyWith(
          situation: ChargingSituation.fromJson(
              jsonDecode(payload) as Map<String, dynamic>));
    } catch (_) {}
  }

  void _applySettings(String payload) {
    try {
      state = state.copyWith(
          settings: ChargingSettings.fromJson(
              jsonDecode(payload) as Map<String, dynamic>));
    } catch (_) {}
  }

  void _applyBmw(String payload) {
    try {
      state = state.copyWith(
          bmwStatus:
              CarStatus.fromJson(jsonDecode(payload) as Map<String, dynamic>));
    } catch (_) {}
  }

  void _applyMini(String payload) {
    try {
      state = state.copyWith(
          miniStatus:
              CarStatus.fromJson(jsonDecode(payload) as Map<String, dynamic>));
    } catch (_) {}
  }

  void _applyVw(String payload) {
    try {
      state = state.copyWith(
          vwStatus:
              CarStatus.fromJson(jsonDecode(payload) as Map<String, dynamic>));
    } catch (_) {}
  }

  Future<void> setChargingLevel(int level) async {
    final newSettings = state.settings.copyWith(chargingLevel: level);
    state = state.copyWith(settings: newSettings);
    await _mqtt.publish(
        MqttTopics.chargingSettings, jsonEncode(newSettings.toJson()));
  }

  @override
  void dispose() {
    _sub.cancel();
    super.dispose();
  }
}

// ─── Provider ─────────────────────────────────────────────────────────────

final chargingProvider =
    StateNotifierProvider<ChargingNotifier, ChargingState>((ref) {
  return ChargingNotifier(ref.watch(mqttServiceProvider));
});
