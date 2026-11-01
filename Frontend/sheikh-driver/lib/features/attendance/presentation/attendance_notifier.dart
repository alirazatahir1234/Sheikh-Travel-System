import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/errors/app_exception.dart';
import '../../../core/errors/error_handler.dart';
import '../../auth/data/auth_repository.dart';
import '../../auth/domain/auth_models.dart';
import '../../settings/domain/settings_access.dart';
import '../data/attendance_api.dart';
import '../domain/attendance_model.dart';

/// True when the signed-in user should see personal clock-in (driver mobile).
bool attendanceIsPersonalMode(FleetSession? session) =>
    session?.isDriverSession == true;

/// True when staff with Driver.View (or role) should see the team board.
bool attendanceIsTeamMode(FleetSession? session) =>
    session != null &&
    !attendanceIsPersonalMode(session) &&
    SettingsAccess.canSeeAttendance(session);

final attendanceHistoryProvider =
    AsyncNotifierProvider<AttendanceNotifier, List<AttendanceRecord>>(
  AttendanceNotifier.new,
);

class AttendanceNotifier extends AsyncNotifier<List<AttendanceRecord>> {
  @override
  Future<List<AttendanceRecord>> build() => _fetch();

  Future<List<AttendanceRecord>> _fetch() async {
    try {
      return await ref.read(attendanceApiProvider).getHistory(pageSize: 90);
    } catch (e) {
      throw toAppException(e);
    }
  }

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

  Future<void> requestCorrection({
    required DateTime attendanceDate,
    required String notes,
  }) async {
    await ref.read(attendanceApiProvider).requestCorrection(
          attendanceDate: attendanceDate,
          notes: notes,
        );
    await refresh();
  }
}

class TeamAttendanceState {
  const TeamAttendanceState({
    required this.date,
    required this.rows,
    this.query = '',
  });

  final DateTime date;
  final List<TeamAttendanceRow> rows;
  final String query;

  TeamAttendanceOverview get overview => TeamAttendanceOverview.fromRows(rows);

  List<TeamAttendanceRow> get filtered {
    final q = query.trim().toLowerCase();
    if (q.isEmpty) return rows;
    return rows
        .where(
          (r) =>
              r.fullName.toLowerCase().contains(q) ||
              (r.phone?.toLowerCase().contains(q) ?? false) ||
              (r.branchName?.toLowerCase().contains(q) ?? false),
        )
        .toList();
  }
}

final teamAttendanceProvider =
    AsyncNotifierProvider<TeamAttendanceNotifier, TeamAttendanceState>(
  TeamAttendanceNotifier.new,
);

class TeamAttendanceNotifier extends AsyncNotifier<TeamAttendanceState> {
  @override
  Future<TeamAttendanceState> build() => _fetch(DateTime.now());

  Future<TeamAttendanceState> _fetch(DateTime date, {String query = ''}) async {
    try {
      final rows = await ref.read(attendanceApiProvider).getTeamAttendance(
            date: date,
            q: query.isEmpty ? null : query,
          );
      return TeamAttendanceState(date: date, rows: rows, query: query);
    } catch (e) {
      throw toAppException(e);
    }
  }

  Future<void> refresh() async {
    final current = state.valueOrNull;
    final date = current?.date ?? DateTime.now();
    final query = current?.query ?? '';
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _fetch(date, query: query));
  }

  Future<void> setDate(DateTime date) async {
    final query = state.valueOrNull?.query ?? '';
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _fetch(date, query: query));
  }

  Future<void> setQuery(String query) async {
    final current = state.valueOrNull;
    if (current == null) return;
    // Local filter first for snappy UI; refresh from API when query settles.
    state = AsyncData(
      TeamAttendanceState(
        date: current.date,
        rows: current.rows,
        query: query,
      ),
    );
  }

  Future<void> searchRemote(String query) async {
    final date = state.valueOrNull?.date ?? DateTime.now();
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _fetch(date, query: query));
  }

  Future<void> applyCorrection({
    required int driverId,
    required DateTime attendanceDate,
    required String status,
    String? notes,
  }) async {
    final session = ref.read(fleetSessionProvider);
    if (session == null ||
        !session.hasPermission(FleetPermissions.driverUpdate)) {
      throw ForbiddenException.attendance;
    }
    await ref.read(attendanceApiProvider).createManagerCorrection(
          driverId: driverId,
          attendanceDate: attendanceDate,
          status: status,
          notes: notes,
        );
    await refresh();
  }
}

String attendanceErrorMessage(Object error) => ErrorHandler.message(error);

bool attendanceCanRetry(Object error) {
  final mapped = error is AppException ? error : toAppException(error);
  return ErrorHandler.isTransient(mapped);
}
