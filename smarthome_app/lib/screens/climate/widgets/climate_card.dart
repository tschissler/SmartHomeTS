import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../models/climate_data.dart';

class ClimateCard extends StatelessWidget {
  const ClimateCard({
    super.key,
    required this.title,
    required this.primaryPoint,
    required this.primaryUnit,
    this.icon,
    this.secondaryPoint,
    this.secondaryUnit,
    this.secondaryLabel,
  });

  final String title;
  final DataPoint primaryPoint;
  final String primaryUnit;
  final IconData? icon;
  final DataPoint? secondaryPoint;
  final String? secondaryUnit;
  final String? secondaryLabel;

  static final _timeFmt = DateFormat('dd.MM HH:mm');

  bool get _hasData =>
      primaryPoint.lastUpdated.millisecondsSinceEpoch > 0;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(10),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                if (icon != null) ...[
                  Icon(icon, size: 16, color: colorScheme.primary),
                  const SizedBox(width: 5),
                ],
                Expanded(
                  child: Text(title,
                      style: TextStyle(
                          fontWeight: FontWeight.bold,
                          color: colorScheme.primary)),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              crossAxisAlignment: CrossAxisAlignment.baseline,
              textBaseline: TextBaseline.alphabetic,
              children: [
                Text(
                  _hasData
                      ? primaryPoint.value.toStringAsFixed(1)
                      : '–',
                  style: Theme.of(context)
                      .textTheme
                      .headlineSmall
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
                const SizedBox(width: 4),
                Text(primaryUnit,
                    style: Theme.of(context).textTheme.bodyMedium),
              ],
            ),
            if (secondaryPoint != null) ...[
              const SizedBox(height: 4),
              Row(
                children: [
                  if (secondaryLabel != null)
                    Text('${secondaryLabel!}: ',
                        style: Theme.of(context).textTheme.bodySmall),
                  Text(
                    _hasData
                        ? secondaryPoint!.value.toStringAsFixed(1)
                        : '–',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                  Text(' ${secondaryUnit ?? ''}',
                      style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            ],
            const SizedBox(height: 6),
            Text(
              _hasData
                  ? _timeFmt.format(primaryPoint.lastUpdated.toLocal())
                  : 'Keine Daten',
              style: Theme.of(context)
                  .textTheme
                  .labelSmall
                  ?.copyWith(color: colorScheme.outline),
            ),
          ],
        ),
      ),
    );
  }
}
