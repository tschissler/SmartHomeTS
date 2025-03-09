import 'package:flutter/material.dart';
import 'package:smarthome_app/main.dart';
import 'package:smarthome_app/widgets/smarthome_drawer.dart';
import 'package:provider/provider.dart';

class FavoritesPage extends StatelessWidget {
  const FavoritesPage({super.key});

  @override
  Widget build(BuildContext context) {
    var favorites = context.watch<MyAppState>().favorites;

    return Scaffold(
      appBar: AppBar(
        title: Text('Favorites'),
      ),
      drawer: SmarthomeDrawer(),
      body: favorites.isEmpty
          ? Center(
              child: Text('No favorites yet.'),
            )
          : ListView(
              children: favorites.map((pair) {
                return ListTile(
                  title: Text(pair.asPascalCase),
                  trailing: Icon(Icons.favorite),
                );
              }).toList(),
            ),
    );
  }
}
