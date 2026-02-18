const List<String> heatingDisplayModes = [
  'Aus',
  '25%',
  '50%',
  '75%',
  '100%',
  'Auto Low',
  'Auto High',
  'Auto',
];

class HeatingCommand {
  final String mode;
  final int fanspeed;

  const HeatingCommand({this.mode = 'Auto', this.fanspeed = 50});

  factory HeatingCommand.fromJson(Map<String, dynamic> json) {
    return HeatingCommand(
      mode: json['Mode'] as String? ?? 'Auto',
      fanspeed: json['Fanspeed'] as int? ?? 50,
    );
  }

  Map<String, dynamic> toJson() => {
        'Mode': mode,
        'Fanspeed': fanspeed,
      };

  String get displayMode {
    if (mode == 'Manual') {
      return switch (fanspeed) {
        0 => 'Aus',
        25 => '25%',
        50 => '50%',
        75 => '75%',
        100 => '100%',
        _ => '$fanspeed%',
      };
    }
    return switch (mode) {
      'AutoLow' => 'Auto Low',
      'AutoHigh' => 'Auto High',
      _ => 'Auto',
    };
  }

  static HeatingCommand fromDisplayMode(String displayMode) =>
      switch (displayMode) {
        'Aus' => const HeatingCommand(mode: 'Manual', fanspeed: 0),
        '25%' => const HeatingCommand(mode: 'Manual', fanspeed: 25),
        '50%' => const HeatingCommand(mode: 'Manual', fanspeed: 50),
        '75%' => const HeatingCommand(mode: 'Manual', fanspeed: 75),
        '100%' => const HeatingCommand(mode: 'Manual', fanspeed: 100),
        'Auto Low' => const HeatingCommand(mode: 'AutoLow', fanspeed: 50),
        'Auto High' => const HeatingCommand(mode: 'AutoHigh', fanspeed: 100),
        _ => const HeatingCommand(mode: 'Auto', fanspeed: 50),
      };
}
