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

  bool get isCheckIn => attendanceType.toLowerCase() == 'checkin';

  factory AttendanceRecord.fromJson(Map<String, dynamic> json) {
    return AttendanceRecord(
      id: json['id'] as int? ?? 0,
      attendanceType: json['attendanceType'] as String? ?? '',
      recordedAt: DateTime.tryParse(json['recordedAt']?.toString() ?? '') ?? DateTime.now(),
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
      notes: json['notes'] as String?,
    );
  }
}
