enum OfflineOpType {
  attendanceCheckIn,
  attendanceCheckOut,
  tripAdvance,
  fuelSubmit,
  inspectionSubmit,
  documentUpload,
  paymentCollect,
}

enum OfflineOpStatus {
  pending,
  syncing,
  failed,
  conflict,
}

class OfflineOperation {
  OfflineOperation({
    required this.id,
    required this.type,
    required this.payload,
    required this.createdAt,
    this.filePaths = const [],
    this.attempts = 0,
    this.lastError,
    this.status = OfflineOpStatus.pending,
  });

  final String id;
  final OfflineOpType type;
  final Map<String, dynamic> payload;
  final List<String> filePaths;
  final DateTime createdAt;
  int attempts;
  String? lastError;
  OfflineOpStatus status;

  String get label => switch (type) {
        OfflineOpType.attendanceCheckIn => 'Attendance check-in',
        OfflineOpType.attendanceCheckOut => 'Attendance check-out',
        OfflineOpType.tripAdvance =>
          'Trip ${payload['action'] ?? 'action'} (#${payload['tripId'] ?? '?'})',
        OfflineOpType.fuelSubmit => 'Fuel receipt',
        OfflineOpType.inspectionSubmit => 'Vehicle inspection',
        OfflineOpType.documentUpload =>
          'Document ${payload['documentType'] ?? 'upload'}',
        OfflineOpType.paymentCollect =>
          'Payment collect (#${payload['tripId'] ?? '?'})',
      };

  Map<String, dynamic> toMap() => {
        'id': id,
        'type': type.name,
        'payload': payload,
        'filePaths': filePaths,
        'createdAt': createdAt.toIso8601String(),
        'attempts': attempts,
        'lastError': lastError,
        'status': status.name,
      };

  factory OfflineOperation.fromMap(Map map) {
    final typeName = map['type']?.toString() ?? '';
    final statusName = map['status']?.toString() ?? 'pending';
    return OfflineOperation(
      id: map['id']?.toString() ?? '',
      type: OfflineOpType.values.firstWhere(
        (e) => e.name == typeName,
        orElse: () => OfflineOpType.tripAdvance,
      ),
      payload: Map<String, dynamic>.from(
        (map['payload'] as Map?)?.map((k, v) => MapEntry(k.toString(), v)) ??
            {},
      ),
      filePaths: ((map['filePaths'] as List?) ?? [])
          .map((e) => e.toString())
          .toList(),
      createdAt:
          DateTime.tryParse(map['createdAt']?.toString() ?? '') ?? DateTime.now(),
      attempts: (map['attempts'] as num?)?.toInt() ?? 0,
      lastError: map['lastError']?.toString(),
      status: OfflineOpStatus.values.firstWhere(
        (e) => e.name == statusName,
        orElse: () => OfflineOpStatus.pending,
      ),
    );
  }
}

class OfflineQueuedException implements Exception {
  OfflineQueuedException(this.message, {this.operationId});
  final String message;
  final String? operationId;

  @override
  String toString() => message;
}
