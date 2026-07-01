import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:signalr_netcore/signalr_client.dart';
import '../../../core/config/app_config.dart';

typedef CommandCallback = void Function(String commandType, int commandId);

class SignalRService {
  SignalRService._();
  static final instance = SignalRService._();

  HubConnection? _connection;
  StreamController<String>? _statusController;
  CommandCallback? _onCommand;

  Stream<String> get statusStream =>
      _statusController?.stream ?? const Stream.empty();

  String get _hubUrl {
    // Convert http(s) to ws(s) for SignalR
    final base = AppConfig.baseUrl.replaceFirst('/api', '');
    return '$base/hubs/tracking';
  }

  Future<void> connect(CommandCallback onCommand) async {
    _onCommand = onCommand;
    _statusController ??= StreamController<String>.broadcast();

    const storage = FlutterSecureStorage();
    final token = await storage.read(key: 'driver_access_token');
    if (token == null) return;

    try {
      _connection = HubConnectionBuilder()
          .withUrl(
            '$_hubUrl?access_token=$token',
            options: HttpConnectionOptions(
              transport: HttpTransportType.WebSockets,
              logMessageContent: kDebugMode,
            ),
          )
          .withAutomaticReconnect(retryDelays: [2000, 5000, 10000, 30000])
          .build();

      _connection!.on('EngineCommand', (args) {
        if (args == null || args.isEmpty) return;
        final data = args[0] as Map<String, dynamic>?;
        if (data == null) return;
        final type = data['commandType'] as String? ?? '';
        final id = data['id'] as int? ?? 0;
        _onCommand?.call(type, id);
      });

      _connection!.onreconnecting(({error}) {
        _statusController?.add('reconnecting');
      });

      _connection!.onreconnected(({connectionId}) {
        _statusController?.add('connected');
      });

      _connection!.onclose(({error}) {
        _statusController?.add('disconnected');
      });

      await _connection!.start();
      _statusController?.add('connected');
    } catch (e) {
      _statusController?.add('error: $e');
    }
  }

  Future<void> disconnect() async {
    await _connection?.stop();
    _connection = null;
    _statusController?.add('disconnected');
  }

  bool get isConnected =>
      _connection?.state == HubConnectionState.Connected;
}
