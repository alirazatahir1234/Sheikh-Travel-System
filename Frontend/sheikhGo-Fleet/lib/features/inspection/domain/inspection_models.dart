class ChecklistItem {
  const ChecklistItem({
    required this.key,
    required this.label,
    required this.required,
  });

  final String key;
  final String label;
  final bool required;

  factory ChecklistItem.fromJson(Map<String, dynamic> json) => ChecklistItem(
        key: json['key'] as String? ?? '',
        label: json['label'] as String? ?? '',
        required: json['required'] as bool? ?? false,
      );
}

class InspectionTemplate {
  const InspectionTemplate({
    required this.id,
    required this.name,
    this.description,
    required this.items,
  });

  final int id;
  final String name;
  final String? description;
  final List<ChecklistItem> items;

  factory InspectionTemplate.fromJson(Map<String, dynamic> json) {
    final raw = json['items'] as List? ?? [];
    return InspectionTemplate(
      id: json['id'] as int? ?? 0,
      name: json['name'] as String? ?? '',
      description: json['description'] as String?,
      items: raw
          .cast<Map<String, dynamic>>()
          .map(ChecklistItem.fromJson)
          .toList(),
    );
  }
}

class InspectionVehicle {
  const InspectionVehicle({
    required this.id,
    required this.name,
    this.plate,
  });

  final int id;
  final String name;
  final String? plate;

  String get label => plate == null || plate!.isEmpty ? name : '$name ($plate)';

  factory InspectionVehicle.fromJson(Map<String, dynamic> json) =>
      InspectionVehicle(
        id: json['id'] as int,
        name: json['name'] as String? ?? '',
        plate: json['plate'] as String?,
      );
}

class InspectionResultItem {
  InspectionResultItem({
    required this.key,
    required this.status,
    this.comment,
  });

  final String key;
  String status; // Pass | Warning | Fail
  String? comment;

  Map<String, dynamic> toJson() => {
        'key': key,
        'status': status,
        'comment': comment,
      };
}

class InspectionSummary {
  const InspectionSummary({
    required this.id,
    required this.vehicleId,
    this.vehicleName,
    this.vehiclePlate,
    required this.inspectionDate,
    required this.result,
    this.odometerReading,
    this.comments,
    required this.photoCount,
    required this.hasSignature,
  });

  final int id;
  final int vehicleId;
  final String? vehicleName;
  final String? vehiclePlate;
  final DateTime inspectionDate;
  final String result;
  final double? odometerReading;
  final String? comments;
  final int photoCount;
  final bool hasSignature;

  factory InspectionSummary.fromJson(Map<String, dynamic> json) =>
      InspectionSummary(
        id: json['id'] as int,
        vehicleId: json['vehicleId'] as int,
        vehicleName: json['vehicleName'] as String?,
        vehiclePlate: json['vehiclePlate'] as String?,
        inspectionDate:
            DateTime.tryParse(json['inspectionDate']?.toString() ?? '') ??
                DateTime.now(),
        result: json['result'] as String? ?? '',
        odometerReading: (json['odometerReading'] as num?)?.toDouble(),
        comments: json['comments'] as String?,
        photoCount: json['photoCount'] as int? ?? 0,
        hasSignature: json['hasSignature'] as bool? ?? false,
      );
}
