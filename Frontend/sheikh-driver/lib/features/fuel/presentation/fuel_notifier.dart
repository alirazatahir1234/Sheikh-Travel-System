import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/fuel_api.dart';

class FuelSubmitState {
  const FuelSubmitState({this.loading = false, this.error, this.success = false});
  final bool loading;
  final String? error;
  final bool success;

  FuelSubmitState copyWith({bool? loading, String? error, bool? success}) => FuelSubmitState(
        loading: loading ?? this.loading,
        error: error,
        success: success ?? this.success,
      );
}

final fuelNotifierProvider =
    AutoDisposeNotifierProvider<FuelNotifier, FuelSubmitState>(FuelNotifier.new);

class FuelNotifier extends AutoDisposeNotifier<FuelSubmitState> {
  @override
  FuelSubmitState build() => const FuelSubmitState();

  Future<void> submit({
    required int vehicleId,
    required double liters,
    required double pricePerLiter,
    required double totalCost,
    required double odometerReading,
    required String station,
    required String fuelType,
    int? driverId,
  }) async {
    state = state.copyWith(loading: true, error: null, success: false);
    try {
      await ref.read(fuelApiProvider).submitReceipt(
            vehicleId: vehicleId,
            liters: liters,
            pricePerLiter: pricePerLiter,
            totalCost: totalCost,
            odometerReading: odometerReading,
            station: station,
            fuelType: fuelType,
            driverId: driverId,
          );
      state = state.copyWith(loading: false, success: true);
    } catch (e) {
      state = state.copyWith(loading: false, error: e.toString());
    }
  }
}
