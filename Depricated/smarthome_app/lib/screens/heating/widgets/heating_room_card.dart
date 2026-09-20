import 'package:flutter/material.dart';

import '../../../models/heating_command.dart';

class HeatingRoomCard extends StatelessWidget {
  const HeatingRoomCard({
    super.key,
    required this.roomName,
    required this.command,
    required this.onModeSelected,
  });

  final String roomName;
  final HeatingCommand command;
  final ValueChanged<String> onModeSelected;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    final isOff = command.displayMode == 'Aus';
    final isAuto = command.displayMode.startsWith('Auto');

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Header
            Row(
              children: [
                Icon(
                  isAuto
                      ? Icons.heat_pump_outlined
                      : isOff
                          ? Icons.air_outlined
                          : Icons.air,
                  size: 20,
                  color: isOff
                      ? colorScheme.outline
                      : colorScheme.primary,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(roomName,
                          style: const TextStyle(fontWeight: FontWeight.bold)),
                      Text('Modus: ${command.displayMode}',
                          style: Theme.of(context)
                              .textTheme
                              .bodySmall
                              ?.copyWith(color: colorScheme.onSurfaceVariant)),
                    ],
                  ),
                ),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: isOff
                        ? colorScheme.surfaceContainerHighest
                        : colorScheme.primaryContainer,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.air,
                          size: 12,
                          color: isOff
                              ? colorScheme.outline
                              : colorScheme.onPrimaryContainer),
                      const SizedBox(width: 3),
                      Text(
                        '${command.fanspeed}%',
                        style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                            color: isOff
                                ? colorScheme.outline
                                : colorScheme.onPrimaryContainer),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),

            // Mode buttons – split into two rows for readability
            _ModeButtonRow(
              modes: const ['Aus', '25%', '50%', '75%', '100%'],
              selected: command.displayMode,
              onSelected: onModeSelected,
            ),
            const SizedBox(height: 6),
            _ModeButtonRow(
              modes: const ['Auto Low', 'Auto High', 'Auto'],
              selected: command.displayMode,
              onSelected: onModeSelected,
            ),
          ],
        ),
      ),
    );
  }
}

class _ModeButtonRow extends StatelessWidget {
  const _ModeButtonRow({
    required this.modes,
    required this.selected,
    required this.onSelected,
  });

  final List<String> modes;
  final String selected;
  final ValueChanged<String> onSelected;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: modes.map((mode) {
        final isActive = mode == selected;
        return Expanded(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 2),
            child: FilledButton(
              style: FilledButton.styleFrom(
                backgroundColor: isActive
                    ? Theme.of(context).colorScheme.primary
                    : Theme.of(context).colorScheme.surfaceContainerHighest,
                foregroundColor: isActive
                    ? Theme.of(context).colorScheme.onPrimary
                    : Theme.of(context).colorScheme.onSurface,
                padding: const EdgeInsets.symmetric(vertical: 8),
                minimumSize: const Size(0, 36),
                shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(6)),
              ),
              onPressed: () => onSelected(mode),
              child: Text(mode,
                  style: const TextStyle(fontSize: 11),
                  textAlign: TextAlign.center),
            ),
          ),
        );
      }).toList(),
    );
  }
}
