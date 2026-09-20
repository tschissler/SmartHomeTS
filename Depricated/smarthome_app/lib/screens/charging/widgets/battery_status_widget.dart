import 'package:flutter/material.dart';

class BatteryStatusWidget extends StatelessWidget {
  const BatteryStatusWidget({super.key, required this.batteryLevel});

  final int batteryLevel;

  @override
  Widget build(BuildContext context) {
    final color = batteryLevel > 50
        ? Colors.green
        : batteryLevel > 20
            ? Colors.orange
            : Colors.red;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.home_outlined,
                    size: 18,
                    color: Theme.of(context).colorScheme.primary),
                const SizedBox(width: 6),
                const Expanded(
                  child: Text('Hausbatterie',
                      style: TextStyle(fontWeight: FontWeight.bold)),
                ),
                Text('$batteryLevel%',
                    style: TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 16,
                        color: color)),
              ],
            ),
            const SizedBox(height: 10),
            LinearProgressIndicator(
              value: batteryLevel / 100.0,
              minHeight: 14,
              backgroundColor:
                  Theme.of(context).colorScheme.surfaceContainerHighest,
              valueColor: AlwaysStoppedAnimation<Color>(color),
              borderRadius: BorderRadius.circular(7),
            ),
          ],
        ),
      ),
    );
  }
}
