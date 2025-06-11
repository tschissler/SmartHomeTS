import 'package:flutter/material.dart';
import 'package:smarthome_app/screens/chargingsettings_page.dart';
import 'package:smarthome_app/screens/energychart_page.dart';
import 'package:smarthome_app/screens/heizung/heizung_uebersicht.dart';

class SmarthomeDrawer extends StatelessWidget {
  const SmarthomeDrawer({
    super.key,
  });

  @override
  Widget build(BuildContext context) {
    return Drawer(
      child: ListView(
        children: [
          ListTile(
              leading: Icon(Icons.home),
              title: Text('Home'),
              onTap: () {
                Navigator.pushReplacement(context,
                    MaterialPageRoute(builder: (context) => ChargingSettingsPage()));
              }),
          ListTile(
              leading: Icon(Icons.thermostat),
              title: Text('Heizung'),
              onTap: () {
                Navigator.pushReplacement(context,
                    MaterialPageRoute(builder: (context) => HeizungsUebersichtPage()));
              }),
          ListTile(
              leading: Icon(Icons.power),
              title: Text('Energy Charts'),
              onTap: () {
                Navigator.pushReplacement(context,
                    MaterialPageRoute(builder: (context) => EnergyChartPage()));
              }),
        ],
      ),
    );
  }
}
