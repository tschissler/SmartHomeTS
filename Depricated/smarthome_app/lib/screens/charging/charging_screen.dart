import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../models/car_status.dart';
import '../../models/car_type.dart';
import '../../providers/charging_provider.dart';
import 'widgets/battery_status_widget.dart';
import 'widgets/car_status_widget.dart';
import 'widgets/charging_level_selector.dart';
import 'widgets/energy_distribution_widget.dart';

class ChargingScreen extends ConsumerWidget {
  const ChargingScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(chargingProvider);
    final notifier = ref.read(chargingProvider.notifier);
    final wide = MediaQuery.sizeOf(context).width > 700;

    return Scaffold(
      body: ListView(
        padding: const EdgeInsets.all(12),
        children: [
          BatteryStatusWidget(batteryLevel: state.situation.batteryLevel),
          const SizedBox(height: 8),
          EnergyDistributionWidget(situation: state.situation),
          const SizedBox(height: 8),
          ChargingLevelSelector(
            selectedLevel: state.settings.chargingLevel,
            onLevelSelected: notifier.setChargingLevel,
          ),
          const SizedBox(height: 8),
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 4, vertical: 4),
            child: Text('Fahrzeuge',
                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
          ),
          // Car cards – adaptive grid for wider screens
          if (wide)
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                    child: CarStatusWidget(
                        carType: CarType.bmw,
                        status: state.bmwStatus ??
                            _placeholderStatus('BMW'))),
                const SizedBox(width: 8),
                Expanded(
                    child: CarStatusWidget(
                        carType: CarType.mini,
                        status: state.miniStatus ??
                            _placeholderStatus('Mini'))),
                const SizedBox(width: 8),
                Expanded(
                    child: CarStatusWidget(
                        carType: CarType.vw,
                        status: state.vwStatus ??
                            _placeholderStatus('VW ID.4'))),
              ],
            )
          else ...[
            CarStatusWidget(
                carType: CarType.bmw,
                status: state.bmwStatus ?? _placeholderStatus('BMW')),
            const SizedBox(height: 8),
            CarStatusWidget(
                carType: CarType.mini,
                status: state.miniStatus ?? _placeholderStatus('Mini')),
            const SizedBox(height: 8),
            CarStatusWidget(
                carType: CarType.vw,
                status: state.vwStatus ?? _placeholderStatus('VW ID.4')),
          ],
        ],
      ),
    );
  }
}

CarStatus _placeholderStatus(String name) =>
    CarStatus(name: name, brand: name);
