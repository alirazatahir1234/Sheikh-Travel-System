import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/attendance_api.dart';
import '../domain/attendance_model.dart';

final attendanceHistoryProvider =
    AsyncNotifierProvider<AttendanceNotifier, List<AttendanceRecord>>(AttendanceNotifier.new);

class AttendanceNotifier extends AsyncNotifier<List<AttendanceRecord>> {
  @override
  Future<List<AttendanceRecord>> build() => _fetch();

  Future<List<AttendanceRecord>> _fetch() =>
      ref.read(attendanceApiProvider).getHistory();

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_fetch);
  }

  Future<void> checkIn({double? lat, double? lng}) async {
    await ref.read(attendanceApiProvider).checkIn(lat: lat, lng: lng);
    await refresh();
  }

  Future<void> checkOut({double? lat, double? lng}) async {
    await ref.read(attendanceApiProvider).checkOut(lat: lat, lng: lng);
    await refresh();
  }
}
