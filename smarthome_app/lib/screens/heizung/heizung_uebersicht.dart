import 'package:flutter/material.dart';
import 'package:logger/logger.dart';
import 'package:smarthome_app/services/mqtt_service.dart';
import 'package:smarthome_app/widgets/smarthome_drawer.dart';
import 'package:intl/intl.dart';


class HeizungsUebersichtPage extends StatefulWidget {
  const HeizungsUebersichtPage({super.key});

  @override
  State<HeizungsUebersichtPage> createState() => _HeizungsUebersichtPageState();
}

class _HeizungsUebersichtPageState extends State<HeizungsUebersichtPage> {
  late MqttService mqttService;
  final Logger logger = Logger();
  
  final Map<String, double> temperatureData = {};
  
  final NumberFormat temperatureFormatter = NumberFormat.decimalPattern('de_DE')
    ..minimumFractionDigits = 1
    ..maximumFractionDigits = 1;
  
  @override
  void initState() {
    super.initState();
    mqttService = MqttService("smarthomepi2", "Smarthome.App", 32004);
    mqttService.onMessageReceived = (topic, message) {
      setState(() {
        final List<String> topicParts = topic.split('/');
        if (topicParts.length >= 3 && topicParts[1] == 'temperatur') {
          final String deviceName = topicParts[2];
          // Convert the message string to double
          try {
            temperatureData[deviceName] = double.parse(message);
            logger.i('Updated temperature for $deviceName: ${temperatureData[deviceName]}');
          } catch (e) {
            logger.e('Error parsing temperature value: $message - $e');
          }
        }
      });
    };
    mqttService.connect(["daten/temperatur/#"]).then((_) {
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
        title: Text('Heizungsübersicht'),
      ),
      body: temperatureData.isEmpty
          ? Center(child: CircularProgressIndicator())
          : Padding(
              padding: EdgeInsets.all(16.0),
              child: ListView.builder(
                itemCount: temperatureData.length,
                itemBuilder: (context, index) {
                  final deviceName = temperatureData.keys.elementAt(index);
                  final temperature = temperatureData[deviceName];
                  return Card(
                    margin: EdgeInsets.only(bottom: 8.0),
                    child: ListTile(                      title: Text(deviceName),
                      subtitle: Text('Temperatur'),
                      trailing: Text(
                        temperature != null 
                          ? '${temperatureFormatter.format(temperature)}°C'
                          : '--°C',
                        style: TextStyle(
                          fontSize: 20,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                  );
                },
              ),
            ),
    );
  }
}
