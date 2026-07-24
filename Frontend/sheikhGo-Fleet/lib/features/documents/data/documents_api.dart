import 'dart:io';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/offline/offline_models.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../domain/document_models.dart';

final documentsApiProvider =
    Provider<DocumentsApi>((ref) => DocumentsApi(ref.read(dioProvider), ref));

class DocumentsApi {
  DocumentsApi(this._dio, this._ref);
  final Dio _dio;
  final Ref _ref;

  Future<DocumentsBundle> getDocuments() async {
    final res = await _dio.get<Map<String, dynamic>>(ApiEndpoints.documents);
    final data = res.data?['data'] as Map<String, dynamic>? ?? {};
    return DocumentsBundle.fromJson(data);
  }

  Future<DriverDocument> upload({
    required String documentType,
    required File file,
    DateTime? expiryDate,
    int? vehicleId,
  }) async {
    return _ref.read(offlineSyncProvider).runOrQueue<DriverDocument>(
          online: () async {
            final form = FormData.fromMap({
              'documentType': documentType,
              if (expiryDate != null) 'expiryDate': expiryDate.toIso8601String(),
              if (vehicleId != null) 'vehicleId': vehicleId,
              'file': await MultipartFile.fromFile(
                file.path,
                filename: file.uri.pathSegments.last,
              ),
            });

            final res = await _dio.post<Map<String, dynamic>>(
              ApiEndpoints.documentsUpload,
              data: form,
            );
            final data = res.data?['data'] as Map<String, dynamic>? ?? {};
            return DriverDocument.fromJson(data);
          },
          type: OfflineOpType.documentUpload,
          payload: {
            'documentType': documentType,
            if (expiryDate != null) 'expiryDate': expiryDate.toIso8601String(),
            if (vehicleId != null) 'vehicleId': vehicleId,
            'filePath': file.path,
          },
          filePaths: [file.path],
          queuedValue: (_) => DriverDocument(
                id: 0,
                scope: 'Driver',
                documentType: documentType,
                title: '$documentType (pending sync)',
                previewUrl: null,
                expiryDate: expiryDate,
                status: 'PendingSync',
                isExpired: false,
                isExpiringSoon: false,
                daysUntilExpiry: null,
                canUpload: true,
                vehicleId: vehicleId,
              ),
        );
  }
}
