class CarStatus {
  final String brand;
  final String name;
  final int battery;
  final String chargingStatus;
  final int chargingTarget;
  final DateTime? chargingEndTime;
  final bool chargerConnected;
  final int remainingRange;
  final int mileage;
  final bool? moving;
  final DateTime lastUpdate;

  CarStatus({
    required this.brand,
    required this.name,
    required this.battery,
    required this.chargingStatus,
    required this.chargingTarget,
    required this.chargingEndTime,
    required this.chargerConnected,
    required this.remainingRange,
    required this.mileage,
    required this.moving,
    required this.lastUpdate,
  });

  factory CarStatus.fromJson(Map<String, dynamic> json) {
    return CarStatus(
      brand: json['brand'],
      name: json['name'],
      battery: json['battery'],
      chargingStatus: json['chargingStatus'],
      chargingTarget: json['chargingTarget'],
      chargingEndTime: json['chargingEndTime'] != null ? DateTime.tryParse(json['chargingEndTime']) : null,
      chargerConnected: json['chargerConnected'],
      remainingRange: json['remainingRange'],
      mileage: json['mileage'],
      moving: json['moving'],
      lastUpdate: DateTime.parse(json['lastUpdate']).toLocal(),
    );
  }
}