class DataPoint {
  final double value;
  final DateTime lastUpdated;

  DataPoint({this.value = 0, DateTime? lastUpdated})
      : lastUpdated = lastUpdated ?? DateTime.fromMillisecondsSinceEpoch(0);
}

class ClimateData {
  final DataPoint basementTemperature;
  final DataPoint basementHumidity;
  final DataPoint outsideTemperature;
  final DataPoint outsideHumidity;
  final DataPoint childRoomTemperature;
  final DataPoint childRoomHumidity;
  final DataPoint bathRoomM1Temperature;
  final DataPoint bathRoomM1Humidity;
  final DataPoint livingRoomTemperature;
  final DataPoint livingRoomHumidity;
  final DataPoint bedroomTemperature;
  final DataPoint bedroomHumidity;
  final DataPoint cisternFillLevel;

  ClimateData({
    DataPoint? basementTemperature,
    DataPoint? basementHumidity,
    DataPoint? outsideTemperature,
    DataPoint? outsideHumidity,
    DataPoint? childRoomTemperature,
    DataPoint? childRoomHumidity,
    DataPoint? bathRoomM1Temperature,
    DataPoint? bathRoomM1Humidity,
    DataPoint? livingRoomTemperature,
    DataPoint? livingRoomHumidity,
    DataPoint? bedroomTemperature,
    DataPoint? bedroomHumidity,
    DataPoint? cisternFillLevel,
  })  : basementTemperature = basementTemperature ?? DataPoint(),
        basementHumidity = basementHumidity ?? DataPoint(),
        outsideTemperature = outsideTemperature ?? DataPoint(),
        outsideHumidity = outsideHumidity ?? DataPoint(),
        childRoomTemperature = childRoomTemperature ?? DataPoint(),
        childRoomHumidity = childRoomHumidity ?? DataPoint(),
        bathRoomM1Temperature = bathRoomM1Temperature ?? DataPoint(),
        bathRoomM1Humidity = bathRoomM1Humidity ?? DataPoint(),
        livingRoomTemperature = livingRoomTemperature ?? DataPoint(),
        livingRoomHumidity = livingRoomHumidity ?? DataPoint(),
        bedroomTemperature = bedroomTemperature ?? DataPoint(),
        bedroomHumidity = bedroomHumidity ?? DataPoint(),
        cisternFillLevel = cisternFillLevel ?? DataPoint();

  ClimateData copyWith({
    DataPoint? basementTemperature,
    DataPoint? basementHumidity,
    DataPoint? outsideTemperature,
    DataPoint? outsideHumidity,
    DataPoint? childRoomTemperature,
    DataPoint? childRoomHumidity,
    DataPoint? bathRoomM1Temperature,
    DataPoint? bathRoomM1Humidity,
    DataPoint? livingRoomTemperature,
    DataPoint? livingRoomHumidity,
    DataPoint? bedroomTemperature,
    DataPoint? bedroomHumidity,
    DataPoint? cisternFillLevel,
  }) {
    return ClimateData(
      basementTemperature: basementTemperature ?? this.basementTemperature,
      basementHumidity: basementHumidity ?? this.basementHumidity,
      outsideTemperature: outsideTemperature ?? this.outsideTemperature,
      outsideHumidity: outsideHumidity ?? this.outsideHumidity,
      childRoomTemperature: childRoomTemperature ?? this.childRoomTemperature,
      childRoomHumidity: childRoomHumidity ?? this.childRoomHumidity,
      bathRoomM1Temperature:
          bathRoomM1Temperature ?? this.bathRoomM1Temperature,
      bathRoomM1Humidity: bathRoomM1Humidity ?? this.bathRoomM1Humidity,
      livingRoomTemperature:
          livingRoomTemperature ?? this.livingRoomTemperature,
      livingRoomHumidity: livingRoomHumidity ?? this.livingRoomHumidity,
      bedroomTemperature: bedroomTemperature ?? this.bedroomTemperature,
      bedroomHumidity: bedroomHumidity ?? this.bedroomHumidity,
      cisternFillLevel: cisternFillLevel ?? this.cisternFillLevel,
    );
  }
}
