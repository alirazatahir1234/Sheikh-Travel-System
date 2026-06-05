import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Customer app scaffold — reuses customer-portal REST APIs (OTP auth, book, track).
/// Run: flutter run --dart-define=API_BASE_URL=http://127.0.0.1:5082/api
void main() {
  runApp(const ProviderScope(child: SheikhCustomerApp()));
}

class SheikhCustomerApp extends StatelessWidget {
  const SheikhCustomerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Sheikh Travel',
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
      appBar: AppBar(title: const Text('Sheikh Travel')),
      body: const Padding(
        padding: EdgeInsets.all(24),
        child: Text(
          'Customer mobile app shell.\n\n'
          'Wire screens to /api/customer-portal (book, track, pay) — '
          'same backend as the Angular customer portal.\n\n'
          'Prefer sheikh-customer-portal PWA until native screens are added.',
        ),
      ),
    );
  }
}
