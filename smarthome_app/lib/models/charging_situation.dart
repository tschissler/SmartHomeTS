
class ChargingSituation {
  final bool insideConnected;
  final bool outsideConnected;
  final double houseConsumptionPower;
  final double insideCurrentChargingPower;
  final double outsideCurrentChargingPower;
  final double powerFromPV;
  final double powerFromGrid;
  final double powerFromBattery;
  final double batteryLevel;
  final double insideChargingLatestmA;
  final double outsideChargingLatestmA;
  final double bmwBatteryLevel;
  final bool bmwReadyForCharging;
  final String bmwLastUpdateFromServer;
  final double vwBatteryLevel;
  final bool vwReadyForCharging;
  final String vwLastUpdateFromServer;

  ChargingSituation({
    required this.insideConnected,
    required this.outsideConnected,
    required this.houseConsumptionPower,
    required this.insideCurrentChargingPower,
    required this.outsideCurrentChargingPower,
    required this.powerFromPV,
    required this.powerFromGrid,
    required this.powerFromBattery,
    required this.batteryLevel,
    required this.insideChargingLatestmA,
    required this.outsideChargingLatestmA,
    required this.bmwBatteryLevel,
    required this.bmwReadyForCharging,
    required this.bmwLastUpdateFromServer,
    required this.vwBatteryLevel,
    required this.vwReadyForCharging,
    required this.vwLastUpdateFromServer,
  });

  factory ChargingSituation.fromJson(Map<String, dynamic> json) {
      final insideCurrentChargingPower = json['InsideCurrentChargingPower'].toDouble();
      final outsideCurrentChargingPower = json['OutsideCurrentChargingPower'].toDouble();
    return ChargingSituation(
      insideConnected: json['InsideConnected'],
      outsideConnected: json['OutsideConnected'],
      houseConsumptionPower: json['HouseConsumptionPower'].toDouble() - insideCurrentChargingPower - outsideCurrentChargingPower,
      insideCurrentChargingPower: insideCurrentChargingPower,
      outsideCurrentChargingPower: outsideCurrentChargingPower,
      powerFromPV: json['PowerFromPV'].toDouble(),
      powerFromGrid: json['PowerFromGrid'].toDouble(),
      powerFromBattery: json['PowerFromBattery'].toDouble(),
      batteryLevel: json['BatteryLevel'].toDouble(),
      insideChargingLatestmA: json['InsideChargingLatestmA'].toDouble(),
      outsideChargingLatestmA: json['OutsideChargingLatestmA'].toDouble(),
      bmwBatteryLevel: json['BMWBatteryLevel'].toDouble(),
      bmwReadyForCharging: json['BMWReadyForCharging'],
      bmwLastUpdateFromServer: json['BMWLastUpdateFromServer'],
      vwBatteryLevel: json['VWBatteryLevel'].toDouble(),
      vwReadyForCharging: json['VWReadyForCharging'],
      vwLastUpdateFromServer: json['VWLastUpdateFromServer'],
    );
  }
}