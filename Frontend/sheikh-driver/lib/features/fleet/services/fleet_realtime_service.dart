import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:signalr_netcore/signalr_client.dart';
import '../../../core/config/app_config.dart';
import '../domain/fleet_models.dart';

/// Staff/dispatcher realtime feed for fleet live map.
class FleetRealtimeService {
  FleetRealtimeService._();
  static final instance = FleetRealtimeService._();

  HubConnection? _connection;
  final _updates = StreamController<GpsPosition>.broadcast();
  final _status = StreamController<String>.broadcast();

  Stream<GpsPosition> get locationUpdates => _updates.stream;
  Stream<String> get connectionStatus => _status.stream;
  bool get isConnected =>
      _connection?.state == HubConnectionState.Connected;

  String get _hubUrl => AppConfig.hubBaseUrl;

  Future<bool> _hasAuthToken() async {
    const storage = FlutterSecureStorage(
      mOptions: MacOsOptions(useDataProtectionKeyChain: false),
    );
    final token = await storage.read(key: 'fleet_access_token') ??
        await storage.read(key: 'driver_access_token');
    return token != null && token.isNotEmpty;
  }

  Future<void> connect() async {
    if (_connection != null) return;

    const storage = FlutterSecureStorage(
      mOptions: MacOsOptions(useDataProtectionKeyChain: false),
    );
    final token = await storage.read(key: 'fleet_access_token') ??
        await storage.read(key: 'driver_access_token');
    if (token == null) {
      _status.add('no_token');
      return;
    }

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

      _connection!.on('ReceiveLocationUpdate', (args) {
        if (args == null || args.isEmpty) return;
        final raw = args[0];
        if (raw is! Map) return;
        try {
          _updates.add(
            GpsPosition.fromJson(Map<String, dynamic>.from(raw)),
          );
        } catch (e) {
          debugPrint('[FleetRealtime] bad payload: $e');
        }
      });

      _connection!.onreconnecting(({error}) => _status.add('reconnecting'));
      _connection!.onreconnected(({connectionId}) async {
        if (!await _hasAuthToken()) {
          await disconnect();
          return;
        }
        _status.add('connected');
        await _joinDispatcher();
      });
      _connection!.onclose(({error}) => _status.add('disconnected'));

      await _connection!.start();
      await _joinDispatcher();
      _status.add('connected');
    } catch (e) {
      _status.add('error: $e');
      debugPrint('[FleetRealtime] connect failed: $e');
    }
  }

  Future<void> _joinDispatcher() async {
    try {
      await _connection?.invoke('JoinDispatcherGroup');
    } catch (e) {
      debugPrint('[FleetRealtime] JoinDispatcherGroup failed: $e');
    }
  }

  Future<void> disconnect() async {
    await _connection?.stop();
    _connection = null;
    _status.add('disconnected');
  }
}
