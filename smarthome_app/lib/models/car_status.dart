class CarStatus {
  final String nickname;
  final String brand;
  final String name;
  final double battery;
  final String chargingStatus;
  final double chargingTarget;
  final DateTime? chargingEndTime;
  final bool chargerConnected;
  final String state;
  final double remainingRange;
  final double mileage;
  final DateTime? lastUpdate;
  final bool moving;

  const CarStatus({
    this.nickname = '',
    this.brand = '',
    this.name = '',
    this.battery = 0,
    this.chargingStatus = '',
    this.chargingTarget = 0,
    this.chargingEndTime,
    this.chargerConnected = false,
    this.state = '',
    this.remainingRange = 0,
    this.mileage = 0,
    this.lastUpdate,
    this.moving = false,
  });

  factory CarStatus.fromJson(Map<String, dynamic> json) {
    return CarStatus(
      nickname: json['nickname'] as String? ?? '',
      brand: json['brand'] as String? ?? '',
      name: json['name'] as String? ?? '',
      battery: (json['battery'] as num?)?.toDouble() ?? 0,
      chargingStatus: json['chargingStatus'] as String? ?? '',
      chargingTarget: (json['chargingTarget'] as num?)?.toDouble() ?? 0,
      chargingEndTime: json['chargingEndTime'] != null
          ? DateTime.tryParse(json['chargingEndTime'] as String)
          : null,
      chargerConnected: json['chargerConnected'] as bool? ?? false,
      state: json['state'] as String? ?? '',
      remainingRange: (json['remainingRange'] as num?)?.toDouble() ?? 0,
      mileage: (json['mileage'] as num?)?.toDouble() ?? 0,
      lastUpdate: json['lastUpdate'] != null
          ? DateTime.tryParse(json['lastUpdate'] as String)
          : null,
      moving: json['moving'] as bool? ?? false,
    );
  }
}
