import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// SheikhGo customer app scaffold — reuses customer-hub REST APIs (OTP auth, book, track).
/// Run: flutter run --dart-define=API_BASE_URL=http://127.0.0.1:5082/api
void main() {
  runApp(const ProviderScope(child: SheikhGoApp()));
}

class SheikhGoApp extends StatelessWidget {
  const SheikhGoApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'SheikhGo',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF0369A1)),
        useMaterial3: true,
      ),
      home: const _HomePlaceholder(),
    );
  }
}

class _HomePlaceholder extends StatelessWidget {
  const _HomePlaceholder();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('SheikhGo')),
      body: const Padding(
        padding: EdgeInsets.all(24),
        child: Text(
          'SheikhGo customer mobile app shell.\n\n'
          'Wire screens to /api/customer-portal (book, track, pay) — '
          'same backend as Sheikh Travel Customer Hub.\n\n'
          'Prefer sheikh-travel-customer-hub PWA until native screens are added.',
        ),
      ),
    );
  }
}
