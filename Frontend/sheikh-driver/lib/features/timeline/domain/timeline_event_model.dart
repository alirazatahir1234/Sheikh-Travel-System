class TimelineEvent {
  const TimelineEvent({
    required this.id,
    required this.eventType,
    required this.title,
    required this.eventTime,
    this.description,
    this.status,
    this.referenceId,
  });

  final int id;
  final String eventType;
  final String title;
  final DateTime eventTime;
  final String? description;
  final String? status;
  final int? referenceId;

  factory TimelineEvent.fromJson(Map<String, dynamic> json) => TimelineEvent(
        id: json['id'] as int? ?? 0,
        eventType: json['eventType'] as String? ?? '',
        title: json['title'] as String? ?? '',
        eventTime: DateTime.tryParse(json['eventTime']?.toString() ?? '') ?? DateTime.now(),
        description: json['description'] as String?,
        status: json['status'] as String?,
        referenceId: json['referenceId'] as int?,
      );
}
