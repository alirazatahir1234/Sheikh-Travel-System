import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class AppShell extends StatelessWidget {
  const AppShell({super.key, required this.child});
  final Widget child;

  static const _tabs = [
    _Tab(icon: Icons.home_outlined, activeIcon: Icons.home, label: 'Home', path: '/dashboard'),
    _Tab(icon: Icons.directions_car_outlined, activeIcon: Icons.directions_car, label: 'Trips', path: '/trips'),
    _Tab(icon: Icons.map_outlined, activeIcon: Icons.map, label: 'Live', path: '/live'),
    _Tab(icon: Icons.notifications_outlined, activeIcon: Icons.notifications, label: 'Alerts', path: '/notifications'),
    _Tab(icon: Icons.person_outlined, activeIcon: Icons.person, label: 'Profile', path: '/profile'),
  ];

  int _currentIndex(BuildContext context) {
    final loc = GoRouterState.of(context).uri.path;
    for (var i = 0; i < _tabs.length; i++) {
      if (loc.startsWith(_tabs[i].path)) return i;
    }
    return 0;
  }

  @override
  Widget build(BuildContext context) {
    final idx = _currentIndex(context);
    return Scaffold(
      body: child,
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: idx,
        onTap: (i) => context.go(_tabs[i].path),
        items: _tabs
            .map((t) => BottomNavigationBarItem(
                  icon: Icon(t.icon),
                  activeIcon: Icon(t.activeIcon),
                  label: t.label,
                ))
            .toList(),
      ),
    );
  }
}

class _Tab {
  const _Tab({
    required this.icon,
    required this.activeIcon,
    required this.label,
    required this.path,
  });
  final IconData icon;
  final IconData activeIcon;
  final String label;
  final String path;
}
