import 'dart:io';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/offline/offline_models.dart';
import '../data/fuel_api.dart';
import '../domain/fuel_model.dart';

class FuelSubmitState {
  const FuelSubmitState({
    this.loading = false,
    this.scanning = false,
    this.error,
    this.success = false,
    this.queuedOffline = false,
  });

  final bool loading;
  final bool scanning;
  final String? error;
  final bool success;
  final bool queuedOffline;

  FuelSubmitState copyWith({
    bool? loading,
    bool? scanning,
    String? error,
    bool? success,
    bool? queuedOffline,
  }) =>
      FuelSubmitState(
        loading: loading ?? this.loading,
        scanning: scanning ?? this.scanning,
        error: error,
        success: success ?? this.success,
        queuedOffline: queuedOffline ?? this.queuedOffline,
      );
}

final fuelNotifierProvider =
    AutoDisposeNotifierProvider<FuelNotifier, FuelSubmitState>(FuelNotifier.new);

final fuelHistoryProvider = FutureProvider.autoDispose<List<FuelLog>>(
  (ref) => ref.read(fuelApiProvider).getHistory(),
);

class FuelNotifier extends AutoDisposeNotifier<FuelSubmitState> {
  @override
  FuelSubmitState build() => const FuelSubmitState();

  Future<void> submit({
    required int vehicleId,
    required double liters,
    required double pricePerLiter,
    required double odometerReading,
    required String station,
    required String fuelType,
    File? receipt,
  }) async {
    state = state.copyWith(
        loading: true, error: null, success: false, queuedOffline: false);
    try {
      await ref.read(fuelApiProvider).submitReceipt(
            vehicleId: vehicleId,
            liters: liters,
            pricePerLiter: pricePerLiter,
            odometerReading: odometerReading,
            station: station,
            fuelType: fuelType,
            receipt: receipt,
          );
      state = state.copyWith(loading: false, success: true);
      ref.invalidate(fuelHistoryProvider);
    } on OfflineQueuedException {
      state =
          state.copyWith(loading: false, success: true, queuedOffline: true);
    } catch (e) {
      state = state.copyWith(loading: false, error: e.toString());
    }
  }

  Future<FuelOcrSuggestion?> scan(File receipt) async {
    state = state.copyWith(scanning: true, error: null);
    try {
      final suggestion = await ref.read(fuelApiProvider).scanReceipt(receipt);
      state = state.copyWith(scanning: false);
      return suggestion;
    } catch (e) {
      state = state.copyWith(scanning: false, error: e.toString());
      return null;
    }
  }
}
