import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../providers/heating_provider.dart';
import 'widgets/heating_room_card.dart';

class HeatingScreen extends ConsumerWidget {
  const HeatingScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(heatingProvider);
    final notifier = ref.read(heatingProvider.notifier);

    return Scaffold(
      body: ListView(
        padding: const EdgeInsets.all(12),
        children: [
          HeatingRoomCard(
            roomName: 'Kinderzimmer M1',
            command: state.kinderzimmer,
            onModeSelected: notifier.setKinderzimmerMode,
          ),
          const SizedBox(height: 8),
          HeatingRoomCard(
            roomName: 'Esszimmer M3',
            command: state.esszimmer,
            onModeSelected: notifier.setEsszimmerMode,
          ),
        ],
      ),
    );
  }
}
