import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../models/car_status.dart';
import '../../../models/car_type.dart';

class CarStatusWidget extends StatelessWidget {
  const CarStatusWidget({
    super.key,
    required this.carType,
    required this.status,
  });

  final CarType carType;
  final CarStatus status;

  String get _imagePath => switch (carType) {
        CarType.bmw => 'assets/images/BMW.webp',
        CarType.mini => 'assets/images/Mini.png',
        CarType.vw => 'assets/images/ID4.png',
      };

  String get _label => switch (carType) {
        CarType.bmw => 'BMW',
        CarType.mini => 'Mini',
        CarType.vw => 'VW ID.4',
      };

  @override
  Widget build(BuildContext context) {
    final batteryColor = status.battery > 50
        ? Colors.green
        : status.battery > 20
            ? Colors.orange
            : Colors.red;
    final fmt = DateFormat('dd.MM HH:mm');

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Header: image + name
            Row(
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(4),
                  child: Image.asset(
                    _imagePath,
                    width: 56,
                    height: 40,
                    fit: BoxFit.cover,
                    errorBuilder: (_, __, ___) => const Icon(
                        Icons.directions_car,
                        size: 40),
                  ),
                ),
                const SizedBox(width: 10),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(_label,
                        style: const TextStyle(fontWeight: FontWeight.bold)),
                    if (status.name.isNotEmpty)
                      Text(status.name,
                          style: Theme.of(context).textTheme.bodySmall),
                  ],
                ),
                const Spacer(),
                if (status.chargerConnected)
                  const Icon(Icons.power, color: Colors.green, size: 18),
              ],
            ),

            const SizedBox(height: 10),

            // Battery bar
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('Akku', style: Theme.of(context).textTheme.labelSmall),
                Text('${status.battery.round()}%',
                    style: TextStyle(
                        color: batteryColor, fontWeight: FontWeight.bold)),
              ],
            ),
            const SizedBox(height: 4),
            LinearProgressIndicator(
              value: status.battery / 100.0,
              minHeight: 8,
              backgroundColor:
                  Theme.of(context).colorScheme.surfaceContainerHighest,
              valueColor: AlwaysStoppedAnimation<Color>(batteryColor),
              borderRadius: BorderRadius.circular(4),
            ),

            const SizedBox(height: 8),

            // Details row
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                _InfoChip(
                    icon: Icons.battery_charging_full,
                    label: 'Ziel ${status.chargingTarget.round()}%'),
                _InfoChip(
                    icon: Icons.route,
                    label: '${status.remainingRange.round()} km'),
              ],
            ),

            if (status.chargingEndTime != null &&
                status.chargingEndTime!.year > 2000) ...[
              const SizedBox(height: 4),
              _InfoChip(
                icon: Icons.schedule,
                label:
                    'Ende ${fmt.format(status.chargingEndTime!.toLocal())}',
              ),
            ],

            if (status.lastUpdate != null) ...[
              const SizedBox(height: 4),
              Text(
                'Aktualisiert: ${fmt.format(status.lastUpdate!.toLocal())}',
                style: Theme.of(context).textTheme.labelSmall,
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _InfoChip extends StatelessWidget {
  const _InfoChip({required this.icon, required this.label});
  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 14, color: Theme.of(context).colorScheme.primary),
        const SizedBox(width: 4),
        Text(label, style: Theme.of(context).textTheme.bodySmall),
      ],
    );
  }
}
