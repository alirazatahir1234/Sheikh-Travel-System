class AttendanceRecord {
  const AttendanceRecord({
    required this.id,
    required this.attendanceType,
    required this.recordedAt,
    this.latitude,
    this.longitude,
    this.notes,
  });

  final int id;
  final String attendanceType;
  final DateTime recordedAt;
  final double? latitude;
  final double? longitude;
  final String? notes;

  bool get isCheckIn {
    final t = attendanceType.toLowerCase().replaceAll(RegExp(r'[\s_\-]'), '');
    return t == 'checkin' || t.startsWith('checkin');
  }

  bool get isCheckOut {
    final t = attendanceType.toLowerCase().replaceAll(RegExp(r'[\s_\-]'), '');
    return t == 'checkout' || t.startsWith('checkout');
  }

  bool get isCorrectionRequest {
    final t = attendanceType.toLowerCase().replaceAll(RegExp(r'[\s_\-]'), '');
    return t.contains('correction');
  }

  factory AttendanceRecord.fromJson(Map<String, dynamic> json) {
    final type =
        (json['attendanceType'] ?? json['AttendanceType'] ?? json['type'] ?? '')
            .toString();
    final recordedRaw =
        json['recordedAt'] ?? json['RecordedAt'] ?? json['eventTime'];
    return AttendanceRecord(
      id: (json['id'] as num?)?.toInt() ?? (json['Id'] as num?)?.toInt() ?? 0,
      attendanceType: type,
      recordedAt:
          DateTime.tryParse(recordedRaw?.toString() ?? '') ?? DateTime.now(),
      latitude: (json['latitude'] as num?)?.toDouble() ??
          (json['Latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble() ??
          (json['Longitude'] as num?)?.toDouble(),
      notes: (json['notes'] ?? json['Notes'])?.toString(),
    );
  }
}

class TeamAttendanceRow {
  const TeamAttendanceRow({
    required this.driverId,
    required this.fullName,
    required this.attendanceStatus,
    required this.attendanceDate,
    this.phone,
    this.branchName,
    this.driverStatus,
    this.attendanceId,
    this.checkInAt,
    this.checkOutAt,
    this.notes,
    this.hoursWorked,
  });

  final int driverId;
  final String fullName;
  final String? phone;
  final String? branchName;
  final String? driverStatus;
  final int? attendanceId;
  final DateTime attendanceDate;
  final String attendanceStatus;
  final DateTime? checkInAt;
  final DateTime? checkOutAt;
  final String? notes;
  final double? hoursWorked;

  bool get isPresent {
    final s = attendanceStatus.toLowerCase();
    return s.contains('present') || checkInAt != null;
  }

  bool get isAbsent {
    final s = attendanceStatus.toLowerCase();
    return s.contains('absent') || (checkInAt == null && !s.contains('late'));
  }

  bool get isLate {
    final s = attendanceStatus.toLowerCase();
    return s.contains('late');
  }

  factory TeamAttendanceRow.fromJson(Map<String, dynamic> json) {
    DateTime? parseDate(Object? raw) =>
        DateTime.tryParse(raw?.toString() ?? '');

    return TeamAttendanceRow(
      driverId: (json['driverId'] as num?)?.toInt() ??
          (json['DriverId'] as num?)?.toInt() ??
          0,
      fullName: (json['fullName'] ?? json['FullName'] ?? 'Driver').toString(),
      phone: (json['phone'] ?? json['Phone'])?.toString(),
      branchName: (json['branchName'] ?? json['BranchName'])?.toString(),
      driverStatus: (json['driverStatus'] ?? json['DriverStatus'])?.toString(),
      attendanceId: (json['attendanceId'] as num?)?.toInt() ??
          (json['AttendanceId'] as num?)?.toInt(),
      attendanceDate: parseDate(
            json['attendanceDate'] ?? json['AttendanceDate'],
          ) ??
          DateTime.now(),
      attendanceStatus:
          (json['attendanceStatus'] ?? json['AttendanceStatus'] ?? 'Absent')
              .toString(),
      checkInAt: parseDate(json['checkInAt'] ?? json['CheckInAt']),
      checkOutAt: parseDate(json['checkOutAt'] ?? json['CheckOutAt']),
      notes: (json['notes'] ?? json['Notes'])?.toString(),
      hoursWorked: (json['hoursWorked'] as num?)?.toDouble() ??
          (json['HoursWorked'] as num?)?.toDouble(),
    );
  }
}

class AttendanceDaySummary {
  const AttendanceDaySummary({
    required this.date,
    this.checkIn,
    this.checkOut,
    this.status = 'Absent',
  });

  final DateTime date;
  final AttendanceRecord? checkIn;
  final AttendanceRecord? checkOut;
  final String status;

  Duration? get duration {
    final a = checkIn?.recordedAt;
    final b = checkOut?.recordedAt;
    if (a == null || b == null) return null;
    final d = b.difference(a);
    return d.isNegative ? null : d;
  }

  bool get isPresent =>
      status.toLowerCase().contains('present') || checkIn != null;
  bool get isLate => status.toLowerCase().contains('late');
  bool get isAbsent => !isPresent && !isLate;
}

class AttendanceOverview {
  const AttendanceOverview({
    required this.presentDays,
    required this.absentDays,
    required this.lateDays,
    required this.totalHours,
  });

  final int presentDays;
  final int absentDays;
  final int lateDays;
  final double totalHours;
}

/// Groups punch events into daily summaries for history / calendar.
List<AttendanceDaySummary> groupAttendanceByDay(
    List<AttendanceRecord> records) {
  final map = <String, List<AttendanceRecord>>{};
  for (final r in records) {
    if (r.isCorrectionRequest) continue;
    final key =
        '${r.recordedAt.toLocal().year}-${r.recordedAt.toLocal().month}-${r.recordedAt.toLocal().day}';
    map.putIfAbsent(key, () => []).add(r);
  }

  final days = <AttendanceDaySummary>[];
  for (final entry in map.entries) {
    final list = entry.value
      ..sort((a, b) => a.recordedAt.compareTo(b.recordedAt));
    AttendanceRecord? checkIn;
    AttendanceRecord? checkOut;
    for (final r in list) {
      if (r.isCheckIn) checkIn ??= r;
      if (r.isCheckOut) checkOut = r;
    }
    final day = list.first.recordedAt.toLocal();
    days.add(
      AttendanceDaySummary(
        date: DateTime(day.year, day.month, day.day),
        checkIn: checkIn,
        checkOut: checkOut,
        status: checkIn != null ? 'Present' : 'Absent',
      ),
    );
  }
  days.sort((a, b) => b.date.compareTo(a.date));
  return days;
}

AttendanceOverview buildOverview(List<AttendanceDaySummary> days) {
  var present = 0;
  var absent = 0;
  var late = 0;
  var hours = 0.0;
  for (final d in days) {
    if (d.isLate) {
      late += 1;
    } else if (d.isPresent) {
      present += 1;
    } else {
      absent += 1;
    }
    final dur = d.duration;
    if (dur != null) hours += dur.inMinutes / 60.0;
  }
  return AttendanceOverview(
    presentDays: present,
    absentDays: absent,
    lateDays: late,
    totalHours: hours,
  );
}

class TeamAttendanceOverview {
  const TeamAttendanceOverview({
    required this.total,
    required this.present,
    required this.absent,
    required this.late,
  });

  final int total;
  final int present;
  final int absent;
  final int late;

  factory TeamAttendanceOverview.fromRows(List<TeamAttendanceRow> rows) {
    var present = 0;
    var absent = 0;
    var late = 0;
    for (final r in rows) {
      if (r.isLate) {
        late += 1;
      } else if (r.isPresent) {
        present += 1;
      } else {
        absent += 1;
      }
    }
    return TeamAttendanceOverview(
      total: rows.length,
      present: present,
      absent: absent,
      late: late,
    );
  }
}
