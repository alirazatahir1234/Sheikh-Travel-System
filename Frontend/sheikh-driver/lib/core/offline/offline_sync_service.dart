import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../api/api_endpoints.dart';
import '../api/dio_client.dart';
import 'connectivity_provider.dart';
import 'offline_models.dart';
import 'offline_outbox.dart';

final offlinePendingCountProvider = StateProvider<int>((ref) => OfflineOutbox.length);

final offlineSyncProvider = Provider<OfflineSyncService>((ref) {
  final service = OfflineSyncService(ref.read(dioProvider), ref);
  ref.onDispose(service.dispose);
  return service;
});

class OfflineSyncService {
  OfflineSyncService(this._dio, this._ref) {
    _connectivitySub = Connectivity().onConnectivityChanged.listen((results) {
      final online = results.isNotEmpty &&
          !results.every((r) => r == ConnectivityResult.none);
      if (online) {
        unawaited(syncNow());
      }
    });
  }

  final Dio _dio;
  final Ref _ref;
  StreamSubscription? _connectivitySub;
  bool _syncing = false;

  void dispose() {
    _connectivitySub?.cancel();
  }

  void _notifyCount() {
    _ref.read(offlinePendingCountProvider.notifier).state = OfflineOutbox.length;
  }

  Future<OfflineOperation> enqueue({
    required OfflineOpType type,
    required Map<String, dynamic> payload,
    List<String> filePaths = const [],
  }) async {
    final op = await OfflineOutbox.enqueue(
      type: type,
      payload: payload,
      filePaths: filePaths,
    );
    _notifyCount();
    return op;
  }

  /// Try online call; on network failure enqueue and throw [OfflineQueuedException].
  Future<T> runOrQueue<T>({
    required Future<T> Function() online,
    required OfflineOpType type,
    required Map<String, dynamic> payload,
    List<String> filePaths = const [],
    T? Function(OfflineOperation op)? queuedValue,
  }) async {
    try {
      return await online();
    } catch (e) {
      if (!isOfflineDioError(e)) rethrow;
      final op = await enqueue(
        type: type,
        payload: payload,
        filePaths: filePaths,
      );
      if (queuedValue != null) {
        final v = queuedValue(op);
        if (v != null) return v;
      }
      throw OfflineQueuedException(
        '${op.label} saved offline — will sync when connected',
        operationId: op.id,
      );
    }
  }

  Future<int> syncNow() async {
    if (_syncing) return 0;
    _syncing = true;
    var synced = 0;
    try {
      final pending = OfflineOutbox.pending();
      for (final op in pending) {
        if (op.attempts >= 8) {
          op.status = OfflineOpStatus.failed;
          op.lastError = 'Max retries exceeded';
          await OfflineOutbox.update(op);
          continue;
        }

        op.status = OfflineOpStatus.syncing;
        op.attempts += 1;
        await OfflineOutbox.update(op);

        try {
          await _dispatch(op);
          await OfflineOutbox.remove(op.id);
          synced++;
        } on DioException catch (e) {
          if (_isAlreadyAppliedConflict(e)) {
            // Conflict resolution: treat as success (server already has the effect).
            await OfflineOutbox.remove(op.id);
            synced++;
            debugPrint('[Offline] Conflict resolved (already applied): ${op.label}');
          } else if (isOfflineDioError(e)) {
            op.status = OfflineOpStatus.pending;
            op.lastError = e.message;
            await OfflineOutbox.update(op);
            break; // still offline — stop draining
          } else if ((e.response?.statusCode ?? 0) >= 400 &&
              (e.response?.statusCode ?? 0) < 500) {
            op.status = OfflineOpStatus.conflict;
            op.lastError = _errorMessage(e);
            await OfflineOutbox.update(op);
            debugPrint('[Offline] Conflict kept for review: ${op.label} — ${op.lastError}');
          } else {
            op.status = OfflineOpStatus.failed;
            op.lastError = _errorMessage(e);
            await OfflineOutbox.update(op);
          }
        } catch (e) {
          op.status = OfflineOpStatus.failed;
          op.lastError = e.toString();
          await OfflineOutbox.update(op);
        }
      }
    } finally {
      _syncing = false;
      _notifyCount();
    }
    return synced;
  }

  /// Reset a failed/conflict item and attempt sync for that op only.
  Future<bool> retryOne(String id) async {
    await OfflineOutbox.requeue(id);
    _notifyCount();
    final n = await syncNow();
    return n > 0;
  }

  Future<void> _dispatch(OfflineOperation op) async {
    final headers = {
      'Idempotency-Key': op.id,
      'X-Client-Op-Id': op.id,
    };

    switch (op.type) {
      case OfflineOpType.attendanceCheckIn:
        await _dio.post(
          ApiEndpoints.attendanceCheckIn,
          data: {
            'latitude': op.payload['latitude'],
            'longitude': op.payload['longitude'],
          },
          options: Options(headers: headers),
        );
      case OfflineOpType.attendanceCheckOut:
        await _dio.post(
          ApiEndpoints.attendanceCheckOut,
          data: {
            'latitude': op.payload['latitude'],
            'longitude': op.payload['longitude'],
          },
          options: Options(headers: headers),
        );
      case OfflineOpType.tripAdvance:
        final id = op.payload['tripId'] as int;
        final action = op.payload['action'] as String;
        final reason = op.payload['reason'] as String?;
        final path = switch (action) {
          'Accept' => ApiEndpoints.acceptTrip(id),
          'Arrived' => ApiEndpoints.arrivedTrip(id),
          'Onboard' => ApiEndpoints.onboardTrip(id),
          'Complete' => ApiEndpoints.completeTrip(id),
          'Reject' => ApiEndpoints.rejectTrip(id),
          _ => throw StateError('Unknown trip action $action'),
        };
        if (action == 'Reject') {
          await _dio.post(path, data: reason ?? '', options: Options(headers: headers));
        } else {
          await _dio.post(path, options: Options(headers: headers));
        }
      case OfflineOpType.fuelSubmit:
        final form = FormData.fromMap({
          'vehicleId': op.payload['vehicleId'],
          'liters': op.payload['liters'],
          'pricePerLiter': op.payload['pricePerLiter'],
          'odometerReading': op.payload['odometerReading'],
          'station': op.payload['station'],
          'fuelType': op.payload['fuelType'],
          'fuelDate': op.payload['fuelDate'] ?? DateTime.now().toUtc().toIso8601String(),
        });
        for (final path in op.filePaths) {
          if (!File(path).existsSync()) continue;
          form.files.add(MapEntry(
            'receipt',
            await MultipartFile.fromFile(path, filename: path.split('/').last),
          ));
        }
        await _dio.post(
          ApiEndpoints.fuelReceipts,
          data: form,
          options: Options(headers: headers),
        );
      case OfflineOpType.inspectionSubmit:
        final form = FormData.fromMap({
          'vehicleId': op.payload['vehicleId'],
          'templateId': op.payload['templateId'],
          if (op.payload['odometerReading'] != null)
            'odometerReading': op.payload['odometerReading'],
          if (op.payload['comments'] != null) 'comments': op.payload['comments'],
          'resultsJson': op.payload['resultsJson'] ?? '[]',
        });
        final photoPaths = (op.payload['photoPaths'] as List?)
                ?.map((e) => e.toString())
                .toList() ??
            [];
        for (final path in [...photoPaths, ...op.filePaths]) {
          if (!File(path).existsSync()) continue;
          final name = path.split('/').last;
          final field = name == 'signature.png' ? 'signature' : 'photos';
          form.files.add(MapEntry(
            field,
            await MultipartFile.fromFile(path, filename: name),
          ));
        }
        final sig = op.payload['signaturePath']?.toString();
        if (sig != null && File(sig).existsSync()) {
          form.files.add(MapEntry(
            'signature',
            await MultipartFile.fromFile(sig, filename: 'signature.png'),
          ));
        }
        await _dio.post(
          ApiEndpoints.inspectionSubmit,
          data: form,
          options: Options(headers: headers),
        );
      case OfflineOpType.documentUpload:
        final filePath = op.filePaths.isNotEmpty
            ? op.filePaths.first
            : op.payload['filePath']?.toString();
        if (filePath == null || !File(filePath).existsSync()) {
          throw StateError('Document file missing for offline upload');
        }
        final form = FormData.fromMap({
          'documentType': op.payload['documentType'],
          if (op.payload['expiryDate'] != null)
            'expiryDate': op.payload['expiryDate'],
          if (op.payload['vehicleId'] != null)
            'vehicleId': op.payload['vehicleId'],
          'file': await MultipartFile.fromFile(
            filePath,
            filename: filePath.split('/').last,
          ),
        });
        await _dio.post(
          ApiEndpoints.documentsUpload,
          data: form,
          options: Options(headers: headers),
        );
      case OfflineOpType.paymentCollect:
        final tripId = op.payload['tripId'] as int;
        await _dio.post(
          ApiEndpoints.collectTripPayment(tripId),
          data: {
            'amountReceived': op.payload['amountReceived'],
            'paymentMethod': op.payload['paymentMethod'],
            'referenceNumber': op.payload['referenceNumber'],
            'notes': op.payload['notes'],
          },
          options: Options(headers: headers),
        );
    }
  }

  static bool _isAlreadyAppliedConflict(DioException e) {
    final status = e.response?.statusCode ?? 0;
    if (status == 409) return true;
    if (status != 400 && status != 422) return false;
    final raw = e.response?.data;
    final text = raw is Map
        ? jsonEncode(raw).toLowerCase()
        : raw?.toString().toLowerCase() ?? '';
    return text.contains('already') ||
        text.contains('invalid transition') ||
        text.contains('not allowed') ||
        text.contains('cannot transition') ||
        text.contains('checked in') ||
        text.contains('checked out');
  }

  static String _errorMessage(DioException e) {
    final data = e.response?.data;
    if (data is Map && data['message'] != null) return data['message'].toString();
    if (data is Map && data['error'] != null) return data['error'].toString();
    return e.message ?? e.toString();
  }
}
