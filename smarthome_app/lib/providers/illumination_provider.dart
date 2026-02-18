import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/mqtt_service.dart';
import '../core/mqtt_topics.dart';
import '../models/illumination_situation.dart';
import 'mqtt_provider.dart';

// ─── HSL ↔ RGB helpers ────────────────────────────────────────────────────

Color hslToColor(double h, double s, double l) {
  final s1 = s / 100.0;
  final l1 = l / 100.0;
  final c = (1.0 - (2.0 * l1 - 1.0).abs()) * s1;
  final x = c * (1.0 - ((h / 60.0) % 2.0 - 1.0).abs());
  final m = l1 - c / 2.0;
  double r, g, b;
  if (h < 60) {
    r = c;
    g = x;
    b = 0;
  } else if (h < 120) {
    r = x;
    g = c;
    b = 0;
  } else if (h < 180) {
    r = 0;
    g = c;
    b = x;
  } else if (h < 240) {
    r = 0;
    g = x;
    b = c;
  } else if (h < 300) {
    r = x;
    g = 0;
    b = c;
  } else {
    r = c;
    g = 0;
    b = x;
  }
  return Color.fromARGB(
    255,
    ((r + m) * 255).round().clamp(0, 255),
    ((g + m) * 255).round().clamp(0, 255),
    ((b + m) * 255).round().clamp(0, 255),
  );
}

/// Returns (h: 0–360, l: 0–100) – saturation is always 100 in this app.
({double h, double l}) colorToHsl(int r, int g, int b) {
  final rf = r / 255.0;
  final gf = g / 255.0;
  final bf = b / 255.0;
  final max = [rf, gf, bf].reduce((a, b) => a > b ? a : b);
  final min = [rf, gf, bf].reduce((a, b) => a < b ? a : b);
  final l = (max + min) / 2.0;
  if (max == min) return (h: 0.0, l: l * 100);
  final d = max - min;
  double h;
  if (max == rf) {
    h = (gf - bf) / d + (gf < bf ? 6.0 : 0.0);
  } else if (max == gf) {
    h = (bf - rf) / d + 2.0;
  } else {
    h = (rf - gf) / d + 4.0;
  }
  return (h: (h / 6.0) * 360.0, l: l * 100.0);
}

// ─── State class ──────────────────────────────────────────────────────────

class IlluminationState {
  final IlluminationSituation situation;
  final double hue;
  final double brightness;
  final int density;

  const IlluminationState({
    this.situation = const IlluminationSituation(),
    this.hue = 30,
    this.brightness = 50,
    this.density = 10,
  });

  IlluminationState copyWith({
    IlluminationSituation? situation,
    double? hue,
    double? brightness,
    int? density,
  }) {
    return IlluminationState(
      situation: situation ?? this.situation,
      hue: hue ?? this.hue,
      brightness: brightness ?? this.brightness,
      density: density ?? this.density,
    );
  }
}

// ─── StateNotifier ────────────────────────────────────────────────────────

class IlluminationNotifier extends StateNotifier<IlluminationState> {
  IlluminationNotifier(this._mqtt) : super(const IlluminationState()) {
    _initFromCache();
    _sub = _mqtt.messageStream.listen(_handleMessage);
  }

  final MqttService _mqtt;
  late final StreamSubscription<AppMqttMessage> _sub;

  void _initFromCache() {
    final cached = _mqtt.getLatestMessage(MqttTopics.ledStripe);
    if (cached != null) _applyLedJson(cached);
    final lamp = _mqtt.getLatestMessage(MqttTopics.lamp);
    if (lamp != null) _applyLamp(lamp);
  }

  void _handleMessage(AppMqttMessage msg) {
    if (msg.topic == MqttTopics.ledStripe) {
      _applyLedJson(msg.payload);
    } else if (msg.topic == MqttTopics.lamp) {
      _applyLamp(msg.payload);
    }
  }

  void _applyLedJson(String payload) {
    try {
      final sit =
          IlluminationSituation.fromJson(jsonDecode(payload) as Map<String, dynamic>);
      final hsl = colorToHsl(sit.right.red, sit.right.green, sit.right.blue);
      state = state.copyWith(
        situation: sit,
        hue: hsl.h,
        brightness: hsl.l,
        density: sit.right.density,
      );
    } catch (_) {}
  }

  void _applyLamp(String payload) {
    final on = payload.toLowerCase() == 'on';
    state = state.copyWith(
        situation: state.situation.copyWith(lampOn: on));
  }

  Future<void> setColor({
    required double hue,
    required double brightness,
    required int density,
    required bool ledOn,
  }) async {
    final color = hslToColor(hue, 100, brightness);
    final settings = IlluminationSettings(
      red: (color.r * 255.0).round() & 0xff,
      green: (color.g * 255.0).round() & 0xff,
      blue: (color.b * 255.0).round() & 0xff,
      density: density,
      on: ledOn,
    );
    final newSit = state.situation.copyWith(left: settings, right: settings);
    state = state.copyWith(
        situation: newSit, hue: hue, brightness: brightness, density: density);
    await _mqtt.publish(
        MqttTopics.ledStripe, jsonEncode(newSit.toJson()));
  }

  Future<void> setLamp(bool on) async {
    state = state.copyWith(situation: state.situation.copyWith(lampOn: on));
    await _mqtt.publish(MqttTopics.lamp, on ? 'on' : 'off');
  }

  @override
  void dispose() {
    _sub.cancel();
    super.dispose();
  }
}

// ─── Provider ─────────────────────────────────────────────────────────────

final illuminationProvider =
    StateNotifierProvider<IlluminationNotifier, IlluminationState>((ref) {
  return IlluminationNotifier(ref.watch(mqttServiceProvider));
});
