class FleetReportColumn {
  const FleetReportColumn({
    required this.key,
    required this.label,
    required this.format,
  });

  final String key;
  final String label;
  final String format;

  factory FleetReportColumn.fromJson(Map<String, dynamic> json) {
    return FleetReportColumn(
      key: json['key'] as String? ?? json['Key'] as String? ?? '',
      label: json['label'] as String? ?? json['Label'] as String? ?? '',
      format: json['format'] as String? ?? json['Format'] as String? ?? 'text',
    );
  }
}

class FleetReportRow {
  const FleetReportRow({
    required this.key,
    required this.label,
    required this.count,
    required this.totalValue,
    required this.fields,
  });

  final String key;
  final String label;
  final int count;
  final double totalValue;
  final Map<String, dynamic> fields;

  factory FleetReportRow.fromJson(Map<String, dynamic> json) {
    final rawFields = json['fields'] ?? json['Fields'];
    final fields = <String, dynamic>{};
    if (rawFields is Map) {
      rawFields.forEach((k, v) => fields[k.toString()] = v);
    }
    return FleetReportRow(
      key: json['key'] as String? ?? json['Key'] as String? ?? '',
      label: json['label'] as String? ?? json['Label'] as String? ?? '',
      count: json['count'] as int? ?? json['Count'] as int? ?? 0,
      totalValue:
          (json['totalValue'] as num? ?? json['TotalValue'] as num? ?? 0)
              .toDouble(),
      fields: fields,
    );
  }
}

class FleetReport {
  const FleetReport({
    required this.reportType,
    required this.title,
    required this.columns,
    required this.rows,
    required this.totalValue,
    this.summary = const {},
  });

  final String reportType;
  final String title;
  final List<FleetReportColumn> columns;
  final List<FleetReportRow> rows;
  final double totalValue;
  final Map<String, dynamic> summary;

  factory FleetReport.fromJson(Map<String, dynamic> json) {
    final cols = json['columns'] ?? json['Columns'];
    final rows = json['rows'] ?? json['Rows'];
    final summaryRaw = json['summary'] ?? json['Summary'];
    final summary = <String, dynamic>{};
    if (summaryRaw is Map) {
      summaryRaw.forEach((k, v) => summary[k.toString()] = v);
    }
    return FleetReport(
      reportType:
          json['reportType'] as String? ?? json['ReportType'] as String? ?? '',
      title: json['title'] as String? ?? json['Title'] as String? ?? 'Report',
      columns: cols is List
          ? cols
              .whereType<Map>()
              .map((e) =>
                  FleetReportColumn.fromJson(Map<String, dynamic>.from(e)))
              .toList()
          : const [],
      rows: rows is List
          ? rows
              .whereType<Map>()
              .map((e) => FleetReportRow.fromJson(Map<String, dynamic>.from(e)))
              .toList()
          : const [],
      totalValue:
          (json['totalValue'] as num? ?? json['TotalValue'] as num? ?? 0)
              .toDouble(),
      summary: summary,
    );
  }

  static const empty = FleetReport(
    reportType: '',
    title: 'Report',
    columns: [],
    rows: [],
    totalValue: 0,
  );
}

class ReportTypeOption {
  const ReportTypeOption(this.id, this.label);
  final String id;
  final String label;
}

/// Sprint 4 curated set — mirrors fleet-reports reportType values.
const fleetReportTypeOptions = [
  ReportTypeOption('fuel', 'Fuel'),
  ReportTypeOption('vehicle', 'Vehicles'),
  ReportTypeOption('driver', 'Drivers'),
  ReportTypeOption('trip', 'Trips'),
  ReportTypeOption('maintenance', 'Maintenance'),
  ReportTypeOption('alert', 'Alerts'),
];
