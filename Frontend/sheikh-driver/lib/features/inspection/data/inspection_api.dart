import 'dart:convert';
import 'dart:io';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/offline/offline_file_store.dart';
import '../../../core/offline/offline_models.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../domain/inspection_models.dart';

final inspectionApiProvider =
    Provider<InspectionApi>((ref) => InspectionApi(ref.read(dioProvider), ref));

class InspectionApi {
  InspectionApi(this._dio, this._ref);
  final Dio _dio;
  final Ref _ref;

  Future<InspectionTemplate> getTemplate() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.inspectionTemplate);
    final data = res.data?['data'] as Map<String, dynamic>? ?? {};
    return InspectionTemplate.fromJson(data);
  }

  Future<List<InspectionVehicle>> getVehicles() async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.inspectionVehicles);
    final list = (res.data?['data'] as List?) ?? [];
    return list
        .cast<Map<String, dynamic>>()
        .map(InspectionVehicle.fromJson)
        .toList();
  }

  Future<List<InspectionSummary>> getHistory({int page = 1}) async {
    final res = await _dio.get<Map<String, dynamic>>(
      ApiEndpoints.inspectionHistory,
      queryParameters: {'page': page, 'pageSize': 30},
    );
    final list = (res.data?['data'] as List?) ?? [];
    return list
        .cast<Map<String, dynamic>>()
        .map(InspectionSummary.fromJson)
        .toList();
  }

  Future<int> submit({
    required int vehicleId,
    required int templateId,
    required List<InspectionResultItem> results,
    double? odometer,
    String? comments,
    List<File>? photos,
    File? signature,
  }) async {
    // Persist photos/signature early so offline queue keeps durable paths.
    final photoPaths = await OfflineFileStore.persistForOffline(
      photos?.map((f) => f.path).toList() ?? const [],
    );
    final sigPaths = signature != null
        ? await OfflineFileStore.persistForOffline([signature.path])
        : const <String>[];
    final sigPath = sigPaths.isNotEmpty ? sigPaths.first : null;

    final photoFiles = photoPaths.map(File.new).toList();
    final sigFile = sigPath != null ? File(sigPath) : null;

    return _ref.read(offlineSyncProvider).runOrQueue<int>(
          online: () async {
            final form = FormData.fromMap({
              'vehicleId': vehicleId,
              'templateId': templateId,
              if (odometer != null) 'odometerReading': odometer,
              if (comments != null && comments.isNotEmpty) 'comments': comments,
              'resultsJson':
                  jsonEncode(results.map((e) => e.toJson()).toList()),
            });

            for (final p in photoFiles.take(8)) {
              if (!await p.exists()) continue;
              form.files.add(MapEntry(
                'photos',
                await MultipartFile.fromFile(p.path,
                    filename: p.uri.pathSegments.last),
              ));
            }

            if (sigFile != null && await sigFile.exists()) {
              form.files.add(MapEntry(
                'signature',
                await MultipartFile.fromFile(sigFile.path,
                    filename: 'signature.png'),
              ));
            }

            final res = await _dio.post<Map<String, dynamic>>(
              ApiEndpoints.inspectionSubmit,
              data: form,
            );
            return res.data?['data'] as int? ?? 0;
          },
          type: OfflineOpType.inspectionSubmit,
          payload: {
            'vehicleId': vehicleId,
            'templateId': templateId,
            if (odometer != null) 'odometerReading': odometer,
            if (comments != null) 'comments': comments,
            'resultsJson': jsonEncode(results.map((e) => e.toJson()).toList()),
            'photoPaths': photoPaths,
            if (sigPath != null) 'signaturePath': sigPath,
          },
          filePaths: [
            ...photoPaths,
            if (sigPath != null) sigPath,
          ],
          queuedValue: (_) => 0,
        );
  }
}
