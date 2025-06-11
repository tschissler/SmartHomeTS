import 'package:flutter/material.dart';
import 'package:smarthome_app/screens/chargingsettings_page.dart';
import 'package:provider/provider.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(SmartHomeApp());
}

class SmartHomeApp extends StatelessWidget {
  const SmartHomeApp({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) => SmartHomeAppState(),
      child: MaterialApp(
        title: 'SmartHomeTS App',
        theme: ThemeData(
          useMaterial3: true,
          colorScheme: ColorScheme.fromSeed(seedColor: const Color.fromARGB(255, 6, 120, 49)),
        ),
        home: ChargingSettingsPage(),
      ),
    );
  }
}

class SmartHomeAppState extends ChangeNotifier {

}