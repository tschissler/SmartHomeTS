import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../providers/illumination_provider.dart';

class LedScreen extends ConsumerStatefulWidget {
  const LedScreen({super.key});

  @override
  ConsumerState<LedScreen> createState() => _LedScreenState();
}

class _LedScreenState extends ConsumerState<LedScreen> {
  // Local slider values for smooth dragging (no MQTT publish on every tick)
  double? _localHue;
  double? _localBrightness;
  int? _localDensity;

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(illuminationProvider);
    final notifier = ref.read(illuminationProvider.notifier);

    final hue = _localHue ?? state.hue;
    final brightness = _localBrightness ?? state.brightness;
    final density = _localDensity ?? state.density;
    final ledOn = state.situation.right.on;
    final lampOn = state.situation.lampOn;
    final previewColor = hslToColor(hue, 100, brightness);

    return Scaffold(
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // ── Schalter ───────────────────────────────────────────────────
          Card(
            child: Column(
              children: [
                SwitchListTile(
                  secondary: const Icon(Icons.light_outlined),
                  title: const Text('Schrank-Beleuchtung'),
                  value: ledOn,
                  onChanged: (on) => notifier.setColor(
                    hue: hue,
                    brightness: brightness,
                    density: density,
                    ledOn: on,
                  ),
                ),
                const Divider(height: 1, indent: 16, endIndent: 16),
                SwitchListTile(
                  secondary: const Icon(Icons.emoji_objects_outlined),
                  title: const Text('Wohnzimmerlampe'),
                  value: lampOn,
                  onChanged: notifier.setLamp,
                ),
              ],
            ),
          ),

          const SizedBox(height: 12),

          // ── Farbregler ─────────────────────────────────────────────────
          Card(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 14, 16, 8),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Icon(Icons.palette_outlined,
                          size: 18,
                          color: Theme.of(context).colorScheme.primary),
                      const SizedBox(width: 6),
                      const Text('Farbe & Helligkeit',
                          style: TextStyle(fontWeight: FontWeight.bold)),
                    ],
                  ),
                  const SizedBox(height: 12),

                  // Hue slider
                  _SliderSection(
                    label: 'Farbton',
                    value: hue,
                    min: 0,
                    max: 360,
                    displayText: '${hue.round()}°',
                    gradient: const LinearGradient(colors: [
                      Color(0xFFFF0000),
                      Color(0xFFFFFF00),
                      Color(0xFF00FF00),
                      Color(0xFF00FFFF),
                      Color(0xFF0000FF),
                      Color(0xFFFF00FF),
                      Color(0xFFFF0000),
                    ]),
                    onChanged: (v) => setState(() => _localHue = v),
                    onChangeEnd: (v) {
                      _localHue = null;
                      notifier.setColor(
                          hue: v,
                          brightness: brightness,
                          density: density,
                          ledOn: ledOn);
                    },
                  ),

                  const SizedBox(height: 10),

                  // Brightness slider
                  _SliderSection(
                    label: 'Helligkeit',
                    value: brightness,
                    min: 0,
                    max: 100,
                    displayText: '${brightness.round()}%',
                    gradient: LinearGradient(colors: [
                      Colors.black,
                      hslToColor(hue, 100, 50),
                      Colors.white,
                    ]),
                    onChanged: (v) => setState(() => _localBrightness = v),
                    onChangeEnd: (v) {
                      _localBrightness = null;
                      notifier.setColor(
                          hue: hue,
                          brightness: v,
                          density: density,
                          ledOn: ledOn);
                    },
                  ),

                  const SizedBox(height: 10),

                  // Density slider
                  _SliderSection(
                    label: 'Dichte',
                    value: density.toDouble(),
                    min: 0,
                    max: 101,
                    displayText: '$density%',
                    gradient: LinearGradient(
                        colors: [Colors.transparent, previewColor]),
                    onChanged: (v) =>
                        setState(() => _localDensity = v.round()),
                    onChangeEnd: (v) {
                      _localDensity = null;
                      notifier.setColor(
                          hue: hue,
                          brightness: brightness,
                          density: v.round(),
                          ledOn: ledOn);
                    },
                  ),

                  const SizedBox(height: 12),
                ],
              ),
            ),
          ),

          const SizedBox(height: 12),

          // ── Farbvorschau ────────────────────────────────────────────────
          Card(
            color: previewColor,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
              child: Row(
                children: [
                  Icon(Icons.circle,
                      color: brightness > 50 ? Colors.black54 : Colors.white54,
                      size: 16),
                  const SizedBox(width: 8),
                  Text(
                    'R ${(previewColor.r * 255).round()}  '
                    'G ${(previewColor.g * 255).round()}  '
                    'B ${(previewColor.b * 255).round()}  '
                    '· $density% Dichte',
                    style: TextStyle(
                      color: brightness > 50 ? Colors.black : Colors.white,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ─── Reusable gradient slider ─────────────────────────────────────────────

class _SliderSection extends StatelessWidget {
  const _SliderSection({
    required this.label,
    required this.value,
    required this.min,
    required this.max,
    required this.displayText,
    required this.gradient,
    required this.onChanged,
    required this.onChangeEnd,
  });

  final String label;
  final double value;
  final double min;
  final double max;
  final String displayText;
  final LinearGradient gradient;
  final ValueChanged<double> onChanged;
  final ValueChanged<double> onChangeEnd;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(label,
                style: Theme.of(context).textTheme.labelLarge),
            Text(displayText,
                style: Theme.of(context).textTheme.labelLarge),
          ],
        ),
        const SizedBox(height: 4),
        Stack(
          alignment: Alignment.center,
          children: [
            Container(
              height: 8,
              margin: const EdgeInsets.symmetric(horizontal: 12),
              decoration: BoxDecoration(
                gradient: gradient,
                borderRadius: BorderRadius.circular(4),
              ),
            ),
            SliderTheme(
              data: SliderTheme.of(context).copyWith(
                trackHeight: 8,
                activeTrackColor: Colors.transparent,
                inactiveTrackColor: Colors.transparent,
              ),
              child: Slider(
                value: value.clamp(min, max),
                min: min,
                max: max,
                onChanged: onChanged,
                onChangeEnd: onChangeEnd,
              ),
            ),
          ],
        ),
      ],
    );
  }
}
