import 'package:flutter/material.dart';

class ChargingLevelSelector extends StatefulWidget {
  const ChargingLevelSelector({super.key});

  @override
  State<ChargingLevelSelector> createState() => _ChargingLevelSelectorState();
}

class _ChargingLevelSelectorState extends State<ChargingLevelSelector> {
  int selectedLevel = 3;

  void _selectLevel(int level) {
    setState(() {
      selectedLevel = level;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            _buildButton(0, 'Aus'),
            _buildButton(1, 'PV-Laden mit Batterie-Prio'),
            _buildButton(2, 'PV-Laden mit Auto-Prio'),
            _buildButton(3, 'PV + Batterie-Laden'),
            _buildButton(4, 'Sofort-Laden'),
            _buildButton(5, 'Schnell-Laden'),
          ],
        ),
        SizedBox(height: 20),
        Container(
          height: 4,
          width: double.infinity,
          decoration: BoxDecoration(
            gradient: LinearGradient(
              colors: [
                Colors.blue,
                Colors.blue,
                Colors.blue,
                Colors.blue,
                Colors.grey,
                Colors.grey,
              ],
              stops: [
                0.0,
                (selectedLevel + 1) / 6,
                (selectedLevel + 1) / 6,
                1.0,
                1.0,
                1.0,
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildButton(int level, String label) {
    bool isSelected = level <= selectedLevel;
    return GestureDetector(
      onTap: () => _selectLevel(level),
      child: Column(
        children: [
          Container(
            width: 30,
            height: 30,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: isSelected ? Colors.blue : Colors.grey,
            ),
            child: Center(
              child: Text(
                level.toString(),
                style: TextStyle(color: Colors.white, fontSize: 12),
              ),
            ),
          ),
          SizedBox(height: 8),
          //Text(label),
        ],
      ),
    );
  }
}