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

  factory AttendanceRecord.fromJson(Map<String, dynamic> json) {
    final type = (json['attendanceType'] ??
            json['AttendanceType'] ??
            json['type'] ??
            '')
        .toString();
    final recordedRaw =
        json['recordedAt'] ?? json['RecordedAt'] ?? json['eventTime'];
    return AttendanceRecord(
      id: (json['id'] as num?)?.toInt() ??
          (json['Id'] as num?)?.toInt() ??
          0,
      attendanceType: type,
      recordedAt: DateTime.tryParse(recordedRaw?.toString() ?? '') ??
          DateTime.now(),
      latitude: (json['latitude'] as num?)?.toDouble() ??
          (json['Latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble() ??
          (json['Longitude'] as num?)?.toDouble(),
      notes: (json['notes'] ?? json['Notes'])?.toString(),
    );
  }
}
