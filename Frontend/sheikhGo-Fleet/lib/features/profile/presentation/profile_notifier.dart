import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/profile_api.dart';
import '../domain/driver_profile_model.dart';

final profileProvider =
    AsyncNotifierProvider<ProfileNotifier, DriverProfile>(ProfileNotifier.new);

class ProfileNotifier extends AsyncNotifier<DriverProfile> {
  @override
  Future<DriverProfile> build() => ref.read(profileApiProvider).getProfile();

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => ref.read(profileApiProvider).getProfile());
  }
}
