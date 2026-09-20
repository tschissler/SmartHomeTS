import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'providers/notifications_provider.dart';
import 'screens/charging/charging_screen.dart';
import 'screens/climate/climate_screen.dart';
import 'screens/heating/heating_screen.dart';
import 'screens/led/led_screen.dart';
import 'screens/notifications/notifications_panel.dart';

// ─── Router ───────────────────────────────────────────────────────────────

final appRouter = GoRouter(
  initialLocation: '/',
  routes: [
    StatefulShellRoute.indexedStack(
      builder: (context, state, navigationShell) =>
          _ScaffoldWithNavBar(navigationShell: navigationShell),
      branches: [
        StatefulShellBranch(routes: [
          GoRoute(path: '/', builder: (_, __) => const LedScreen()),
        ]),
        StatefulShellBranch(routes: [
          GoRoute(path: '/charging', builder: (_, __) => const ChargingScreen()),
        ]),
        StatefulShellBranch(routes: [
          GoRoute(path: '/climate', builder: (_, __) => const ClimateScreen()),
        ]),
        StatefulShellBranch(routes: [
          GoRoute(path: '/heating', builder: (_, __) => const HeatingScreen()),
        ]),
      ],
    ),
  ],
);

// ─── Shell scaffold with persistent bottom nav ────────────────────────────

class _ScaffoldWithNavBar extends ConsumerWidget {
  const _ScaffoldWithNavBar({required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static const _titles = ['Beleuchtung', 'Laden', 'Klima', 'Heizung'];

  void _openNotifications(BuildContext context) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (_) => const NotificationsPanel(),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final count =
        ref.watch(notificationsProvider.select((s) => s.active.length));

    return Scaffold(
      appBar: AppBar(
        title: Text(_titles[navigationShell.currentIndex]),
        actions: [
          Badge(
            isLabelVisible: count > 0,
            label: Text('$count'),
            child: IconButton(
              icon: Icon(count > 0
                  ? Icons.notifications_active_outlined
                  : Icons.notifications_outlined),
              tooltip: 'Meldungen',
              onPressed: () => _openNotifications(context),
            ),
          ),
          const SizedBox(width: 8),
        ],
      ),
      body: navigationShell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: navigationShell.currentIndex,
        onDestinationSelected: navigationShell.goBranch,
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.light_mode_outlined),
            selectedIcon: Icon(Icons.light_mode),
            label: 'Beleuchtung',
          ),
          NavigationDestination(
            icon: Icon(Icons.ev_station_outlined),
            selectedIcon: Icon(Icons.ev_station),
            label: 'Laden',
          ),
          NavigationDestination(
            icon: Icon(Icons.thermostat_outlined),
            selectedIcon: Icon(Icons.thermostat),
            label: 'Klima',
          ),
          NavigationDestination(
            icon: Icon(Icons.air_outlined),
            selectedIcon: Icon(Icons.air),
            label: 'Heizung',
          ),
        ],
      ),
    );
  }
}

// ─── Root app widget ──────────────────────────────────────────────────────

class SmartHomeApp extends StatelessWidget {
  const SmartHomeApp({super.key});

  static const _seedColor = Color(0xFF1E6B3C);

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'SmartHomeTS',
      debugShowCheckedModeBanner: false,
      themeMode: ThemeMode.system,
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(
          seedColor: _seedColor,
          brightness: Brightness.light,
        ),
        cardTheme: CardThemeData(
          elevation: 1,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
        appBarTheme: const AppBarTheme(centerTitle: false),
      ),
      darkTheme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(
          seedColor: _seedColor,
          brightness: Brightness.dark,
        ),
        cardTheme: CardThemeData(
          elevation: 1,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
        appBarTheme: const AppBarTheme(centerTitle: false),
      ),
      routerConfig: appRouter,
    );
  }
}
