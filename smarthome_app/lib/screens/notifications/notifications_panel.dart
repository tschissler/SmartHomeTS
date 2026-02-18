import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../models/notification_message.dart';
import '../../providers/notifications_provider.dart';

/// Modal bottom sheet listing all active (unacknowledged) notifications.
class NotificationsPanel extends ConsumerWidget {
  const NotificationsPanel({super.key});

  static final _timeFmt = DateFormat('dd.MM HH:mm');

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(notificationsProvider);
    final notifier = ref.read(notificationsProvider.notifier);
    final colorScheme = Theme.of(context).colorScheme;

    return DraggableScrollableSheet(
      initialChildSize: 0.5,
      minChildSize: 0.3,
      maxChildSize: 0.9,
      expand: false,
      builder: (context, scrollController) {
        return Column(
          children: [
            // Handle + header
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
              child: Row(
                children: [
                  Icon(Icons.notifications_outlined,
                      color: colorScheme.primary),
                  const SizedBox(width: 8),
                  Text('Meldungen',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.bold)),
                  const Spacer(),
                  if (state.active.isNotEmpty)
                    TextButton(
                      onPressed: () async {
                        for (final msg in List.of(state.active)) {
                          await notifier.acknowledge(msg);
                        }
                      },
                      child: const Text('Alle bestätigen'),
                    ),
                  IconButton(
                    icon: const Icon(Icons.close),
                    onPressed: () => Navigator.of(context).pop(),
                    visualDensity: VisualDensity.compact,
                  ),
                ],
              ),
            ),
            const Divider(height: 1),

            // Content
            Expanded(
              child: state.active.isEmpty
                  ? _EmptyState()
                  : ListView.separated(
                      controller: scrollController,
                      padding: const EdgeInsets.symmetric(
                          horizontal: 12, vertical: 8),
                      itemCount: state.active.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        final msg = state.active[index];
                        return _NotificationTile(
                          msg: msg,
                          onAcknowledge: () => notifier.acknowledge(msg),
                        );
                      },
                    ),
            ),
          ],
        );
      },
    );
  }
}

class _NotificationTile extends StatelessWidget {
  const _NotificationTile({
    required this.msg,
    required this.onAcknowledge,
  });

  final NotificationMessage msg;
  final VoidCallback onAcknowledge;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Coloured left indicator bar
            Container(
              width: 5,
              decoration: BoxDecoration(
                color: msg.color,
                borderRadius: const BorderRadius.only(
                  topLeft: Radius.circular(12),
                  bottomLeft: Radius.circular(12),
                ),
              ),
            ),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 12, 12, 10),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Header row: icon + title + timestamp
                    Row(
                      children: [
                        Icon(msg.icon, size: 18, color: msg.color),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            msg.title,
                            style: const TextStyle(fontWeight: FontWeight.bold),
                          ),
                        ),
                        Text(
                          NotificationsPanel._timeFmt
                              .format(msg.receivedAt.toLocal()),
                          style: Theme.of(context)
                              .textTheme
                              .labelSmall
                              ?.copyWith(
                                  color: Theme.of(context)
                                      .colorScheme
                                      .outline),
                        ),
                      ],
                    ),
                    // Type badge
                    Padding(
                      padding: const EdgeInsets.only(top: 2, bottom: 6),
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 7, vertical: 2),
                        decoration: BoxDecoration(
                          color: msg.color.withAlpha(31),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          msg.typeLabel,
                          style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.bold,
                              color: msg.color),
                        ),
                      ),
                    ),
                    // Message text
                    Text(msg.text,
                        style: Theme.of(context).textTheme.bodyMedium),
                    const SizedBox(height: 10),
                    // Acknowledge button
                    Align(
                      alignment: Alignment.centerRight,
                      child: FilledButton.tonal(
                        onPressed: onAcknowledge,
                        style: FilledButton.styleFrom(
                          minimumSize: const Size(0, 32),
                          padding: const EdgeInsets.symmetric(
                              horizontal: 16, vertical: 0),
                          visualDensity: VisualDensity.compact,
                        ),
                        child: const Text('Bestätigen'),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.check_circle_outline,
              size: 56,
              color: Theme.of(context).colorScheme.primary.withAlpha(153)),
          const SizedBox(height: 12),
          Text('Keine aktiven Meldungen',
              style: Theme.of(context).textTheme.bodyLarge),
        ],
      ),
    );
  }
}
