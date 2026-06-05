import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'features/auth/login_screen.dart';
import 'features/trips/trips_screen.dart';
import 'core/services/session_storage.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const ProviderScope(child: SheikhDriverApp()));
}

class SheikhDriverApp extends ConsumerWidget {
  const SheikhDriverApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final token = ref.watch(sessionTokenProvider);
    return MaterialApp(
      title: 'Sheikh Driver',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF0D9488)),
        useMaterial3: true,
      ),
      home: token == null || token.isEmpty
          ? const LoginScreen()
          : const TripsScreen(),
    );
  }
}
