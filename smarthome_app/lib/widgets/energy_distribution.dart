import 'package:flutter/material.dart';

class EnergyDistributionWidget extends StatelessWidget {
  static const pvColor = Color.fromARGB(255, 13, 202, 240);
  static const batteryColor = Color.fromARGB(255, 173, 255, 47);
  static const gridColor = Color.fromARGB(255, 247, 37, 133);
  static const houseColor = Color.fromARGB(255, 218, 165, 32);
  static const garageColor = Color.fromARGB(255, 97, 70, 128);
  static const outsideColor = Color.fromARGB(255, 190, 140, 229);

  final double pvPower;
  final double batteryPower;
  final double gridPower;
  final double housePower;
  final double garagePower;
  final double outsidePower;

  const EnergyDistributionWidget({
    super.key,
    required this.pvPower,
    required this.batteryPower,
    required this.gridPower,
    required this.housePower,
    required this.garagePower,
    required this.outsidePower,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Wrap(
          spacing: 5,
          runSpacing: 5,
          children: [
            _buildItem('PV:', pvPower, pvColor),
            _buildItem('Batterie:', batteryPower, batteryColor),
            _buildItem('Netz:', gridPower, gridColor),
            _buildItem('Haus:', housePower, houseColor),
            _buildItem('Garage:', garagePower, garageColor),
            _buildItem('Außen:', outsidePower, outsideColor),
          ],
        ),
        SizedBox(height: 10),
        Row(children: [
          Expanded(
            flex: pvPower.toInt(),
            child: Container(
              height: 10,
              color: pvColor,
            ),
          ),
          if (batteryPower > 0)
            Expanded(
              flex: batteryPower.toInt(),
              child: Container(
                height: 10,
                color: batteryColor,
              ),
            ),
          if (gridPower > 0)
            Expanded(
              flex: gridPower.toInt(),
              child: Container(
                height: 10,
                color: gridColor,
              ),
            ),
          Expanded(
            flex: (10000 -
                    pvPower -
                    (batteryPower > 0 ? batteryPower : 0) -
                    (gridPower > 0 ? gridPower : 0))
                .toInt(),
            child: Container(
              height: 10,
              color: Colors.transparent,
            ),
          ),
        ]),
        SizedBox(height: 10),
        Row(
          children: [
            Expanded(
              flex: housePower.toInt(),
              child: Container(
                height: 10,
                color: houseColor,
              ),
            ),
            if (batteryPower < 0)
              Expanded(
                flex: batteryPower.toInt() * -1,
                child: Container(
                  height: 10,
                  color: batteryColor,
                ),
              ),
            if (gridPower < 0)
              Expanded(
                flex: gridPower.toInt() * -1,
                child: Container(
                  height: 10,
                  color: gridColor,
                ),
              ),
            Expanded(
              flex: garagePower.toInt(),
              child: Container(
                height: 10,
                color: garageColor,
              ),
            ),
            Expanded(
              flex: outsidePower.toInt(),
              child: Container(
                height: 10,
                color: outsideColor,
              ),
            ),
            Expanded(
              flex: (10000 +
                      (batteryPower < 0 ? batteryPower : 0) +
                      (gridPower < 0 ? gridPower : 0) -
                      housePower -
                      garagePower -
                      outsidePower)
                  .toInt(),
              child: Container(
                height: 10,
                color: Colors.transparent,
              ),
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildItem(String label, double value, Color color) {
    return SizedBox(
      width: 120,
      child: Row(
        children: [
          Container(
            padding: EdgeInsets.symmetric(vertical: 8.0, horizontal: 8.0),
            decoration: BoxDecoration(
              color: color,
            ),
          ),
          SizedBox(width: 3),
          Expanded(
            child: Text(
              label,
              style: TextStyle(fontSize: 11),
              textAlign: TextAlign.left,
            ),
          ),
          Expanded(
            child: Text(
              '${value.toStringAsFixed(0)}W',
              style: TextStyle(fontSize: 11),
              textAlign: TextAlign.left,
            ),
          ),
          SizedBox(width: 6),
        ],
      ),
    );
  }
}
