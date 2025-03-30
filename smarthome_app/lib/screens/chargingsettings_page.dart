import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:smarthome_app/models/car_status.dart';
import 'package:smarthome_app/models/car_type.dart';
import 'package:smarthome_app/models/charging_situation.dart';
import 'package:smarthome_app/services/mqtt_service.dart';
import 'package:smarthome_app/widgets/batterystatus.dart';
import 'package:smarthome_app/widgets/carstatus.dart';
import 'package:smarthome_app/widgets/charginglevelselector.dart';
import 'package:smarthome_app/widgets/energy_distribution.dart';
import 'package:smarthome_app/widgets/scalable_widget.dart';
import 'package:smarthome_app/widgets/smarthome_drawer.dart';
import 'package:logger/logger.dart';

class ChargingSettingsPage extends StatefulWidget {
  const ChargingSettingsPage({super.key});

  @override
  State<ChargingSettingsPage> createState() => _ChargingSettingsPageState();
}

class _ChargingSettingsPageState extends State<ChargingSettingsPage> {
  late MqttService mqttService;

  ChargingSituation? chargingSituation;
  CarStatus? miniStatus;
  CarStatus? bmwStatus;
  CarStatus? id4Status;
  final Logger logger = Logger();

  @override
  void initState() {
    super.initState();
    mqttService = MqttService("smarthomepi2", "Smarthome.App", 32004);
    mqttService.onMessageReceived = (topic, message) {
      setState(() {
        switch (topic.toLowerCase()) {
          case "data/charging/situation":
            var data = jsonDecode(message);
            chargingSituation = ChargingSituation.fromJson(data);
          case "data/charging/mini":
            var data = jsonDecode(message);
            miniStatus = CarStatus.fromJson(data);
          case "data/charging/bmw":
            var data = jsonDecode(message);
            bmwStatus = CarStatus.fromJson(data);
          case "data/charging/vw":
            var data = jsonDecode(message);
            id4Status = CarStatus.fromJson(data);
        }
      });
    };
    mqttService.connect(["data/charging/#"]).then((_) {
      logger.i("Connected to MQTT Broker");
    }).catchError((error) {
      logger.i('Connection error: $error');
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
        drawer: SmarthomeDrawer(),
        appBar: AppBar(
          title: Text('Ladeeinstellungen', style: TextStyle(fontSize: 16)),
          toolbarHeight: 20,
          iconTheme: IconThemeData(size: 16),
        ),
        body: SingleChildScrollView(
          padding: const EdgeInsets.all(10.0),
          child: Column(children: [
            SizedBox(
              child: BatteryStatusWidget(
                  batteryPercentage: chargingSituation?.batteryLevel ?? 0),
            ),
            SizedBox(height: 20),
            EnergyDistributionWidget(
              pvPower: chargingSituation?.powerFromPV ?? 0,
              batteryPower: chargingSituation?.powerFromBattery ?? 0,
              gridPower: chargingSituation?.powerFromGrid ?? 0,
              housePower: chargingSituation?.houseConsumptionPower ?? 0,
              garagePower: chargingSituation?.insideCurrentChargingPower ?? 0,
              outsidePower: chargingSituation?.outsideCurrentChargingPower ?? 0,
            ),
            SizedBox(height: 20),
            ChargingLevelSelector(),
            SizedBox(height: 10),
            ScalableWidget(widgets: [
              CarStatusWidget(car: CarType.bmw, carStatus: bmwStatus),
              CarStatusWidget(car: CarType.mini, carStatus: miniStatus),
              CarStatusWidget(car: CarType.id4, carStatus: id4Status),
            ]),
          ]),
        ));
  }
}
