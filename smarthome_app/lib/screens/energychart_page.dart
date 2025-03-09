import 'package:flutter/material.dart';
import 'package:logger/logger.dart';
import 'package:smarthome_app/services/influxdb_service.dart';
import 'package:smarthome_app/widgets/smarthome_drawer.dart';
import 'package:syncfusion_flutter_charts/charts.dart';
import 'package:intl/intl.dart';

class EnergyChartPage extends StatefulWidget {
  const EnergyChartPage({super.key});

  @override
  EnergyChartPageState createState() => EnergyChartPageState();
}

class EnergyChartPageState extends State<EnergyChartPage> {
  final InfluxDBService influxDBService = InfluxDBService(
      url: "http://smarthomepi2:32086",
      token:
          "4fft6a1QWwzF_cAWIQphJHqu4pKvzZBCNNfEoQZUP6_31eEkTmsgySMk-nSZzqbBE2NuvIQ6XbYh8Cg7iSWFJg==",
      org: "smarthome",
      bucket: "SmartHomeData");

  List<ChartData> netzbezugDataPoints = [];
  List<ChartData> netzeinspeissungDataPoints = [];
  List<ChartData> batteryTelemetryDataPoints = [];
  late TooltipBehavior _tooltipBehavior;
  final Logger logger = Logger();

  @override
  void initState() {
    _tooltipBehavior = TooltipBehavior(enable: true);
    super.initState();
    fetchData();
  }

  Future<void> fetchData() async {
    try {
      var records = await (await influxDBService.queryEnergyData()).toList();
      setState(() {
        netzbezugDataPoints = records
            .where((record) => record['_field'] == 'Netzbezug')
            .map((record) {
          final time = DateTime.parse(record['_time'] as String).toLocal();
          final value = record['_value'] as double;
          return ChartData(time, value);
        }).toList();

        netzeinspeissungDataPoints = records
            .where((record) => record['_field'] == 'Netzeinspeissung')
            .map((record) {
          final time = DateTime.parse(record['_time'] as String).toLocal();
          final value = record['_value'] as double;
          return ChartData(time, value);
        }).toList();
      });
    } catch (e) {
      logger.i(e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final now = DateTime.now();
    final startOfDay = DateTime(now.year, now.month, now.day);
    final endOfDay = startOfDay.add(Duration(hours: 24));
    return Scaffold(
      drawer: SmarthomeDrawer(),
      appBar: AppBar(
        title: Text('Energy Chart'),
      ),
      body: Padding(
        padding: const EdgeInsets.all(8.0),
        child: SfCartesianChart(
          enableSideBySideSeriesPlacement: false,
          tooltipBehavior: _tooltipBehavior,
          primaryXAxis: DateTimeAxis(
            minimum: startOfDay,
            maximum: endOfDay,
            intervalType: DateTimeIntervalType.hours,
            interval: 1,
            dateFormat: DateFormat.Hm(),
          ),
          primaryYAxis: NumericAxis(
            labelFormat: '{value} Wh',
          ),
          series: <CartesianSeries>[
            if (netzbezugDataPoints.isNotEmpty)
              ColumnSeries<ChartData, DateTime>(
                dataSource: netzbezugDataPoints,
                name: 'Netzbezug',
                xValueMapper: (ChartData data, _) => data.time,
                yValueMapper: (ChartData data, _) => data.value,
                color: Color.fromARGB(255, 90, 90, 90),
                animationDuration: 200,
                enableTooltip: true,
                width: 0.9,
                spacing: 0.1,
              ),
            if (netzeinspeissungDataPoints.isNotEmpty)
              ColumnSeries<ChartData, DateTime>(
                dataSource: netzeinspeissungDataPoints,
                xValueMapper: (ChartData data, _) => data.time,
                yValueMapper: (ChartData data, _) => data.value,
                color: Colors.lightBlue,
                animationDuration: 200,
                enableTooltip: true,
                width: 0.9,
                spacing: 0.1,
              ),
            if (batteryTelemetryDataPoints.isNotEmpty)
              ColumnSeries<ChartData, DateTime>(
                dataSource: batteryTelemetryDataPoints,
                name: 'Battery Telemetry',
                xValueMapper: (ChartData data, _) => data.time,
                yValueMapper: (ChartData data, _) => data.value,
                color: Colors.green,
                animationDuration: 200,
                enableTooltip: true,
                width: 0.9,
                spacing: 0.1,
              ),
          ],
        ),
      ),
    );
  }
}

class ChartData {
  final DateTime time;
  final double value;

  ChartData(this.time, this.value);
}
