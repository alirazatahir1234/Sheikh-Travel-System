import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/data/auth_repository.dart';
import '../../trips/presentation/trips_screen.dart';
import 'ops_trips_screen.dart';
import '../../../shared/widgets/sg_brand_loader.dart';

/// Staff see ops trips (`/api/trips`); drivers keep the driver-app trips UI.
class TripsEntryScreen extends ConsumerWidget {
  const TripsEntryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(fleetSessionProvider);
    if (session == null) {
      return const Scaffold(body: Center(child: SgBrandLoader()));
    }
    if (session.isDriverOnly) {
      return const TripsScreen();
    }
    return const OpsTripsScreen();
  }
}
