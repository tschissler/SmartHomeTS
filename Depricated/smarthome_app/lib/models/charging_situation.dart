class ChargingSituation {
  final bool insideConnected;
  final bool outsideConnected;
  final int houseConsumptionPower;
  final int insideCurrentChargingPower;
  final int outsideCurrentChargingPower;
  final int powerFromPV;
  final int powerFromGrid;
  final int powerFromBattery;
  final int batteryLevel;
  final int insideChargingLatestmA;
  final int outsideChargingLatestmA;
  final double insideChargingCurrentSessionWh;
  final double outsideChargingCurrentSessionWh;

  const ChargingSituation({
    this.insideConnected = false,
    this.outsideConnected = false,
    this.houseConsumptionPower = 0,
    this.insideCurrentChargingPower = 0,
    this.outsideCurrentChargingPower = 0,
    this.powerFromPV = 0,
    this.powerFromGrid = 0,
    this.powerFromBattery = 0,
    this.batteryLevel = 0,
    this.insideChargingLatestmA = -1,
    this.outsideChargingLatestmA = -1,
    this.insideChargingCurrentSessionWh = 0,
    this.outsideChargingCurrentSessionWh = 0,
  });

  factory ChargingSituation.fromJson(Map<String, dynamic> json) {
    return ChargingSituation(
      insideConnected: json['InsideConnected'] as bool? ?? false,
      outsideConnected: json['OutsideConnected'] as bool? ?? false,
      houseConsumptionPower: json['HouseConsumptionPower'] as int? ?? 0,
      insideCurrentChargingPower:
          json['InsideCurrentChargingPower'] as int? ?? 0,
      outsideCurrentChargingPower:
          json['OutsideCurrentChargingPower'] as int? ?? 0,
      powerFromPV: json['PowerFromPV'] as int? ?? 0,
      powerFromGrid: json['PowerFromGrid'] as int? ?? 0,
      powerFromBattery: json['PowerFromBattery'] as int? ?? 0,
      batteryLevel: json['BatteryLevel'] as int? ?? 0,
      insideChargingLatestmA: json['InsideChargingLatestmA'] as int? ?? -1,
      outsideChargingLatestmA: json['OutsideChargingLatestmA'] as int? ?? -1,
      insideChargingCurrentSessionWh:
          (json['InsideChargingCurrentSessionWh'] as num?)?.toDouble() ?? 0,
      outsideChargingCurrentSessionWh:
          (json['OutsideChargingCurrentSessionWh'] as num?)?.toDouble() ?? 0,
    );
  }
}
