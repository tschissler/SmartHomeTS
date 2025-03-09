import 'package:flutter/material.dart';
import 'package:smarthome_app/screens/chargingsettings_page.dart';
import 'package:smarthome_app/widgets/smarthome_drawer.dart';

enum Page { generator, favorites }

class MyHomePage extends StatelessWidget {
  const MyHomePage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('SmartHomeTS App'),
      ),
      drawer: SmarthomeDrawer(),
      body: ChargingSettingsPage(),
    );
  }
}
