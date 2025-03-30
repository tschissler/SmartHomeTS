import 'package:flutter/material.dart';

class ScalableWidget extends StatelessWidget {
  final List<Widget> widgets;

  const ScalableWidget({super.key, required this.widgets});

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(builder: (context, constraints) {
      double availableWidth = (constraints.maxWidth-16) / widgets.length;
      double minWidth = 250;
      return Wrap(
        spacing: 8.0,
        runSpacing: 8.0,
        children: widgets.map((widget) {
          return ConstrainedBox(
            constraints: BoxConstraints(minWidth: minWidth, maxWidth: availableWidth > minWidth ? availableWidth : minWidth),
            child: widget,
          );
        }).toList(),
      );
    });
  }
}
