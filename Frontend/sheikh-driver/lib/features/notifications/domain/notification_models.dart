class AppNotification {
  const AppNotification({
    required this.id,
    required this.title,
    required this.message,
    required this.type,
    required this.isRead,
    required this.createdAt,
    this.module,
    this.referenceId,
    this.priority = 2,
    this.isArchived = false,
  });

  final int id;
  final String title;
  final String message;
  final String type;
  final bool isRead;
  final DateTime createdAt;
  final String? module;
  final int? referenceId;
  final int priority;
  final bool isArchived;

  String get category {
    final m = (module ?? '').trim();
    if (m.isNotEmpty) return m;
    final t = type.toLowerCase();
    if (t.contains('trip') || t.contains('booking')) return 'Trips';
    if (t.contains('payment') || t.contains('earning')) return 'Earnings';
    if (t.contains('fuel')) return 'Fuel';
    if (t.contains('sos')) return 'SOS';
    if (t.contains('vehicle') || t.contains('gps')) return 'Fleet';
    return 'System';
  }

  factory AppNotification.fromJson(Map<String, dynamic> json) {
    final typeRaw = json['type'];
    String typeName;
    if (typeRaw is int) {
      typeName = _typeName(typeRaw);
    } else {
      typeName = typeRaw?.toString() ?? 'System';
    }

    return AppNotification(
      id: json['id'] as int? ?? 0,
      title: json['title'] as String? ?? '',
      message: json['message'] as String? ?? '',
      type: typeName,
      isRead: json['isRead'] as bool? ?? false,
      createdAt:
          DateTime.tryParse(json['createdAt']?.toString() ?? '') ?? DateTime.now(),
      module: json['module'] as String?,
      referenceId: json['referenceId'] as int?,
      priority: (json['priority'] as num?)?.toInt() ?? 2,
      isArchived: json['isArchived'] as bool? ?? false,
    );
  }

  static String _typeName(int value) => switch (value) {
        1 => 'BookingCreated',
        2 => 'TripDelayed',
        3 => 'VehicleOffline',
        4 => 'PaymentReceived',
        5 => 'EngineCommandSent',
        6 => 'Sos',
        7 => 'TripDriverAssigned',
        8 => 'TripStarted',
        9 => 'TripCompleted',
        10 => 'TripCancelled',
        11 => 'TripUpdated',
        12 => 'TripDriverArriving',
        _ => 'System',
      };
}

class ForegroundBannerEvent {
  const ForegroundBannerEvent({
    required this.title,
    required this.body,
    this.route,
    this.notificationId,
  });

  final String title;
  final String body;
  final String? route;
  final int? notificationId;
}
