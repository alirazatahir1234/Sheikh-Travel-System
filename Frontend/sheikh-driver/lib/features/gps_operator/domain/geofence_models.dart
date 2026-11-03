DateTime? _date(dynamic v) =>
    v is String ? DateTime.tryParse(v.endsWith('Z') ? v : '${v}Z')?.toUtc() : null;

T? _pick<T>(Map<String, dynamic> json, String camel) {
  final pascal = camel[0].toUpperCase() + camel.substring(1);
  final v = json[camel] ?? json[pascal];
  return v is T ? v : null;
}

/// "just now" / "5 min ago" / "3 h ago" / "2 d ago" for position freshness.
String formatFreshness(DateTime? utc, {DateTime? now}) {
  if (utc == null) return 'no position';
  final diff = (now ?? DateTime.now().toUtc()).difference(utc.toUtc());
  if (diff.inMinutes < 1) return 'just now';
  if (diff.inMinutes < 60) return '${diff.inMinutes} min ago';
  if (diff.inHours < 24) return '${diff.inHours} h ago';
  return '${diff.inDays} d ago';
}

/// Positions older than this are shown as stale.
const geofenceStaleAfter = Duration(minutes: 15);

/// Server-evaluated inside/outside state (`GET gps/geofences/states`).
class GeofenceVehicleState {
  const GeofenceVehicleState({
    required this.geofenceId,
    required this.vehicleId,
    required this.isInside,
    this.vehicleName,
    this.registrationNumber,
    this.sinceUtc,
    this.lastEvaluatedUtc,
    this.lastPositionUtc,
  });

  final int geofenceId;
  final int vehicleId;
  final bool isInside;
  final String? vehicleName;
  final String? registrationNumber;
  final DateTime? sinceUtc;
  final DateTime? lastEvaluatedUtc;
  final DateTime? lastPositionUtc;

  String get vehicleLabel {
    final reg = registrationNumber?.trim() ?? '';
    final name = vehicleName?.trim() ?? '';
    if (name.isNotEmpty && reg.isNotEmpty) return '$name · $reg';
    if (reg.isNotEmpty) return reg;
    if (name.isNotEmpty) return name;
    return 'Vehicle #$vehicleId';
  }

  factory GeofenceVehicleState.fromJson(Map<String, dynamic> json) {
    return GeofenceVehicleState(
      geofenceId: _pick<int>(json, 'geofenceId') ?? 0,
      vehicleId: _pick<int>(json, 'vehicleId') ?? 0,
      isInside: _pick<bool>(json, 'isInside') ?? false,
      vehicleName: _pick<String>(json, 'vehicleName'),
      registrationNumber: _pick<String>(json, 'registrationNumber'),
      sinceUtc: _date(_pick<String>(json, 'sinceUtc')),
      lastEvaluatedUtc: _date(_pick<String>(json, 'lastEvaluatedUtc')),
      lastPositionUtc: _date(_pick<String>(json, 'lastPositionUtc')),
    );
  }
}

class GeofenceAssignment {
  const GeofenceAssignment({
    required this.id,
    this.vehicleId,
    this.vehicleName,
    this.registrationNumber,
    this.branchName,
    this.departmentName,
  });

  final int id;
  final int? vehicleId;
  final String? vehicleName;
  final String? registrationNumber;
  final String? branchName;
  final String? departmentName;

  String get label {
    if (vehicleId != null) {
      final reg = registrationNumber?.trim() ?? '';
      final name = vehicleName?.trim() ?? '';
      if (name.isNotEmpty && reg.isNotEmpty) return '$name · $reg';
      return reg.isNotEmpty ? reg : (name.isNotEmpty ? name : 'Vehicle #$vehicleId');
    }
    if ((branchName ?? '').isNotEmpty) return 'Branch: $branchName';
    if ((departmentName ?? '').isNotEmpty) return 'Department: $departmentName';
    return 'Assignment #$id';
  }

  factory GeofenceAssignment.fromJson(Map<String, dynamic> json) {
    return GeofenceAssignment(
      id: _pick<int>(json, 'id') ?? 0,
      vehicleId: _pick<int>(json, 'vehicleId'),
      vehicleName: _pick<String>(json, 'vehicleName'),
      registrationNumber: _pick<String>(json, 'registrationNumber'),
      branchName: _pick<String>(json, 'branchName'),
      departmentName: _pick<String>(json, 'departmentName'),
    );
  }
}

/// Body for `POST/PUT gps/geofences` (matches Create/UpdateGeofenceDto).
class GeofenceDraft {
  const GeofenceDraft({
    required this.name,
    required this.category,
    required this.areaType,
    this.centerLat = 0,
    this.centerLng = 0,
    this.radiusMeters = 0,
    this.geoJson,
    this.description,
    this.color,
    this.isActive = true,
    this.alertOnEnter = true,
    this.alertOnExit = true,
    this.vehicleIds = const [],
  });

  final String name;
  final String category;
  final String areaType;
  final double centerLat;
  final double centerLng;
  final double radiusMeters;
  final String? geoJson;
  final String? description;
  final String? color;
  final bool isActive;
  final bool alertOnEnter;
  final bool alertOnExit;
  final List<int> vehicleIds;

  Map<String, dynamic> toJson() => {
        'name': name,
        'category': category,
        'areaType': areaType,
        'centerLat': centerLat,
        'centerLng': centerLng,
        'radiusMeters': radiusMeters,
        'geoJson': geoJson,
        'description': description,
        'color': color,
        'isActive': isActive,
        'alertOnEnter': alertOnEnter,
        'alertOnExit': alertOnExit,
        'vehicleIds': vehicleIds,
      };
}

/// Client-side mirror of the server geometry rules (GpsGeoHelper).
const geofenceMinRadiusMeters = 50.0;
const geofenceMaxRadiusMeters = 50000.0;

String? validateGeofenceGeometry({
  required bool circle,
  double? radiusMeters,
  bool hasCenter = false,
  List<({double lat, double lng})> vertices = const [],
}) {
  if (circle) {
    if (!hasCenter) return 'Tap the map to place the zone centre.';
    final r = radiusMeters ?? 0;
    if (r < geofenceMinRadiusMeters || r > geofenceMaxRadiusMeters) {
      return 'Radius must be between 50 m and 50 km.';
    }
    return null;
  }
  if (vertices.toSet().length < 3) {
    return 'Add at least 3 distinct points to close the shape.';
  }
  return null;
}
