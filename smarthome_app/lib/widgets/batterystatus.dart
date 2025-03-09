import 'package:flutter/material.dart';

class BatteryStatusWidget extends StatelessWidget {
  final double batteryPercentage;

  const BatteryStatusWidget({super.key, required this.batteryPercentage});

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.start,
        children: [
          Text(
            'Batterie Ladestand: ${batteryPercentage.toInt()}%',
            style: TextStyle(fontSize: 12),
          ),
          LinearProgressIndicator(
            minHeight: 15.0,
            value: batteryPercentage / 100,
            borderRadius: BorderRadius.all(Radius.circular(3)),
            backgroundColor: Colors.grey,
            valueColor: AlwaysStoppedAnimation<Color>(Color.fromARGB(255, 78, 135, 82)),
          ),
        ],
      ),
    );
  }
}
