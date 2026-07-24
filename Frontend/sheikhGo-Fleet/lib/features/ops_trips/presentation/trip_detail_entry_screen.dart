import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/data/auth_repository.dart';
import '../../trips/presentation/trip_detail_screen.dart';
import 'ops_trip_detail_screen.dart';

class TripDetailEntryScreen extends ConsumerWidget {
  const TripDetailEntryScreen({super.key, required this.tripId});
  final int tripId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(fleetSessionProvider);
    if (session != null && !session.isDriverOnly) {
      return OpsTripDetailScreen(tripId: tripId);
    }
    return TripDetailScreen(tripId: tripId);
  }
}
