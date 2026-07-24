import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/core/offline/connectivity_provider.dart';
import 'package:sheikh_go_driver/core/offline/offline_models.dart';

void main() {
  group('OfflineOperation', () {
    test('round-trips through toMap/fromMap', () {
      final op = OfflineOperation(
        id: 'abc',
        type: OfflineOpType.fuelSubmit,
        payload: {'liters': 20, 'station': 'PSO'},
        createdAt: DateTime.utc(2026, 7, 1, 10),
        filePaths: ['/tmp/r.jpg'],
        attempts: 2,
        lastError: 'timeout',
        status: OfflineOpStatus.failed,
      );
      final restored = OfflineOperation.fromMap(op.toMap());
      expect(restored.id, 'abc');
      expect(restored.type, OfflineOpType.fuelSubmit);
      expect(restored.payload['station'], 'PSO');
      expect(restored.filePaths, ['/tmp/r.jpg']);
      expect(restored.attempts, 2);
      expect(restored.status, OfflineOpStatus.failed);
      expect(restored.label, 'Fuel receipt');
    });

    test('trip advance label includes action and id', () {
      final op = OfflineOperation(
        id: '1',
        type: OfflineOpType.tripAdvance,
        payload: {'action': 'Accept', 'tripId': 88},
        createdAt: DateTime.now(),
      );
      expect(op.label, contains('Accept'));
      expect(op.label, contains('88'));
    });
  });

  group('isOfflineDioError', () {
    test('connection errors are offline', () {
      expect(
        isOfflineDioError(DioException(
          requestOptions: RequestOptions(path: '/'),
          type: DioExceptionType.connectionError,
        )),
        isTrue,
      );
    });

    test('400 responses are not offline', () {
      expect(
        isOfflineDioError(DioException(
          requestOptions: RequestOptions(path: '/'),
          type: DioExceptionType.badResponse,
          response: Response(
            requestOptions: RequestOptions(path: '/'),
            statusCode: 400,
          ),
        )),
        isFalse,
      );
    });

    test('socket message treated as offline', () {
      expect(
        isOfflineDioError(DioException(
          requestOptions: RequestOptions(path: '/'),
          type: DioExceptionType.unknown,
          message: 'SocketException: Failed host lookup',
        )),
        isTrue,
      );
    });
  });

  group('OfflineQueuedException', () {
    test('toString returns message', () {
      final e = OfflineQueuedException('saved offline', operationId: 'x');
      expect(e.toString(), 'saved offline');
      expect(e.operationId, 'x');
    });
  });
}
