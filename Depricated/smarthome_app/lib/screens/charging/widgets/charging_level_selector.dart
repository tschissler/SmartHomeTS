import 'package:flutter/material.dart';

const List<String> _levelDescriptions = [
  'Aus – kein Laden',
  'PV-Laden mit Batterie-Priorität (>3,8 kW Überschuss)',
  'PV-Laden mit Auto-Priorität (>Mindestleistung)',
  'PV + Batterie laden (falls Batterie >50%)',
  'Sofortladen mit Mindestleistung',
  'Schnellladen 8 kW',
];

class ChargingLevelSelector extends StatelessWidget {
  const ChargingLevelSelector({
    super.key,
    required this.selectedLevel,
    required this.onLevelSelected,
  });

  final int selectedLevel;
  final ValueChanged<int> onLevelSelected;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Ladestufe',
                style: TextStyle(fontWeight: FontWeight.bold)),
            const SizedBox(height: 4),
            Text(
              _levelDescriptions[selectedLevel.clamp(0, 5)],
              style: Theme.of(context).textTheme.bodySmall,
            ),
            const SizedBox(height: 12),
            Row(
              children: List.generate(6, (level) {
                final isSelected = level == selectedLevel;
                return Expanded(
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 2),
                    child: Tooltip(
                      message: _levelDescriptions[level],
                      child: FilledButton(
                        style: FilledButton.styleFrom(
                          backgroundColor: isSelected
                              ? Theme.of(context).colorScheme.primary
                              : Theme.of(context)
                                  .colorScheme
                                  .surfaceContainerHighest,
                          foregroundColor: isSelected
                              ? Theme.of(context).colorScheme.onPrimary
                              : Theme.of(context).colorScheme.onSurface,
                          padding: const EdgeInsets.symmetric(vertical: 12),
                          minimumSize: const Size(0, 44),
                          shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(8)),
                        ),
                        onPressed: () => onLevelSelected(level),
                        child: Text('$level',
                            style: const TextStyle(
                                fontSize: 16, fontWeight: FontWeight.bold)),
                      ),
                    ),
                  ),
                );
              }),
            ),
          ],
        ),
      ),
    );
  }
}
