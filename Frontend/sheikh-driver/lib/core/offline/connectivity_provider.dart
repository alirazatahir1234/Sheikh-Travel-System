import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

final connectivityListProvider = StreamProvider<List<ConnectivityResult>>(
  (ref) => Connectivity().onConnectivityChanged,
);

final isOnlineProvider = Provider<bool>((ref) {
  final async = ref.watch(connectivityListProvider);
  return async.maybeWhen(
    data: (results) =>
        results.isNotEmpty && !results.every((r) => r == ConnectivityResult.none),
    orElse: () => true,
  );
});

bool isOfflineDioError(Object e) {
  if (e is! DioException) return false;
  if (e.type == DioExceptionType.connectionError ||
      e.type == DioExceptionType.connectionTimeout ||
      e.type == DioExceptionType.receiveTimeout ||
      e.type == DioExceptionType.sendTimeout) {
    return true;
  }
  // Android/iOS often surface offline as unknown with SocketException
  final msg = e.message?.toLowerCase() ?? '';
  return msg.contains('socket') ||
      msg.contains('network is unreachable') ||
      msg.contains('failed host lookup');
}
