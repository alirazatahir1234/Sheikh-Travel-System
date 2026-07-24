import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/offline/offline_models.dart';
import '../data/trips_api.dart';
import '../domain/trip_model.dart';

final tripsProvider = AsyncNotifierProvider.autoDispose<TripsNotifier, List<Trip>>(
  TripsNotifier.new,
);

class TripsNotifier extends AutoDisposeAsyncNotifier<List<Trip>> {
  @override
  Future<List<Trip>> build() => _fetch();

  Future<List<Trip>> _fetch() => ref.read(tripsApiProvider).getTrips();

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_fetch);
  }

  /// Returns an offline queue message when the action was saved locally.
  Future<String?> advance(int id, String action, {String? reason}) async {
    try {
      await ref.read(tripsApiProvider).advance(id, action, reason: reason);
      await refresh();
      return null;
    } on OfflineQueuedException catch (e) {
      try {
        state = AsyncData(await _fetch());
      } catch (_) {}
      return e.message;
    }
  }

  Future<String?> startTrip(int id) => advance(id, 'Accept');
  Future<String?> completeTrip(int id) => advance(id, 'Complete');
  Future<String?> rejectTrip(int id, String reason) =>
      advance(id, 'Reject', reason: reason);
}
