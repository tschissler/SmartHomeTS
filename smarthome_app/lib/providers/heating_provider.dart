import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/mqtt_service.dart';
import '../core/mqtt_topics.dart';
import '../models/heating_command.dart';
import 'mqtt_provider.dart';

// ─── State class ──────────────────────────────────────────────────────────

class HeatingState {
  final HeatingCommand kinderzimmer;
  final HeatingCommand esszimmer;

  const HeatingState({
    this.kinderzimmer = const HeatingCommand(mode: 'Auto', fanspeed: 50),
    this.esszimmer = const HeatingCommand(mode: 'Auto', fanspeed: 50),
  });

  HeatingState copyWith({HeatingCommand? kinderzimmer, HeatingCommand? esszimmer}) {
    return HeatingState(
      kinderzimmer: kinderzimmer ?? this.kinderzimmer,
      esszimmer: esszimmer ?? this.esszimmer,
    );
  }
}

// ─── StateNotifier ────────────────────────────────────────────────────────

class HeatingNotifier extends StateNotifier<HeatingState> {
  HeatingNotifier(this._mqtt) : super(const HeatingState()) {
    _initFromCache();
    _sub = _mqtt.messageStream.listen(_handleMessage);
  }

  final MqttService _mqtt;
  late final StreamSubscription<AppMqttMessage> _sub;

  void _initFromCache() {
    final kz = _mqtt.getLatestMessage(MqttTopics.heatingKinderzimmer);
    if (kz != null) _applyKinderzimmer(kz);
    final ez = _mqtt.getLatestMessage(MqttTopics.heatingEsszimmer);
    if (ez != null) _applyEsszimmer(ez);
  }

  void _handleMessage(AppMqttMessage msg) {
    if (msg.topic == MqttTopics.heatingKinderzimmer) {
      _applyKinderzimmer(msg.payload);
    } else if (msg.topic == MqttTopics.heatingEsszimmer) {
      _applyEsszimmer(msg.payload);
    }
  }

  void _applyKinderzimmer(String payload) {
    try {
      state = state.copyWith(
          kinderzimmer: HeatingCommand.fromJson(
              jsonDecode(payload) as Map<String, dynamic>));
    } catch (_) {}
  }

  void _applyEsszimmer(String payload) {
    try {
      state = state.copyWith(
          esszimmer: HeatingCommand.fromJson(
              jsonDecode(payload) as Map<String, dynamic>));
    } catch (_) {}
  }

  Future<void> setKinderzimmerMode(String displayMode) async {
    final cmd = HeatingCommand.fromDisplayMode(displayMode);
    state = state.copyWith(kinderzimmer: cmd);
    await _mqtt.publish(
        MqttTopics.heatingKinderzimmer, jsonEncode(cmd.toJson()));
  }

  Future<void> setEsszimmerMode(String displayMode) async {
    final cmd = HeatingCommand.fromDisplayMode(displayMode);
    state = state.copyWith(esszimmer: cmd);
    await _mqtt.publish(
        MqttTopics.heatingEsszimmer, jsonEncode(cmd.toJson()));
  }

  @override
  void dispose() {
    _sub.cancel();
    super.dispose();
  }
}

// ─── Provider ─────────────────────────────────────────────────────────────

final heatingProvider =
    StateNotifierProvider<HeatingNotifier, HeatingState>((ref) {
  return HeatingNotifier(ref.watch(mqttServiceProvider));
});
