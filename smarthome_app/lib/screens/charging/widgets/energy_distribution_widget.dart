import 'package:flutter/material.dart';

import '../../../models/charging_situation.dart';

class EnergyDistributionWidget extends StatelessWidget {
  const EnergyDistributionWidget({super.key, required this.situation});

  final ChargingSituation situation;

  @override
  Widget build(BuildContext context) {
    final pvW = situation.powerFromPV;
    final batW = situation.powerFromBattery < 0
        ? situation.powerFromBattery.abs()
        : 0;
    final gridInW =
        situation.powerFromGrid < 0 ? situation.powerFromGrid.abs() : 0;

    final houseW = situation.houseConsumptionPower;
    final batChargeW =
        situation.powerFromBattery > 0 ? situation.powerFromBattery : 0;
    final gridFeedW =
        situation.powerFromGrid > 0 ? situation.powerFromGrid : 0;
    final garageW = situation.insideCurrentChargingPower;
    final outsideW = situation.outsideCurrentChargingPower;

    // Material 3 friendly palette – readable on both light and dark backgrounds
    const pvColor = Color(0xFFF59E0B);       // amber – solar
    const batColor = Color(0xFF3B82F6);      // blue – battery
    const gridInColor = Color(0xFFEF4444);   // red – grid import
    const houseColor = Color(0xFF6366F1);    // indigo – house consumption
    const batChargeColor = Color(0xFF60A5FA); // light-blue – charging battery
    const gridFeedColor = Color(0xFFF97316); // orange – grid feed-in
    const garageColor = Color(0xFF14B8A6);   // teal – garage charger
    const outsideColor = Color(0xFF8B5CF6);  // purple – outside charger

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.bolt,
                    size: 18,
                    color: Theme.of(context).colorScheme.primary),
                const SizedBox(width: 6),
                const Text('Energieverteilung',
                    style: TextStyle(fontWeight: FontWeight.bold)),
              ],
            ),
            const SizedBox(height: 12),
            _BarRow(label: 'Quellen'),
            _PowerBar(segments: [
              _Segment('PV', pvW, pvColor),
              _Segment('Batterie', batW, batColor),
              _Segment('Netz', gridInW, gridInColor),
            ]),
            const SizedBox(height: 8),
            _BarRow(label: 'Verbrauch'),
            _PowerBar(segments: [
              _Segment('Haus', houseW, houseColor),
              _Segment('Akku laden', batChargeW, batChargeColor),
              _Segment('Einspeisung', gridFeedW, gridFeedColor),
              _Segment('Garage', garageW, garageColor),
              _Segment('Außen', outsideW, outsideColor),
            ]),
            const SizedBox(height: 12),
            _Legend(segments: [
              _Segment('PV', pvW, pvColor),
              _Segment('Batterie', batW, batColor),
              _Segment('Netz', gridInW, gridInColor),
              _Segment('Haus', houseW, houseColor),
              _Segment('Garage', garageW, garageColor),
              _Segment('Außen', outsideW, outsideColor),
            ]),
          ],
        ),
      ),
    );
  }
}

class _Segment {
  const _Segment(this.label, this.watts, this.color);
  final String label;
  final int watts;
  final Color color;
}

class _BarRow extends StatelessWidget {
  const _BarRow({required this.label});
  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Text(label,
          style: Theme.of(context).textTheme.labelSmall),
    );
  }
}

class _PowerBar extends StatelessWidget {
  const _PowerBar({required this.segments});
  final List<_Segment> segments;

  static const double _maxPowerW = 15000;

  @override
  Widget build(BuildContext context) {
    final totalFlex = segments.fold<int>(
      0,
      (sum, s) => sum + ((s.watts.abs() / _maxPowerW) * 1000).round(),
    );
    final emptyFlex = (1000 - totalFlex).clamp(0, 1000);

    return ClipRRect(
      borderRadius: BorderRadius.circular(6),
      child: Container(
        height: 22,
        color: Theme.of(context).colorScheme.surfaceContainerHighest,
        child: Row(
          children: [
            ...segments.map((s) {
              final flex = ((s.watts.abs() / _maxPowerW) * 1000).round();
              if (flex == 0) return const SizedBox.shrink();
              return Expanded(flex: flex, child: Container(color: s.color));
            }),
            if (emptyFlex > 0) Expanded(flex: emptyFlex, child: const SizedBox()),
          ],
        ),
      ),
    );
  }
}

class _Legend extends StatelessWidget {
  const _Legend({required this.segments});
  final List<_Segment> segments;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 12,
      runSpacing: 4,
      children: segments.map((s) {
        return Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
                width: 12,
                height: 12,
                decoration: BoxDecoration(
                    color: s.color,
                    borderRadius: BorderRadius.circular(2))),
            const SizedBox(width: 4),
            Text('${s.label} ${(s.watts / 1000).toStringAsFixed(1)} kW',
                style: Theme.of(context).textTheme.labelSmall),
          ],
        );
      }).toList(),
    );
  }
}
