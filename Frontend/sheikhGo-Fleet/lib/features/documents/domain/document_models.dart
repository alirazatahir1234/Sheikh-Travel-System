class DriverDocument {
  const DriverDocument({
    this.id,
    required this.scope,
    required this.documentType,
    required this.title,
    this.previewUrl,
    this.expiryDate,
    required this.status,
    required this.isExpired,
    required this.isExpiringSoon,
    this.daysUntilExpiry,
    required this.canUpload,
    this.vehicleId,
    this.vehicleName,
  });

  final int? id;
  final String scope;
  final String documentType;
  final String title;
  final String? previewUrl;
  final DateTime? expiryDate;
  final String status;
  final bool isExpired;
  final bool isExpiringSoon;
  final int? daysUntilExpiry;
  final bool canUpload;
  final int? vehicleId;
  final String? vehicleName;

  bool get hasFile => previewUrl != null && previewUrl!.isNotEmpty;
  bool get needsAttention => isExpired || isExpiringSoon || status == 'Missing';

  factory DriverDocument.fromJson(Map<String, dynamic> json) => DriverDocument(
        id: json['id'] as int?,
        scope: json['scope'] as String? ?? 'Driver',
        documentType: json['documentType'] as String? ?? '',
        title: json['title'] as String? ?? '',
        previewUrl: json['previewUrl'] as String?,
        expiryDate: json['expiryDate'] != null
            ? DateTime.tryParse(json['expiryDate'].toString())
            : null,
        status: json['status'] as String? ?? 'Missing',
        isExpired: json['isExpired'] as bool? ?? false,
        isExpiringSoon: json['isExpiringSoon'] as bool? ?? false,
        daysUntilExpiry: json['daysUntilExpiry'] as int?,
        canUpload: json['canUpload'] as bool? ?? false,
        vehicleId: json['vehicleId'] as int?,
        vehicleName: json['vehicleName'] as String?,
      );
}

class DocumentsBundle {
  const DocumentsBundle({
    required this.documents,
    this.licenseExpiryDate,
    this.cnicNumber,
    required this.expiringCount,
    required this.expiredCount,
    required this.missingCount,
  });

  final List<DriverDocument> documents;
  final DateTime? licenseExpiryDate;
  final String? cnicNumber;
  final int expiringCount;
  final int expiredCount;
  final int missingCount;

  factory DocumentsBundle.fromJson(Map<String, dynamic> json) {
    final list = (json['documents'] as List?) ?? [];
    return DocumentsBundle(
      documents: list
          .cast<Map<String, dynamic>>()
          .map(DriverDocument.fromJson)
          .toList(),
      licenseExpiryDate: json['licenseExpiryDate'] != null
          ? DateTime.tryParse(json['licenseExpiryDate'].toString())
          : null,
      cnicNumber: json['cnicNumber'] as String?,
      expiringCount: json['expiringCount'] as int? ?? 0,
      expiredCount: json['expiredCount'] as int? ?? 0,
      missingCount: json['missingCount'] as int? ?? 0,
    );
  }
}
