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
  DateTime? _lastLocationAt;
  String _lastStatus = 'disconnected';
  int _locationCount = 0;

  Stream<GpsPosition> get locationUpdates => _updates.stream;
  Stream<String> get connectionStatus => _status.stream;
  DateTime? get lastLocationAt => _lastLocationAt;
  String get currentStatus => _lastStatus;
  bool get isConnected =>
      _connection?.state == HubConnectionState.Connected;

  String get _hubUrl => AppConfig.hubBaseUrl;

  void _emitStatus(String status) {
    _lastStatus = status;
    _status.add(status);
  }

  Future<bool> _hasAuthToken() async {
    const storage = FlutterSecureStorage();
    final token = await storage.read(key: 'fleet_access_token') ??
        await storage.read(key: 'driver_access_token');
    return token != null && token.isNotEmpty;
  }

  Future<void> connect() async {
    if (_connection != null) return;

    const storage = FlutterSecureStorage();
    final token = await storage.read(key: 'fleet_access_token') ??
        await storage.read(key: 'driver_access_token');
    if (token == null) {
      _emitStatus('no_token');
      return;
    }

    try {
      debugPrint('[FleetRealtime] connecting to $_hubUrl');
      _connection = HubConnectionBuilder()
          .withUrl(
            '$_hubUrl?access_token=$token',
            options: HttpConnectionOptions(
              transport: HttpTransportType.WebSockets,
              logMessageContent: kDebugMode,
            ),
          )
          .withAutomaticReconnect(retryDelays: [0, 1000, 2000, 5000, 10000])
          .build();

      _connection!.on('ReceiveLocationUpdate', (args) {
        if (args == null || args.isEmpty) return;
        final raw = args[0];
        if (raw is! Map) return;
        try {
          final pos = GpsPosition.fromJson(Map<String, dynamic>.from(raw));
          _lastLocationAt = DateTime.now();
          _locationCount++;
          if (kDebugMode && _locationCount % 25 == 1) {
            debugPrint(
              '[FleetRealtime] location updates applied=$_locationCount '
              'vehicle=${pos.vehicleId}',
            );
          }
          _updates.add(pos);
        } catch (e) {
          debugPrint('[FleetRealtime] bad payload: $e');
        }
      });

      _connection!.onreconnecting(({error}) {
        debugPrint('[FleetRealtime] reconnecting: $error');
        _emitStatus('reconnecting');
      });
      _connection!.onreconnected(({connectionId}) async {
        if (!await _hasAuthToken()) {
          await disconnect();
          return;
        }
        debugPrint('[FleetRealtime] reconnected id=$connectionId');
        _emitStatus('connected');
        await _joinDispatcher();
      });
      _connection!.onclose(({error}) {
        debugPrint('[FleetRealtime] closed: $error');
        _emitStatus('disconnected');
      });

      await _connection!.start();
      await _joinDispatcher();
      _emitStatus('connected');
      debugPrint('[FleetRealtime] connected');
    } catch (e) {
      debugPrint('[FleetRealtime] connect failed: $e');
      _connection = null;
      _emitStatus('disconnected');
    }
  }

  Future<void> _joinDispatcher() async {
    try {
      await _connection?.invoke('JoinDispatcherGroup');
      debugPrint('[FleetRealtime] JoinDispatcherGroup ok');
    } catch (e) {
      debugPrint('[FleetRealtime] JoinDispatcherGroup failed: $e');
    }
  }

  Future<void> disconnect() async {
    await _connection?.stop();
    _connection = null;
    _emitStatus('disconnected');
  }
}
