enum ChargingStation { none, inside, outside }

class ChargingSettings {
  final int chargingLevel;
  final ChargingStation preferedChargingStation;
  final bool insideChargingEnabled;
  final bool outsideChargingEnabled;

  const ChargingSettings({
    this.chargingLevel = 0,
    this.preferedChargingStation = ChargingStation.none,
    this.insideChargingEnabled = true,
    this.outsideChargingEnabled = true,
  });

  factory ChargingSettings.fromJson(Map<String, dynamic> json) {
    final stationIndex = json['PreferedChargingStation'] as int? ?? 0;
    return ChargingSettings(
      chargingLevel: json['ChargingLevel'] as int? ?? 0,
      preferedChargingStation: stationIndex < ChargingStation.values.length
          ? ChargingStation.values[stationIndex]
          : ChargingStation.none,
      insideChargingEnabled: json['InsideChargingEnabled'] as bool? ?? true,
      outsideChargingEnabled: json['OutsideChargingEnabled'] as bool? ?? true,
    );
  }

  Map<String, dynamic> toJson() => {
        'ChargingLevel': chargingLevel,
        'PreferedChargingStation': preferedChargingStation.index,
        'InsideChargingEnabled': insideChargingEnabled,
        'OutsideChargingEnabled': outsideChargingEnabled,
      };

  ChargingSettings copyWith({
    int? chargingLevel,
    ChargingStation? preferedChargingStation,
    bool? insideChargingEnabled,
    bool? outsideChargingEnabled,
  }) {
    return ChargingSettings(
      chargingLevel: chargingLevel ?? this.chargingLevel,
      preferedChargingStation:
          preferedChargingStation ?? this.preferedChargingStation,
      insideChargingEnabled:
          insideChargingEnabled ?? this.insideChargingEnabled,
      outsideChargingEnabled:
          outsideChargingEnabled ?? this.outsideChargingEnabled,
    );
  }
}
