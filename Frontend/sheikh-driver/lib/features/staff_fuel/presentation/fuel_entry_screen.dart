import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/data/auth_repository.dart';
import '../../fuel/presentation/fuel_screen.dart';
import 'staff_fuel_list_screen.dart';

/// Staff see fleet fuel logs; drivers keep receipt capture UI.
class FuelEntryScreen extends ConsumerWidget {
  const FuelEntryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(fleetSessionProvider);
    if (session == null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (session.isDriverOnly) return const FuelScreen();
    return const StaffFuelListScreen();
  }
}
