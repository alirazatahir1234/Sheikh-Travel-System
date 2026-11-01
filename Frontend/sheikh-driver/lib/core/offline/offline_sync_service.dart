import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../api/api_endpoints.dart';
import '../api/dio_client.dart';
import '../providers/theme_provider.dart';
import 'connectivity_provider.dart';
import 'offline_file_store.dart';
import 'offline_models.dart';
import 'offline_outbox.dart';

/// Ops that hit max automatic retries and need an explicit requeue.
bool isOfflineRetryExhausted(OfflineOperation op) =>
    op.status == OfflineOpStatus.failed &&
    (op.attempts >= 8 ||
        (op.lastError?.toLowerCase().contains('max retries') ?? false));

const lastSuccessfulSyncAtPrefsKey = 'lastSuccessfulSyncAt';
const lifetimeSyncedCountPrefsKey = 'offlineLifetimeSyncedCount';

final offlinePendingCountProvider = StateProvider<int>((ref) => OfflineOutbox.length);

/// Bumped on every outbox mutation so queue UIs refresh even when pending count
/// stays the same (e.g. pending → failed without removing the item).
final offlineOutboxRevisionProvider = StateProvider<int>((ref) => 0);

/// Wall-clock of last successful offline drain (or empty-queue sync while online).
final lastSuccessfulSyncAtProvider = StateProvider<DateTime?>((ref) {
  final raw = ref.read(prefsBoxProvider).get(lastSuccessfulSyncAtPrefsKey);
  if (raw is String) return DateTime.tryParse(raw)?.toLocal();
  return null;
});

/// Lifetime count of operations successfully drained from the outbox.
final lifetimeSyncedCountProvider = StateProvider<int>((ref) {
  final raw = ref.read(prefsBoxProvider).get(lifetimeSyncedCountPrefsKey);
  if (raw is int) return raw;
  if (raw is num) return raw.toInt();
  return 0;
});

/// Live outbox snapshot for Settings / connectivity cards (no dummy figures).
final offlineSyncStatsProvider = Provider<OfflineSyncStats>((ref) {
  ref.watch(offlinePendingCountProvider);
  ref.watch(offlineOutboxRevisionProvider);
  final lifetimeSynced = ref.watch(lifetimeSyncedCountProvider);
  final items = OfflineOutbox.all();
  var pending = 0;
  var failed = 0;
  var syncing = false;
  for (final op in items) {
    switch (op.status) {
      case OfflineOpStatus.pending:
        pending++;
      case OfflineOpStatus.syncing:
        pending++;
        syncing = true;
      case OfflineOpStatus.failed:
        failed++;
      case OfflineOpStatus.conflict:
        failed++;
    }
  }
  return OfflineSyncStats(
    pending: pending,
    failed: failed,
    syncedLifetime: lifetimeSynced,
    isSyncing: syncing,
  );
});

class OfflineSyncStats {
  const OfflineSyncStats({
    required this.pending,
    required this.failed,
    required this.syncedLifetime,
    required this.isSyncing,
  });

  final int pending;
  final int failed;
  final int syncedLifetime;
  final bool isSyncing;

  bool get showProgress => isSyncing || pending > 0;
}

final offlineSyncProvider = Provider<OfflineSyncService>((ref) {
  final service = OfflineSyncService(ref.read(dioProvider), ref);
  ref.onDispose(service.dispose);
  // Drain any ops queued before the shell mounted (e.g. cold start online).
  // Defer so sync does not mutate other providers during this provider's build.
  scheduleMicrotask(() => unawaited(service.syncNow()));
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
    _ref.read(offlineOutboxRevisionProvider.notifier).state++;
  }

  void _markLastSuccessfulSync() {
    final now = DateTime.now();
    _ref.read(lastSuccessfulSyncAtProvider.notifier).state = now;
    unawaited(
      _ref.read(prefsBoxProvider).put(
            lastSuccessfulSyncAtPrefsKey,
            now.toUtc().toIso8601String(),
          ),
    );
  }

  void _recordSuccessfulSyncs(int count) {
    if (count <= 0) return;
    final next = _ref.read(lifetimeSyncedCountProvider) + count;
    _ref.read(lifetimeSyncedCountProvider.notifier).state = next;
    unawaited(
      _ref.read(prefsBoxProvider).put(lifetimeSyncedCountPrefsKey, next),
    );
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
      // Copy picker/camera files into documents before enqueue so sync still works
      // after OS clears cache/temp paths.
      final durablePaths =
          await OfflineFileStore.persistForOffline(filePaths);
      final durablePayload = OfflineFileStore.rewritePayloadPaths(
        payload,
        filePaths,
        durablePaths,
      );
      final op = await enqueue(
        type: type,
        payload: durablePayload,
        filePaths: durablePaths.isNotEmpty ? durablePaths : filePaths,
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
    final startedEmpty = OfflineOutbox.pending().isEmpty;
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
      if (synced > 0) {
        _recordSuccessfulSyncs(synced);
      }
      final online = _ref.read(isOnlineProvider);
      if (synced > 0 || (startedEmpty && online)) {
        _markLastSuccessfulSync();
      }
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

  /// Reset attempt counts for all failed (incl. max-retries) ops and sync.
  Future<int> retryAllFailed() async {
    final failed = OfflineOutbox.all().where(
      (o) =>
          o.status == OfflineOpStatus.failed ||
          o.status == OfflineOpStatus.conflict ||
          isOfflineRetryExhausted(o),
    );
    for (final op in failed) {
      await OfflineOutbox.requeue(op.id);
    }
    _notifyCount();
    return syncNow();
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
          'Start' => ApiEndpoints.startTrip(id),
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
      case OfflineOpType.voiceCommand:
        await _dio.post(
          ApiEndpoints.voiceCommands,
          data: {
            'transcript': op.payload['transcript'],
            'languageCode': op.payload['languageCode'],
            if (op.payload['intent'] != null) 'intent': op.payload['intent'],
            'clientOpId': op.id,
            if (op.payload['latitude'] != null) 'latitude': op.payload['latitude'],
            if (op.payload['longitude'] != null) 'longitude': op.payload['longitude'],
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
