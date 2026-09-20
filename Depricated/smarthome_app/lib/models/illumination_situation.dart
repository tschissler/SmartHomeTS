class IlluminationSettings {
  final int red;
  final int green;
  final int blue;
  final int density;
  final bool on;

  const IlluminationSettings({
    this.red = 255,
    this.green = 128,
    this.blue = 0,
    this.density = 10,
    this.on = true,
  });

  factory IlluminationSettings.fromJson(Map<String, dynamic> json) {
    return IlluminationSettings(
      red: json['Red'] as int? ?? 0,
      green: json['Green'] as int? ?? 0,
      blue: json['Blue'] as int? ?? 0,
      density: json['Density'] as int? ?? 0,
      on: json['On'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() => {
        'Red': red,
        'Green': green,
        'Blue': blue,
        'Density': density,
        'On': on,
      };

  IlluminationSettings copyWith({
    int? red,
    int? green,
    int? blue,
    int? density,
    bool? on,
  }) {
    return IlluminationSettings(
      red: red ?? this.red,
      green: green ?? this.green,
      blue: blue ?? this.blue,
      density: density ?? this.density,
      on: on ?? this.on,
    );
  }
}

class IlluminationSituation {
  final IlluminationSettings left;
  final IlluminationSettings right;
  final bool lampOn;

  const IlluminationSituation({
    this.left = const IlluminationSettings(),
    this.right = const IlluminationSettings(),
    this.lampOn = false,
  });

  factory IlluminationSituation.fromJson(Map<String, dynamic> json) {
    return IlluminationSituation(
      left: IlluminationSettings.fromJson(
          json['Left'] as Map<String, dynamic>? ?? {}),
      right: IlluminationSettings.fromJson(
          json['Right'] as Map<String, dynamic>? ?? {}),
      lampOn: json['LampOn'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() => {
        'Left': left.toJson(),
        'Right': right.toJson(),
        'LampOn': lampOn,
      };

  IlluminationSituation copyWith({
    IlluminationSettings? left,
    IlluminationSettings? right,
    bool? lampOn,
  }) {
    return IlluminationSituation(
      left: left ?? this.left,
      right: right ?? this.right,
      lampOn: lampOn ?? this.lampOn,
    );
  }
}
