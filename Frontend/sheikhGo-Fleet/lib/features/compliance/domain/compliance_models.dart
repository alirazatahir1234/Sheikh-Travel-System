class ComplianceDocument {
  const ComplianceDocument({
    required this.id,
    required this.entityType,
    required this.documentType,
    required this.status,
    this.entityName,
    this.documentNumber,
    this.issuedDate,
    this.expiryDate,
    this.fileUrl,
  });

  final int id;
  final String entityType;
  final String? entityName;
  final String documentType;
  final String? documentNumber;
  final DateTime? issuedDate;
  final DateTime? expiryDate;
  final String status;
  final String? fileUrl;

  bool get isExpired => status.toLowerCase().contains('expired');
  bool get isExpiring =>
      status.toLowerCase().contains('expir') && !isExpired;

  factory ComplianceDocument.fromJson(Map<String, dynamic> json) {
    return ComplianceDocument(
      id: json['id'] as int? ?? json['Id'] as int? ?? 0,
      entityType:
          json['entityType'] as String? ?? json['EntityType'] as String? ?? '',
      entityName: json['entityName'] as String? ?? json['EntityName'] as String?,
      documentType: json['documentType'] as String? ??
          json['DocumentType'] as String? ??
          '',
      documentNumber: json['documentNumber'] as String? ??
          json['DocumentNumber'] as String?,
      issuedDate: DateTime.tryParse(
        (json['issuedDate'] ?? json['IssuedDate'] ?? '').toString(),
      ),
      expiryDate: DateTime.tryParse(
        (json['expiryDate'] ?? json['ExpiryDate'] ?? '').toString(),
      ),
      status: json['status'] as String? ?? json['Status'] as String? ?? '',
      fileUrl: json['fileUrl'] as String? ?? json['FileUrl'] as String?,
    );
  }
}

class ComplianceSummary {
  const ComplianceSummary({
    this.expired = 0,
    this.expiring7Days = 0,
    this.expiring15Days = 0,
    this.expiring30Days = 0,
  });

  final int expired;
  final int expiring7Days;
  final int expiring15Days;
  final int expiring30Days;

  factory ComplianceSummary.fromJson(Map<String, dynamic> json) {
    int n(String a, [String? b]) =>
        json[a] as int? ?? (b != null ? json[b] as int? : null) ?? 0;
    return ComplianceSummary(
      expired: n('expired', 'Expired'),
      expiring7Days: n('expiring7Days', 'Expiring7Days'),
      expiring15Days: n('expiring15Days', 'Expiring15Days'),
      expiring30Days: n('expiring30Days', 'Expiring30Days'),
    );
  }

  static const empty = ComplianceSummary();
}
