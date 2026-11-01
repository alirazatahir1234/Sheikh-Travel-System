import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:intl/intl.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/app_exception.dart';
import '../../../core/offline/offline_models.dart';
import '../../auth/data/auth_repository.dart';
import '../../auth/domain/auth_models.dart';
import '../domain/attendance_model.dart';
import 'attendance_notifier.dart';
import '../../../shared/widgets/sg_brand_loader.dart';

const _bg = Color(0xFFF2F6F8);
const _navy = Color(0xFF071D2E);
const _teal = Color(0xFF0D9488);
const _emerald = Color(0xFF14B8A6);

class AttendanceScreen extends ConsumerWidget {
  const AttendanceScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(fleetSessionProvider);
    if (attendanceIsPersonalMode(session)) {
      return const _DriverAttendanceView();
    }
    if (attendanceIsTeamMode(session)) {
      return const _TeamAttendanceView();
    }
    return Scaffold(
      backgroundColor: _bg,
      appBar: AppBar(
        title: const Text('Attendance'),
        backgroundColor: Colors.white,
        foregroundColor: _navy,
      ),
      body: _AttendanceErrorBody(
        message: ForbiddenException.attendance.message,
        onRetry: null,
      ),
    );
  }
}

// ── Driver (personal) ───────────────────────────────────────────────────────

class _DriverAttendanceView extends ConsumerStatefulWidget {
  const _DriverAttendanceView();

  @override
  ConsumerState<_DriverAttendanceView> createState() =>
      _DriverAttendanceViewState();
}

class _DriverAttendanceViewState extends ConsumerState<_DriverAttendanceView> {
  bool _actionLoading = false;
  DateTime _calendarMonth = DateTime(DateTime.now().year, DateTime.now().month);

  bool _isClockedIn(List<AttendanceRecord> records) {
    if (records.isEmpty) return false;
    final today = DateFormat('yyyy-MM-dd').format(DateTime.now());
    final todayRecords = records
        .where((r) =>
            DateFormat('yyyy-MM-dd').format(r.recordedAt.toLocal()) == today)
        .toList()
      ..sort((a, b) => b.recordedAt.compareTo(a.recordedAt));
    if (todayRecords.isEmpty) return false;
    return todayRecords.first.isCheckIn;
  }

  AttendanceDaySummary? _todaySummary(List<AttendanceDaySummary> days) {
    final today = DateTime.now();
    for (final d in days) {
      if (d.date.year == today.year &&
          d.date.month == today.month &&
          d.date.day == today.day) {
        return d;
      }
    }
    return null;
  }

  Future<Position?> _getLocation() async {
    try {
      final perm = await Geolocator.requestPermission();
      if (perm == LocationPermission.denied ||
          perm == LocationPermission.deniedForever) {
        return null;
      }
      return await Geolocator.getCurrentPosition(
        desiredAccuracy: LocationAccuracy.medium,
        timeLimit: const Duration(seconds: 8),
      );
    } catch (_) {
      return null;
    }
  }

  Future<void> _handleAction(bool clockedIn) async {
    final l10n = AppLocalizations.of(context);
    setState(() => _actionLoading = true);
    try {
      final pos = await _getLocation();
      final notifier = ref.read(attendanceHistoryProvider.notifier);
      if (clockedIn) {
        await notifier.checkOut(lat: pos?.latitude, lng: pos?.longitude);
      } else {
        await notifier.checkIn(lat: pos?.latitude, lng: pos?.longitude);
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              clockedIn ? l10n.clockedOutSuccess : l10n.clockedInSuccess,
            ),
            backgroundColor: clockedIn ? AppColors.warning : AppColors.success,
          ),
        );
      }
    } on OfflineQueuedException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text(e.message), backgroundColor: AppColors.warning),
        );
        ref.read(attendanceHistoryProvider.notifier).refresh();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(attendanceErrorMessage(e)),
            backgroundColor: AppColors.error,
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _actionLoading = false);
    }
  }

  Future<void> _requestCorrection(DateTime day) async {
    final controller = TextEditingController();
    final notes = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Request correction'),
        content: TextField(
          controller: controller,
          maxLines: 3,
          decoration: const InputDecoration(
            hintText: 'Describe the correction needed',
            border: OutlineInputBorder(),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, controller.text.trim()),
            child: const Text('Submit'),
          ),
        ],
      ),
    );
    if (notes == null || notes.isEmpty || !mounted) return;
    try {
      await ref.read(attendanceHistoryProvider.notifier).requestCorrection(
            attendanceDate: day,
            notes: notes,
          );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Correction request submitted'),
            backgroundColor: AppColors.success,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(attendanceErrorMessage(e)),
            backgroundColor: AppColors.error,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final historyAsync = ref.watch(attendanceHistoryProvider);
    final dateLabel = DateFormat('EEEE, d MMMM yyyy').format(DateTime.now());

    return Scaffold(
      backgroundColor: _bg,
      appBar: AppBar(
        title: Text(l10n.attendance),
        backgroundColor: Colors.white,
        foregroundColor: _navy,
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_outlined),
            onPressed: () =>
                ref.read(attendanceHistoryProvider.notifier).refresh(),
          ),
        ],
      ),
      body: historyAsync.when(
        loading: () => const Center(child: SgBrandLoader()),
        error: (e, _) => _AttendanceErrorBody(
          message: attendanceErrorMessage(e),
          onRetry: attendanceCanRetry(e)
              ? () => ref.read(attendanceHistoryProvider.notifier).refresh()
              : null,
          retryLabel: l10n.retry,
        ),
        data: (records) {
          final days = groupAttendanceByDay(records);
          final overview = buildOverview(days);
          final today = _todaySummary(days);
          final clockedIn = _isClockedIn(records);
          final timeFmt = DateFormat('HH:mm');

          return RefreshIndicator(
            color: _teal,
            onRefresh: () =>
                ref.read(attendanceHistoryProvider.notifier).refresh(),
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 28),
              children: [
                Text(
                  dateLabel,
                  style: TextStyle(
                    color: context.sg.textSecondary,
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: 12),
                _Card(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          const Icon(Icons.today_outlined,
                              color: _teal, size: 20),
                          const SizedBox(width: 8),
                          Text(
                            "Today's Attendance",
                            style: TextStyle(
                              color: context.sg.textPrimary,
                              fontWeight: FontWeight.w700,
                              fontSize: 16,
                            ),
                          ),
                          const Spacer(),
                          _StatusChip(
                            label: clockedIn
                                ? 'Clocked in'
                                : (today?.isPresent == true
                                    ? 'Completed'
                                    : 'Not checked in'),
                            color: clockedIn
                                ? _emerald
                                : (today?.isPresent == true
                                    ? AppColors.success
                                    : AppColors.warning),
                          ),
                        ],
                      ),
                      const SizedBox(height: 16),
                      Row(
                        children: [
                          Expanded(
                            child: _MetricTile(
                              label: 'In',
                              value: today?.checkIn != null
                                  ? timeFmt.format(
                                      today!.checkIn!.recordedAt.toLocal())
                                  : '—',
                              icon: Icons.login_outlined,
                            ),
                          ),
                          Expanded(
                            child: _MetricTile(
                              label: 'Out',
                              value: today?.checkOut != null
                                  ? timeFmt.format(
                                      today!.checkOut!.recordedAt.toLocal())
                                  : '—',
                              icon: Icons.logout_outlined,
                            ),
                          ),
                          Expanded(
                            child: _MetricTile(
                              label: 'Shift',
                              value: today?.duration != null
                                  ? _fmtDuration(today!.duration!)
                                  : '—',
                              icon: Icons.schedule_outlined,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 18),
                      SizedBox(
                        width: double.infinity,
                        height: 52,
                        child: FilledButton.icon(
                          onPressed: _actionLoading
                              ? null
                              : () => _handleAction(clockedIn),
                          style: FilledButton.styleFrom(
                            backgroundColor:
                                clockedIn ? AppColors.warning : _teal,
                            foregroundColor: Colors.white,
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(14),
                            ),
                          ),
                          icon: _actionLoading
                              ? const SizedBox(
                                  width: 18,
                                  height: 18,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : Icon(
                                  clockedIn
                                      ? Icons.logout_outlined
                                      : Icons.login_outlined,
                                ),
                          label: Text(
                            clockedIn ? l10n.checkOutCaps : l10n.checkInCaps,
                            style: const TextStyle(
                              fontWeight: FontWeight.w700,
                              fontSize: 16,
                            ),
                          ),
                        ),
                      ),
                      if (today?.checkIn?.latitude != null) ...[
                        const SizedBox(height: 10),
                        Row(
                          children: [
                            Icon(Icons.location_on_outlined,
                                size: 16, color: context.sg.textSecondary),
                            const SizedBox(width: 4),
                            Text(
                              'Location verified at clock-in',
                              style: TextStyle(
                                fontSize: 12,
                                color: context.sg.textSecondary,
                              ),
                            ),
                          ],
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(height: 14),
                _Card(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Overview',
                        style: TextStyle(
                          color: context.sg.textPrimary,
                          fontWeight: FontWeight.w700,
                          fontSize: 16,
                        ),
                      ),
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          Expanded(
                            child: _OverviewStat(
                              label: 'Present',
                              value: '${overview.presentDays}',
                              color: _emerald,
                            ),
                          ),
                          Expanded(
                            child: _OverviewStat(
                              label: 'Absent',
                              value: '${overview.absentDays}',
                              color: AppColors.error,
                            ),
                          ),
                          Expanded(
                            child: _OverviewStat(
                              label: 'Late',
                              value: '${overview.lateDays}',
                              color: AppColors.warning,
                            ),
                          ),
                          Expanded(
                            child: _OverviewStat(
                              label: 'Hours',
                              value: overview.totalHours.toStringAsFixed(1),
                              color: _teal,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 14),
                _Card(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Text(
                            'History',
                            style: TextStyle(
                              color: context.sg.textPrimary,
                              fontWeight: FontWeight.w700,
                              fontSize: 16,
                            ),
                          ),
                          const Spacer(),
                          TextButton(
                            onPressed: () => _requestCorrection(DateTime.now()),
                            child: const Text('Request correction'),
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      _MonthCalendar(
                        month: _calendarMonth,
                        days: days,
                        onPrev: () => setState(() {
                          _calendarMonth = DateTime(
                            _calendarMonth.year,
                            _calendarMonth.month - 1,
                          );
                        }),
                        onNext: () => setState(() {
                          _calendarMonth = DateTime(
                            _calendarMonth.year,
                            _calendarMonth.month + 1,
                          );
                        }),
                      ),
                      const SizedBox(height: 12),
                      if (days.isEmpty)
                        Padding(
                          padding: const EdgeInsets.symmetric(vertical: 24),
                          child: Center(
                            child: Text(
                              l10n.noAttendanceRecords,
                              style: TextStyle(color: context.sg.textSecondary),
                            ),
                          ),
                        )
                      else
                        ...days.take(14).map((d) => _HistoryTile(day: d)),
                    ],
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

// ── Team (manager / admin) ──────────────────────────────────────────────────

class _TeamAttendanceView extends ConsumerStatefulWidget {
  const _TeamAttendanceView();

  @override
  ConsumerState<_TeamAttendanceView> createState() =>
      _TeamAttendanceViewState();
}

class _TeamAttendanceViewState extends ConsumerState<_TeamAttendanceView> {
  final _search = TextEditingController();

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final current =
        ref.read(teamAttendanceProvider).valueOrNull?.date ?? DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: current,
      firstDate: DateTime.now().subtract(const Duration(days: 365)),
      lastDate: DateTime.now().add(const Duration(days: 1)),
    );
    if (picked != null) {
      await ref.read(teamAttendanceProvider.notifier).setDate(picked);
    }
  }

  Future<void> _correct(TeamAttendanceRow row) async {
    final session = ref.read(fleetSessionProvider);
    if (session == null ||
        !session.hasPermission(FleetPermissions.driverUpdate)) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(ForbiddenException.attendance.message),
          backgroundColor: AppColors.error,
        ),
      );
      return;
    }
    final notesCtrl = TextEditingController();
    String status = row.isPresent ? 'Present' : 'Absent';
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setLocal) => AlertDialog(
          title: Text('Correct · ${row.fullName}'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<String>(
                initialValue: status,
                items: const [
                  DropdownMenuItem(value: 'Present', child: Text('Present')),
                  DropdownMenuItem(value: 'Absent', child: Text('Absent')),
                  DropdownMenuItem(value: 'Late', child: Text('Late')),
                ],
                onChanged: (v) => setLocal(() => status = v ?? status),
                decoration: const InputDecoration(labelText: 'Status'),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: notesCtrl,
                maxLines: 2,
                decoration: const InputDecoration(
                  labelText: 'Notes',
                  border: OutlineInputBorder(),
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Save'),
            ),
          ],
        ),
      ),
    );
    if (ok != true || !mounted) return;
    try {
      await ref.read(teamAttendanceProvider.notifier).applyCorrection(
            driverId: row.driverId,
            attendanceDate: row.attendanceDate,
            status: status,
            notes: notesCtrl.text.trim().isEmpty ? null : notesCtrl.text.trim(),
          );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Attendance updated'),
            backgroundColor: AppColors.success,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(attendanceErrorMessage(e)),
            backgroundColor: AppColors.error,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(teamAttendanceProvider);
    final dateFmt = DateFormat('EEE, d MMM yyyy');
    final timeFmt = DateFormat('HH:mm');

    return Scaffold(
      backgroundColor: _bg,
      appBar: AppBar(
        title: const Text('Team Attendance'),
        backgroundColor: Colors.white,
        foregroundColor: _navy,
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.calendar_today_outlined),
            onPressed: _pickDate,
          ),
          IconButton(
            icon: const Icon(Icons.refresh_outlined),
            onPressed: () =>
                ref.read(teamAttendanceProvider.notifier).refresh(),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: SgBrandLoader()),
        error: (e, _) => _AttendanceErrorBody(
          message: attendanceErrorMessage(e),
          onRetry: attendanceCanRetry(e)
              ? () => ref.read(teamAttendanceProvider.notifier).refresh()
              : null,
        ),
        data: (state) {
          final overview = state.overview;
          final rows = state.filtered;
          return RefreshIndicator(
            color: _teal,
            onRefresh: () =>
                ref.read(teamAttendanceProvider.notifier).refresh(),
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 28),
              children: [
                Text(
                  dateFmt.format(state.date),
                  style: TextStyle(
                    color: context.sg.textSecondary,
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: 12),
                _Card(
                  child: Row(
                    children: [
                      Expanded(
                        child: _OverviewStat(
                          label: 'Total',
                          value: '${overview.total}',
                          color: _navy,
                        ),
                      ),
                      Expanded(
                        child: _OverviewStat(
                          label: 'Present',
                          value: '${overview.present}',
                          color: _emerald,
                        ),
                      ),
                      Expanded(
                        child: _OverviewStat(
                          label: 'Absent',
                          value: '${overview.absent}',
                          color: AppColors.error,
                        ),
                      ),
                      Expanded(
                        child: _OverviewStat(
                          label: 'Late',
                          value: '${overview.late}',
                          color: AppColors.warning,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _search,
                  onChanged: (v) =>
                      ref.read(teamAttendanceProvider.notifier).setQuery(v),
                  onSubmitted: (v) =>
                      ref.read(teamAttendanceProvider.notifier).searchRemote(v),
                  decoration: InputDecoration(
                    hintText: 'Search drivers',
                    filled: true,
                    fillColor: Colors.white,
                    prefixIcon: const Icon(Icons.search_outlined),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(14),
                      borderSide: BorderSide.none,
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                if (rows.isEmpty)
                  _Card(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: 28),
                      child: Center(
                        child: Text(
                          'No drivers for this date',
                          style: TextStyle(color: context.sg.textSecondary),
                        ),
                      ),
                    ),
                  )
                else
                  ...rows.map((r) {
                    final color = r.isLate
                        ? AppColors.warning
                        : (r.isPresent ? _emerald : AppColors.error);
                    return Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: _Card(
                        child: InkWell(
                          onTap: () => _correct(r),
                          borderRadius: BorderRadius.circular(16),
                          child: Row(
                            children: [
                              CircleAvatar(
                                backgroundColor: color.withValues(alpha: 0.12),
                                child: Icon(Icons.person_outline, color: color),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      r.fullName,
                                      style: const TextStyle(
                                        fontWeight: FontWeight.w700,
                                        color: _navy,
                                      ),
                                    ),
                                    const SizedBox(height: 2),
                                    Text(
                                      [
                                        if (r.branchName != null &&
                                            r.branchName!.isNotEmpty)
                                          r.branchName!,
                                        if (r.checkInAt != null)
                                          'In ${timeFmt.format(r.checkInAt!.toLocal())}',
                                        if (r.checkOutAt != null)
                                          'Out ${timeFmt.format(r.checkOutAt!.toLocal())}',
                                        if (r.hoursWorked != null)
                                          '${r.hoursWorked!.toStringAsFixed(1)}h',
                                      ].join(' · '),
                                      style: TextStyle(
                                        fontSize: 12,
                                        color: context.sg.textSecondary,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              _StatusChip(
                                label: r.attendanceStatus,
                                color: color,
                              ),
                            ],
                          ),
                        ),
                      ),
                    );
                  }),
              ],
            ),
          );
        },
      ),
    );
  }
}

// ── Shared widgets ──────────────────────────────────────────────────────────

class _Card extends StatelessWidget {
  const _Card({required this.child});
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        boxShadow: [
          BoxShadow(
            color: _navy.withValues(alpha: 0.06),
            blurRadius: 16,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      child: child,
    );
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.label, required this.color});
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: color,
          fontSize: 12,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _MetricTile extends StatelessWidget {
  const _MetricTile({
    required this.label,
    required this.value,
    required this.icon,
  });
  final String label;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Icon(icon, color: _teal, size: 18),
        const SizedBox(height: 6),
        Text(
          value,
          style: const TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: 16,
            color: _navy,
          ),
        ),
        Text(
          label,
          style: TextStyle(fontSize: 12, color: context.sg.textSecondary),
        ),
      ],
    );
  }
}

class _OverviewStat extends StatelessWidget {
  const _OverviewStat({
    required this.label,
    required this.value,
    required this.color,
  });
  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          value,
          style: TextStyle(
            fontSize: 20,
            fontWeight: FontWeight.w800,
            color: color,
          ),
        ),
        const SizedBox(height: 2),
        Text(
          label,
          style: TextStyle(fontSize: 11, color: context.sg.textSecondary),
        ),
      ],
    );
  }
}

class _HistoryTile extends StatelessWidget {
  const _HistoryTile({required this.day});
  final AttendanceDaySummary day;

  @override
  Widget build(BuildContext context) {
    final timeFmt = DateFormat('HH:mm');
    final dateFmt = DateFormat('EEE, d MMM');
    final color = day.isPresent ? _emerald : AppColors.error;
    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: Container(
        width: 40,
        height: 40,
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.12),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Icon(
          day.isPresent ? Icons.check_circle_outline : Icons.highlight_off,
          color: color,
          size: 20,
        ),
      ),
      title: Text(
        dateFmt.format(day.date),
        style: const TextStyle(fontWeight: FontWeight.w600, color: _navy),
      ),
      subtitle: Text(
        [
          if (day.checkIn != null)
            'In ${timeFmt.format(day.checkIn!.recordedAt.toLocal())}',
          if (day.checkOut != null)
            'Out ${timeFmt.format(day.checkOut!.recordedAt.toLocal())}',
          if (day.duration != null) _fmtDuration(day.duration!),
        ].join(' · '),
        style: TextStyle(fontSize: 12, color: context.sg.textSecondary),
      ),
      trailing: _StatusChip(label: day.status, color: color),
    );
  }
}

class _MonthCalendar extends StatelessWidget {
  const _MonthCalendar({
    required this.month,
    required this.days,
    required this.onPrev,
    required this.onNext,
  });

  final DateTime month;
  final List<AttendanceDaySummary> days;
  final VoidCallback onPrev;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    final label = DateFormat('MMMM yyyy').format(month);
    final byDay = {
      for (final d in days)
        if (d.date.year == month.year && d.date.month == month.month)
          d.date.day: d,
    };
    final first = DateTime(month.year, month.month, 1);
    final daysInMonth = DateTime(month.year, month.month + 1, 0).day;
    final startWeekday = first.weekday % 7; // Sunday-first feel

    return Column(
      children: [
        Row(
          children: [
            IconButton(
              onPressed: onPrev,
              icon: const Icon(Icons.chevron_left),
            ),
            Expanded(
              child: Text(
                label,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  color: _navy,
                ),
              ),
            ),
            IconButton(
              onPressed: onNext,
              icon: const Icon(Icons.chevron_right),
            ),
          ],
        ),
        Row(
          children: ['S', 'M', 'T', 'W', 'T', 'F', 'S']
              .map(
                (d) => Expanded(
                  child: Center(
                    child: Text(
                      d,
                      style: TextStyle(
                        fontSize: 11,
                        color: context.sg.textSecondary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ),
              )
              .toList(),
        ),
        const SizedBox(height: 6),
        GridView.builder(
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          itemCount: startWeekday + daysInMonth,
          gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: 7,
            mainAxisSpacing: 4,
            crossAxisSpacing: 4,
          ),
          itemBuilder: (context, index) {
            if (index < startWeekday) return const SizedBox.shrink();
            final dayNum = index - startWeekday + 1;
            final summary = byDay[dayNum];
            Color? bg;
            Color fg = _navy;
            if (summary != null) {
              if (summary.isPresent) {
                bg = _emerald.withValues(alpha: 0.18);
                fg = _emerald;
              } else {
                bg = AppColors.error.withValues(alpha: 0.12);
                fg = AppColors.error;
              }
            }
            return Container(
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: bg,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(
                '$dayNum',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: fg,
                ),
              ),
            );
          },
        ),
      ],
    );
  }
}

class _AttendanceErrorBody extends StatelessWidget {
  const _AttendanceErrorBody({
    required this.message,
    required this.onRetry,
    this.retryLabel = 'Retry',
  });

  final String message;
  final VoidCallback? onRetry;
  final String retryLabel;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.error_outline,
                size: 48, color: context.sg.textSecondary),
            const SizedBox(height: 12),
            Text(
              message,
              textAlign: TextAlign.center,
              style: TextStyle(color: context.sg.textPrimary, height: 1.35),
            ),
            if (onRetry != null) ...[
              const SizedBox(height: 16),
              FilledButton(
                onPressed: onRetry,
                style: FilledButton.styleFrom(backgroundColor: _teal),
                child: Text(retryLabel),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

String _fmtDuration(Duration d) {
  final h = d.inHours;
  final m = d.inMinutes.remainder(60);
  if (h <= 0) return '${m}m';
  return '${h}h ${m}m';
}
