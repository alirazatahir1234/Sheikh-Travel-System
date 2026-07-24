import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/api/api_response.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/offline/offline_models.dart';
import '../../../core/offline/offline_sync_service.dart';
import '../domain/payment_summary_model.dart';

final paymentsApiProvider =
    Provider<PaymentsApi>((ref) => PaymentsApi(ref.read(dioProvider), ref));

class PaymentsApi {
  PaymentsApi(this._dio, this._ref);
  final Dio _dio;
  final Ref _ref;

  Future<PaymentSummary> getSummary(int tripId) async {
    final res =
        await _dio.get<Map<String, dynamic>>(ApiEndpoints.tripPaymentSummary(tripId));
    ApiResponseParser.ensureSuccess(res.data);
    final raw = res.data?['data'];
    final map = raw is Map
        ? Map<String, dynamic>.from(raw)
        : (res.data ?? <String, dynamic>{});
    return PaymentSummary.fromJson(map);
  }

  Future<void> collect({
    required int tripId,
    required double amountReceived,
    required String paymentMethod,
    String? referenceNumber,
    String? notes,
  }) async {
    await _ref.read(offlineSyncProvider).runOrQueue(
          online: () async {
            final res = await _dio.post<Map<String, dynamic>>(
              ApiEndpoints.collectTripPayment(tripId),
              data: {
                'amountReceived': amountReceived,
                'paymentMethod': paymentMethod,
                'referenceNumber': referenceNumber,
                'notes': notes,
              },
            );
            ApiResponseParser.ensureSuccess(res.data);
          },
          type: OfflineOpType.paymentCollect,
          payload: {
            'tripId': tripId,
            'amountReceived': amountReceived,
            'paymentMethod': paymentMethod,
            if (referenceNumber != null) 'referenceNumber': referenceNumber,
            if (notes != null) 'notes': notes,
          },
        );
  }
}
