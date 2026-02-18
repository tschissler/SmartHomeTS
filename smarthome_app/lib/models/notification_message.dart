import 'package:flutter/material.dart';

enum NotificationType { hinweis, warnung, fehler, kritisch }

class NotificationMessage {
  const NotificationMessage({
    required this.topic,
    required this.type,
    required this.text,
    this.bestaetigt = false,
    required this.receivedAt,
  });

  final String topic;
  final NotificationType type;
  final String text;
  final bool bestaetigt;
  final DateTime receivedAt;

  /// Short display name derived from the last path segment of the topic.
  String get title => topic.contains('/') ? topic.split('/').last : topic;

  /// Stable integer ID for the local notification (safe positive int).
  int get notificationId => topic.hashCode.abs() % 100000;

  Color get color => switch (type) {
        NotificationType.hinweis => const Color(0xFF3B82F6),   // blue
        NotificationType.warnung => const Color(0xFFF97316),   // orange
        NotificationType.fehler => const Color(0xFFEF4444),    // red
        NotificationType.kritisch => const Color(0xFF7F1D1D),  // dark-red
      };

  IconData get icon => switch (type) {
        NotificationType.hinweis => Icons.info_outline,
        NotificationType.warnung => Icons.warning_amber_outlined,
        NotificationType.fehler => Icons.error_outline,
        NotificationType.kritisch => Icons.crisis_alert,
      };

  String get typeLabel => switch (type) {
        NotificationType.hinweis => 'Hinweis',
        NotificationType.warnung => 'Warnung',
        NotificationType.fehler => 'Fehler',
        NotificationType.kritisch => 'Kritisch',
      };

  factory NotificationMessage.fromJson(String topic, Map<String, dynamic> json) {
    final typeStr = (json['Type'] as String? ?? '').toLowerCase();
    final type = switch (typeStr) {
      'warnung' => NotificationType.warnung,
      'fehler' => NotificationType.fehler,
      'kritisch' => NotificationType.kritisch,
      _ => NotificationType.hinweis,
    };
    return NotificationMessage(
      topic: topic,
      type: type,
      text: json['Text'] as String? ?? '',
      bestaetigt: json['Bestaetigt'] as bool? ?? false,
      receivedAt: DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() => {
        'Type': typeLabel,
        'Text': text,
        'Bestaetigt': bestaetigt,
      };

  NotificationMessage copyWith({bool? bestaetigt}) => NotificationMessage(
        topic: topic,
        type: type,
        text: text,
        bestaetigt: bestaetigt ?? this.bestaetigt,
        receivedAt: receivedAt,
      );
}
