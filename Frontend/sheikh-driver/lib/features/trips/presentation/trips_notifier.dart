import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/trips_api.dart';
import '../domain/trip_model.dart';

final tripsProvider =
    AsyncNotifierProvider<TripsNotifier, List<Trip>>(TripsNotifier.new);

class TripsNotifier extends AsyncNotifier<List<Trip>> {
  @override
  Future<List<Trip>> build() => _fetch();

  Future<List<Trip>> _fetch() => ref.read(tripsApiProvider).getTrips();

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_fetch);
  }

  Future<void> startTrip(int id) async {
    await ref.read(tripsApiProvider).startTrip(id);
    await refresh();
  }

  Future<void> completeTrip(int id) async {
    await ref.read(tripsApiProvider).completeTrip(id);
    await refresh();
  }

  Future<void> rejectTrip(int id, String reason) async {
    await ref.read(tripsApiProvider).rejectTrip(id, reason);
    await refresh();
  }
}
