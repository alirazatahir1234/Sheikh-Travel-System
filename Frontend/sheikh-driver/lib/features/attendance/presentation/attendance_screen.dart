import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../domain/attendance_model.dart';
import 'attendance_notifier.dart';

class AttendanceScreen extends ConsumerStatefulWidget {
  const AttendanceScreen({super.key});

  @override
  ConsumerState<AttendanceScreen> createState() => _AttendanceScreenState();
}

class _AttendanceScreenState extends ConsumerState<AttendanceScreen> {
  bool _actionLoading = false;

  bool _isClockedIn(List<AttendanceRecord> records) {
    if (records.isEmpty) return false;
    final today = DateFormat('yyyy-MM-dd').format(DateTime.now());
    final todayRecords = records
        .where((r) => DateFormat('yyyy-MM-dd').format(r.recordedAt.toLocal()) == today)
        .toList()
      ..sort((a, b) => b.recordedAt.compareTo(a.recordedAt));
    if (todayRecords.isEmpty) return false;
    return todayRecords.first.isCheckIn;
  }

  Future<Position?> _getLocation() async {
    try {
      final perm = await Geolocator.requestPermission();
      if (perm == LocationPermission.denied || perm == LocationPermission.deniedForever) {
        return null;
      }
      return await Geolocator.getCurrentPosition();
    } catch (_) {
      return null;
    }
  }

  Future<void> _handleAction(bool clockedIn) async {
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
            content: Text(clockedIn ? 'Clocked out successfully' : 'Clocked in successfully'),
            backgroundColor: clockedIn ? AppColors.warning : AppColors.success,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.toString()), backgroundColor: AppColors.error),
        );
      }
    } finally {
      if (mounted) setState(() => _actionLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final historyAsync = ref.watch(attendanceHistoryProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Attendance'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.read(attendanceHistoryProvider.notifier).refresh(),
          ),
        ],
      ),
      body: historyAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => _ErrorView(
          message: e.toString(),
          onRetry: () => ref.read(attendanceHistoryProvider.notifier).refresh(),
        ),
        data: (records) {
          final clockedIn = _isClockedIn(records);
          return Column(
            children: [
              _ClockWidget(
                clockedIn: clockedIn,
                loading: _actionLoading,
                onTap: () => _handleAction(clockedIn),
              ),
              const Divider(height: 1),
              Expanded(
                child: records.isEmpty
                    ? const Center(
                        child: Text('No attendance records',
                            style: TextStyle(color: AppColors.textSecondary)),
                      )
                    : _HistoryList(records: records),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _ClockWidget extends StatelessWidget {
  const _ClockWidget({
    required this.clockedIn,
    required this.loading,
    required this.onTap,
  });

  final bool clockedIn;
  final bool loading;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final now = DateTime.now();
    final timeFmt = DateFormat('HH:mm');
    final dateFmt = DateFormat('EEEE, d MMMM');
    final color = clockedIn ? AppColors.success : AppColors.primary;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: 32, horizontal: 24),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.05),
        border: Border(bottom: BorderSide(color: color.withValues(alpha: 0.15))),
      ),
      child: Column(
        children: [
          Text(
            timeFmt.format(now),
            style: TextStyle(
              fontSize: 52,
              fontWeight: FontWeight.w700,
              color: color,
              height: 1,
            ),
          ),
          const SizedBox(height: 4),
          Text(dateFmt.format(now),
              style: const TextStyle(color: AppColors.textSecondary, fontSize: 14)),
          const SizedBox(height: 24),
          SizedBox(
            width: 200,
            height: 52,
            child: FilledButton.icon(
              onPressed: loading ? null : onTap,
              style: FilledButton.styleFrom(
                backgroundColor: clockedIn ? AppColors.warning : AppColors.success,
              ),
              icon: loading
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                    )
                  : Icon(clockedIn ? Icons.logout : Icons.login),
              label: Text(
                clockedIn ? 'Clock Out' : 'Clock In',
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
              ),
            ),
          ),
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  width: 8,
                  height: 8,
                  decoration: BoxDecoration(color: color, shape: BoxShape.circle),
                ),
                const SizedBox(width: 6),
                Text(
                  clockedIn ? 'Currently clocked in' : 'Not clocked in',
                  style: TextStyle(color: color, fontSize: 13, fontWeight: FontWeight.w500),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _HistoryList extends StatelessWidget {
  const _HistoryList({required this.records});
  final List<AttendanceRecord> records;

  @override
  Widget build(BuildContext context) {
    // Group by date
    final Map<String, List<AttendanceRecord>> grouped = {};
    for (final r in records) {
      final key = DateFormat('EEEE, d MMMM yyyy').format(r.recordedAt.toLocal());
      grouped.putIfAbsent(key, () => []).add(r);
    }

    final dates = grouped.keys.toList();
    return ListView.builder(
      padding: const EdgeInsets.symmetric(vertical: 8),
      itemCount: dates.length,
      itemBuilder: (context, i) {
        final date = dates[i];
        final dayRecords = grouped[date]!;
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 6),
              child: Text(date,
                  style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary)),
            ),
            ...dayRecords.map((r) => _RecordTile(record: r)),
          ],
        );
      },
    );
  }
}

class _RecordTile extends StatelessWidget {
  const _RecordTile({required this.record});
  final AttendanceRecord record;

  @override
  Widget build(BuildContext context) {
    final timeFmt = DateFormat('HH:mm');
    final color = record.isCheckIn ? AppColors.success : AppColors.warning;
    final icon = record.isCheckIn ? Icons.login : Icons.logout;
    final label = record.isCheckIn ? 'Check In' : 'Check Out';

    return ListTile(
      leading: Container(
        width: 40,
        height: 40,
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Icon(icon, color: color, size: 20),
      ),
      title: Text(label,
          style: const TextStyle(fontWeight: FontWeight.w600, color: AppColors.textPrimary)),
      subtitle: record.latitude != null
          ? Text('GPS: ${record.latitude!.toStringAsFixed(4)}, ${record.longitude!.toStringAsFixed(4)}',
              style: const TextStyle(fontSize: 12, color: AppColors.textSecondary))
          : null,
      trailing: Text(
        timeFmt.format(record.recordedAt.toLocal()),
        style: TextStyle(color: color, fontWeight: FontWeight.w700, fontSize: 16),
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 48, color: AppColors.textSecondary),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 16),
            FilledButton(onPressed: onRetry, child: const Text('Retry')),
          ],
        ),
      );
}
