import 'package:influxdb_client/api.dart';

class InfluxDBService {
  final String url;
  final String token;
  final String org;
  final String bucket;

  InfluxDBService(
      {required this.url,
      required this.token,
      required this.org,
      required this.bucket});

  Future<Stream<FluxRecord>> queryEnergyData() async {
    var client =
        InfluxDBClient(url: url, token: token, org: org, bucket: bucket, debug: true);
    final query = '''
      import "date"
      startOfToday = date.truncate(t: now(), unit: 1d)
      startTime = date.add(d: -75m, to: startOfToday)
      endTime = date.add(d: 24h, to: startOfToday)
            
      from(bucket: "SmartHomeData")
        |> range(start: startTime, stop: endTime) 
        |> filter(fn: (r) => r["_measurement"] == "strom")
        |> filter(fn: (r) => r["_field"] == "Netzbezug" or r["_field"] == "Netzeinspeissung")
        |> filter(fn: (r) => r["location"] == "M1")
        |> aggregateWindow(every: 15m, fn: last, createEmpty: false)
        |> map(fn: (r) => ({ r with _value: if r["_field"] == "Netzbezug" then r._value*1000.0 else -r._value*1000.0 }))
        |> difference()
        |> yield(name: "delta")
    ''';


    var queryService = client.getQueryService();

    try {
            // Log the query before executing it
      var recordstream = await queryService.query(query);
      return recordstream;
    } catch (e) {
      // Log the error
      logPrint('############  Error: $e');
            if (e is FormatException) {
        logPrint('############  FormatException: ${e.message}');
        logPrint('############  Source: ${e.source}');
        logPrint('############  Offset: ${e.offset}');
      }
      rethrow;
    }
  }
}
