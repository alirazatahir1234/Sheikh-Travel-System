import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:signalr_netcore/signalr_client.dart';
import '../../../core/config/app_config.dart';

typedef CommandCallback = void Function(String commandType, int commandId);

/// Server-pushed location payload from TrackingHub.ReceiveLocationUpdate.
class LiveLocationUpdate {
  const LiveLocationUpdate({
    required this.vehicleId,
    required this.latitude,
    required this.longitude,
    required this.speed,
    required this.timestamp,
    this.heading,
    this.ignition,
    this.address,
    this.alarmType,
  });

  final int vehicleId;
  final double latitude;
  final double longitude;
  final double speed;
  final DateTime timestamp;
  final double? heading;
  final bool? ignition;
  final String? address;
  final String? alarmType;

  static LiveLocationUpdate? tryParse(dynamic raw) {
    if (raw is! Map) return null;
    final map = Map<String, dynamic>.from(raw);
    final vehicleId = (map['vehicleId'] as num?)?.toInt();
    final lat = (map['latitude'] as num?)?.toDouble();
    final lng = (map['longitude'] as num?)?.toDouble();
    if (vehicleId == null || lat == null || lng == null) return null;
    final tsRaw = map['timestamp'];
    DateTime ts;
    if (tsRaw is String) {
      ts = DateTime.tryParse(tsRaw)?.toUtc() ?? DateTime.now().toUtc();
    } else {
      ts = DateTime.now().toUtc();
    }
    return LiveLocationUpdate(
      vehicleId: vehicleId,
      latitude: lat,
      longitude: lng,
      speed: (map['speed'] as num?)?.toDouble() ?? 0,
      timestamp: ts,
      heading: (map['heading'] as num?)?.toDouble(),
      ignition: map['ignition'] as bool?,
      address: map['address'] as String?,
      alarmType: map['alarmType'] as String?,
    );
  }
}

class SignalRService {
  SignalRService._();
  static final instance = SignalRService._();

  HubConnection? _connection;
  final _statusController = StreamController<String>.broadcast();
  final _locationController = StreamController<LiveLocationUpdate>.broadcast();
  CommandCallback? _onCommand;
  int? _joinedVehicleId;

  Stream<String> get statusStream => _statusController.stream;

  Stream<LiveLocationUpdate> get locationUpdates => _locationController.stream;

  String get _hubUrl => AppConfig.hubBaseUrl;

  Future<bool> _hasAuthToken() async {
    const storage = FlutterSecureStorage();
    final token = await storage.read(key: 'fleet_access_token') ??
        await storage.read(key: 'driver_access_token');
    return token != null && token.isNotEmpty;
  }

  Future<void> connect(
    CommandCallback onCommand, {
    int? vehicleId,
  }) async {
    _onCommand = onCommand;

    const storage = FlutterSecureStorage();
    final token = await storage.read(key: 'fleet_access_token') ??
        await storage.read(key: 'driver_access_token');
    if (token == null) return;

    try {
      await _connection?.stop();

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

      _connection!.on('EngineCommand', (args) {
        if (args == null || args.isEmpty) return;
        final data = args[0] as Map<String, dynamic>?;
        if (data == null) return;
        final type = data['commandType'] as String? ?? '';
        final id = data['id'] as int? ?? 0;
        _onCommand?.call(type, id);
      });

      _connection!.on('ReceiveLocationUpdate', (args) {
        if (args == null || args.isEmpty) return;
        final update = LiveLocationUpdate.tryParse(args[0]);
        if (update != null) {
          _locationController.add(update);
        }
      });

      _connection!.on('ReceiveSosAlert', (args) {
        // Surface SOS as a location-adjacent status for UI chips.
        _statusController.add('sos');
      });

      _connection!.onreconnecting(({error}) {
        _statusController.add('reconnecting');
      });

      _connection!.onreconnected(({connectionId}) async {
        if (!await _hasAuthToken()) {
          await disconnect();
          return;
        }
        _statusController.add('connected');
        await _rejoinVehicleGroup();
      });

      _connection!.onclose(({error}) {
        _statusController.add('disconnected');
      });

      await _connection!.start();
      _statusController.add('connected');
      if (vehicleId != null) {
        await joinVehicleGroup(vehicleId);
      }
    } catch (e) {
      _statusController.add('error: $e');
    }
  }

  Future<void> joinVehicleGroup(int vehicleId) async {
    if (_connection?.state != HubConnectionState.Connected) {
      _joinedVehicleId = vehicleId;
      return;
    }
    try {
      final previous = _joinedVehicleId;
      if (previous != null && previous != vehicleId) {
        await _connection!.invoke('LeaveVehicleGroup', args: <Object>[previous]);
      }
      await _connection!.invoke('JoinVehicleGroup', args: <Object>[vehicleId]);
      _joinedVehicleId = vehicleId;
    } catch (e) {
      if (kDebugMode) {
        debugPrint('JoinVehicleGroup failed: $e');
      }
    }
  }

  Future<void> _rejoinVehicleGroup() async {
    final id = _joinedVehicleId;
    if (id != null) await joinVehicleGroup(id);
  }

  Future<void> disconnect() async {
    try {
      if (_joinedVehicleId != null &&
          _connection?.state == HubConnectionState.Connected) {
        await _connection!.invoke('LeaveVehicleGroup', args: <Object>[_joinedVehicleId!]);
      }
    } catch (_) {}
    await _connection?.stop();
    _connection = null;
    _joinedVehicleId = null;
    _statusController.add('disconnected');
  }

  bool get isConnected =>
      _connection?.state == HubConnectionState.Connected;
}
