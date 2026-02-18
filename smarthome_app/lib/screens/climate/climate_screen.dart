import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../providers/climate_provider.dart';
import 'widgets/climate_card.dart';

class ClimateScreen extends ConsumerWidget {
  const ClimateScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = ref.watch(climateProvider);
    final width = MediaQuery.sizeOf(context).width;
    final columns = width > 900 ? 3 : width > 600 ? 2 : 2;

    return Scaffold(
      body: Padding(
        padding: const EdgeInsets.all(12),
        child: GridView.count(
          crossAxisCount: columns,
          crossAxisSpacing: 8,
          mainAxisSpacing: 8,
          childAspectRatio: 1.2,
          children: [
            ClimateCard(
              title: 'Außen',
              icon: Icons.wb_sunny_outlined,
              primaryPoint: data.outsideTemperature,
              primaryUnit: '°C',
              secondaryPoint: data.outsideHumidity,
              secondaryUnit: '%',
              secondaryLabel: 'Feuchte',
            ),
            ClimateCard(
              title: 'Wohnzimmer',
              icon: Icons.weekend_outlined,
              primaryPoint: data.livingRoomTemperature,
              primaryUnit: '°C',
              secondaryPoint: data.livingRoomHumidity,
              secondaryUnit: '%',
              secondaryLabel: 'Feuchte',
            ),
            ClimateCard(
              title: 'Schlafzimmer',
              icon: Icons.king_bed_outlined,
              primaryPoint: data.bedroomTemperature,
              primaryUnit: '°C',
              secondaryPoint: data.bedroomHumidity,
              secondaryUnit: '%',
              secondaryLabel: 'Feuchte',
            ),
            ClimateCard(
              title: 'Keller',
              icon: Icons.foundation_outlined,
              primaryPoint: data.basementTemperature,
              primaryUnit: '°C',
              secondaryPoint: data.basementHumidity,
              secondaryUnit: '%',
              secondaryLabel: 'Feuchte',
            ),
            ClimateCard(
              title: 'Bad M1',
              icon: Icons.bathtub_outlined,
              primaryPoint: data.bathRoomM1Temperature,
              primaryUnit: '°C',
              secondaryPoint: data.bathRoomM1Humidity,
              secondaryUnit: '%',
              secondaryLabel: 'Feuchte',
            ),
            ClimateCard(
              title: 'Kinderzimmer',
              icon: Icons.child_care_outlined,
              primaryPoint: data.childRoomTemperature,
              primaryUnit: '°C',
              secondaryPoint: data.childRoomHumidity,
              secondaryUnit: '%',
              secondaryLabel: 'Feuchte',
            ),
            ClimateCard(
              title: 'Zisterne',
              icon: Icons.water_outlined,
              primaryPoint: data.cisternFillLevel,
              primaryUnit: '%',
            ),
          ],
        ),
      ),
    );
  }
}
