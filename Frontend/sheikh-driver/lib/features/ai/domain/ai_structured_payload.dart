import 'dart:convert';

/// Parsed fleet summary for chat cards (from backend JSON only).
class AiFleetStatusSummary {
  const AiFleetStatusSummary({
    required this.total,
    required this.online,
    required this.offline,
    required this.moving,
    required this.idle,
    required this.parked,
    required this.overallMessage,
    required this.vehicles,
  });

  final int total;
  final int online;
  final int offline;
  final int moving;
  final int idle;
  final int parked;
  final String? overallMessage;
  final List<AiFleetVehicleRow> vehicles;
}

class AiFleetVehicleRow {
  const AiFleetVehicleRow({
    required this.registrationNumber,
    required this.gpsBucket,
    this.vehicleName,
    this.speed,
    this.lastUpdate,
    this.statusLabel,
  });

  final String registrationNumber;
  final String gpsBucket;
  final String? vehicleName;
  final double? speed;
  final String? lastUpdate;
  final String? statusLabel;
}

/// Alert row derived from structured fleet / health payloads (real fields only).
class AiAssistantAlertCard {
  const AiAssistantAlertCard({
    required this.title,
    required this.severity,
    this.vehicleLabel,
    this.location,
    this.timeLabel,
  });

  final String title;
  final String severity;
  final String? vehicleLabel;
  final String? location;
  final String? timeLabel;
}

class AiStructuredAssistantData {
  const AiStructuredAssistantData({
    this.fleetStatus,
    this.alerts = const [],
    this.errorMessage,
  });

  final AiFleetStatusSummary? fleetStatus;
  final List<AiAssistantAlertCard> alerts;
  final String? errorMessage;

  bool get hasWidgets => fleetStatus != null || alerts.isNotEmpty;
}

int? _asInt(dynamic v) {
  if (v is num) return v.toInt();
  if (v is String && v.trim().isNotEmpty) return int.tryParse(v);
  return null;
}

double? _asDouble(dynamic v) {
  if (v is num) return v.toDouble();
  if (v is String && v.trim().isNotEmpty) return double.tryParse(v);
  return null;
}

String? _asString(dynamic v) =>
    v == null ? null : v.toString().trim().isEmpty ? null : v.toString().trim();

AiStructuredAssistantData? parseAiStructuredPayload(
  String? json, {
  String? toolUsed,
}) {
  if (json == null || json.trim().isEmpty) return null;
  try {
    final decoded = jsonDecode(json);
    return parseAiStructuredData(decoded, toolUsed: toolUsed);
  } catch (_) {
    return null;
  }
}

/// Detect ERP / MCP fleet payloads (`responseType: fleet_status_analytics` or raw summary).
AiStructuredAssistantData? parseAiStructuredData(
  dynamic data, {
  String? toolUsed,
}) {
  if (data == null) return null;
  if (data is String) {
    return parseAiStructuredPayload(data, toolUsed: toolUsed);
  }
  if (data is! Map) {
    return null;
  }
  final root = Map<String, dynamic>.from(data);
  if (root['error'] != null && root['data'] == null) {
    return AiStructuredAssistantData(
      errorMessage: _asString(root['error']) ?? _asString(root['message']),
    );
  }

  final nested = root['data'];
  if (nested is Map) {
    final inner = parseAiStructuredData(nested, toolUsed: toolUsed);
    if (inner != null) return inner;
  }

  final fleet = _parseFleetStatus(root, toolUsed);
  final healthAlerts = _alertsFromFleetHealth(root);
  final vehicleAlerts =
      fleet != null ? _alertsFromVehicles(fleet.vehicles) : <AiAssistantAlertCard>[];

  final alerts = [...healthAlerts, ...vehicleAlerts];
  if (fleet == null && alerts.isEmpty) return null;

  return AiStructuredAssistantData(
    fleetStatus: fleet,
    alerts: alerts,
  );
}

AiFleetStatusSummary? _parseFleetStatus(
  Map<String, dynamic> root,
  String? toolUsed,
) {
  final tool = (toolUsed ?? '').toLowerCase();
  final typed = _asString(root['responseType']) == 'fleet_status_analytics';
  final summaryObj = root['summary'];
  if (summaryObj is! Map) return null;
  final summary = Map<String, dynamic>.from(summaryObj);
  final total = _asInt(summary['total']);
  final looksRaw = total != null &&
      (_asInt(summary['online']) != null ||
          _asInt(summary['offline']) != null ||
          _asInt(summary['moving']) != null);
  final fleetTool =
      tool.contains('get_fleet_status') || tool.contains('getfleetstatus');
  if (!typed && !looksRaw && !(fleetTool && looksRaw)) return null;
  if (total == null) return null;
  final online = _asInt(summary['online']) ?? 0;
  final offline = _asInt(summary['offline']) ?? 0;
  final moving = _asInt(summary['moving']) ?? 0;
  final idle = _asInt(summary['idle']) ?? 0;
  final parked = _asInt(summary['parked']) ?? 0;

  final vehicles = <AiFleetVehicleRow>[];
  final rawVehicles = root['vehicles'];
  if (rawVehicles is List) {
    for (final row in rawVehicles) {
      if (row is! Map) continue;
      final m = Map<String, dynamic>.from(row);
      final reg = _asString(m['registrationNumber']) ??
          _asString(m['registration']) ??
          _asString(m['plate']);
      if (reg == null) continue;
      vehicles.add(
        AiFleetVehicleRow(
          registrationNumber: reg,
          gpsBucket: (_asString(m['gpsBucket']) ?? 'unknown').toLowerCase(),
          vehicleName: _asString(m['vehicleName']) ?? _asString(m['name']),
          speed: _asDouble(m['speed']),
          lastUpdate: _asString(m['lastUpdate']) ?? _asString(m['lastSeen']),
          statusLabel: _asString(m['statusLabel']),
        ),
      );
    }
  }

  String? overall;
  final acein = root['acein'];
  if (acein is Map) {
    overall = _asString(acein['answer']);
  }
  overall ??= _asString(root['summaryText']);
  if (overall == null && offline == 0 && total > 0) {
    overall = 'Overall status: All vehicles are operational.';
  }

  if (!typed && total == 0 && online == 0 && offline == 0) {
    return null;
  }

  return AiFleetStatusSummary(
    total: total,
    online: online,
    offline: offline,
    moving: moving,
    idle: idle,
    parked: parked,
    overallMessage: overall,
    vehicles: vehicles,
  );
}

List<AiAssistantAlertCard> _alertsFromFleetHealth(Map<String, dynamic> root) {
  final health = root['healthPercent'] != null || root['criticalAlerts'] != null
      ? root
      : null;
  if (health == null) return const [];
  final critical = _asInt(health['criticalAlerts']) ?? 0;
  if (critical <= 0) return const [];
  return [
    AiAssistantAlertCard(
      title: 'Critical alerts',
      severity: 'Critical',
      timeLabel: '$critical active',
    ),
  ];
}

List<AiAssistantAlertCard> _alertsFromVehicles(List<AiFleetVehicleRow> vehicles) {
  final out = <AiAssistantAlertCard>[];
  for (final v in vehicles) {
    switch (v.gpsBucket) {
      case 'sos':
        out.add(
          AiAssistantAlertCard(
            title: v.statusLabel ?? 'SOS alert',
            severity: 'Critical',
            vehicleLabel: v.registrationNumber,
            timeLabel: v.lastUpdate,
          ),
        );
      case 'moving':
        final speed = v.speed;
        if (speed != null && speed >= 100) {
          out.add(
            AiAssistantAlertCard(
              title: 'Over speeding',
              severity: 'Critical',
              vehicleLabel: v.registrationNumber,
              timeLabel: v.lastUpdate,
            ),
          );
        }
      default:
        break;
    }
  }
  return out;
}
