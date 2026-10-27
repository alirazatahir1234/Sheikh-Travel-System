import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../data/driver_score_api.dart';
import '../domain/driver_score_models.dart';

final driverScoreApiProvider = Provider<DriverScoreApi>((ref) {
  return DriverScoreApi(ref.watch(dioProvider));
});

final myDriverGpsScoreProvider = FutureProvider.autoDispose<DriverGpsScore>((ref) async {
  final to = DateTime.now().toUtc();
  final from = to.subtract(const Duration(days: 30));
  return ref.watch(driverScoreApiProvider).myScore(from: from, to: to);
});
