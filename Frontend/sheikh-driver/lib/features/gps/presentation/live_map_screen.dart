import 'dart:async';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/trips/presentation/trips_notifier.dart';
import '../services/location_queue.dart';
import '../services/signalr_service.dart';

class LiveMapScreen extends ConsumerStatefulWidget {
  const LiveMapScreen({super.key});

  @override
  ConsumerState<LiveMapScreen> createState() => _LiveMapScreenState();
}

class _LiveMapScreenState extends ConsumerState<LiveMapScreen> {
  Position? _position;
  StreamSubscription<Position>? _positionSub;
  StreamSubscription<List<ConnectivityResult>>? _connectivitySub;
  bool _online = true;
  bool _tracking = false;
  int _queuedCount = 0;
  String _signalrStatus = 'disconnected';
  StreamSubscription<String>? _signalrStatusSub;

  @override
  void initState() {
    super.initState();
    _watchConnectivity();
    _watchSignalR();
  }

  @override
  void dispose() {
    _positionSub?.cancel();
    _connectivitySub?.cancel();
    _signalrStatusSub?.cancel();
    SignalRService.instance.disconnect();
    super.dispose();
  }

  void _watchConnectivity() {
    _connectivitySub = Connectivity().onConnectivityChanged.listen((results) {
      final online = results.any((r) => r != ConnectivityResult.none);
      if (mounted) setState(() => _online = online);
      if (online && _queuedCount > 0) _drainQueue();
    });
  }

  void _watchSignalR() {
    _signalrStatusSub = SignalRService.instance.statusStream.listen((status) {
      if (mounted) setState(() => _signalrStatus = status);
    });
  }

  Future<void> _startTracking() async {
    final perm = await Geolocator.requestPermission();
    if (perm == LocationPermission.denied || perm == LocationPermission.deniedForever) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Location permission required')),
        );
      }
      return;
    }

    // Connect SignalR for engine command listening
    await SignalRService.instance.connect((type, id) async {
      await ref.read(dioProvider).post(ApiEndpoints.completeCommand(id));
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Engine command received: $type'),
            backgroundColor: AppColors.warning,
          ),
        );
      }
    });

    setState(() => _tracking = true);

    _positionSub = Geolocator.getPositionStream(
      locationSettings: const LocationSettings(
        accuracy: LocationAccuracy.high,
        distanceFilter: 10, // Update every 10 metres
        timeLimit: Duration(seconds: 10),
      ),
    ).listen((pos) async {
      if (!mounted) return;
      setState(() => _position = pos);
      await _postOrQueue(pos);
    });
  }

  Future<void> _postOrQueue(Position pos) async {
    final trips = ref.read(tripsProvider).valueOrNull ?? [];
    final active = trips.where((t) => t.isStarted).firstOrNull;
    if (active?.vehicleId == null) return;

    final loc = QueuedLocation(
      vehicleId: active!.vehicleId!,
      lat: pos.latitude,
      lng: pos.longitude,
      speed: pos.speed * 3.6,
      ts: pos.timestamp.millisecondsSinceEpoch,
      bookingId: active.id,
    );

    if (_online) {
      try {
        // Try to drain any backlog first
        if (_queuedCount > 0) await _drainQueue();

        await ref.read(dioProvider).post(ApiEndpoints.tripLocation, data: {
          'vehicleId': loc.vehicleId,
          'latitude': loc.lat,
          'longitude': loc.lng,
          'speed': loc.speed,
          'bookingId': loc.bookingId,
        });
      } catch (_) {
        await LocationQueue.enqueue(loc);
        if (mounted) setState(() => _queuedCount = LocationQueue.length);
      }
    } else {
      await LocationQueue.enqueue(loc);
      if (mounted) setState(() => _queuedCount = LocationQueue.length);
    }
  }

  Future<void> _drainQueue() async {
    final positions = LocationQueue.getAll();
    if (positions.isEmpty) return;
    try {
      await ref.read(dioProvider).post(ApiEndpoints.tripLocationBatch, data: {
        'positions': positions
            .map((p) => {
                  'vehicleId': p.vehicleId,
                  'latitude': p.lat,
                  'longitude': p.lng,
                  'speed': p.speed,
                  if (p.bookingId != null) 'bookingId': p.bookingId,
                })
            .toList(),
      });
      await LocationQueue.clear();
      if (mounted) setState(() => _queuedCount = 0);
    } catch (_) {
      // Leave intact for next attempt
    }
  }

  void _stopTracking() {
    _positionSub?.cancel();
    _positionSub = null;
    SignalRService.instance.disconnect();
    setState(() {
      _tracking = false;
      _position = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Live Tracking'),
        actions: [
          if (!_online)
            const Padding(
              padding: EdgeInsets.only(right: 4),
              child: Icon(Icons.cloud_off, color: Colors.orangeAccent, size: 20),
            ),
          IconButton(
            icon: Icon(_tracking ? Icons.gps_fixed : Icons.gps_not_fixed),
            tooltip: _tracking ? 'Stop' : 'Start',
            onPressed: _tracking ? _stopTracking : _startTracking,
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _showSosDialog,
        backgroundColor: AppColors.error,
        icon: const Icon(Icons.sos, color: Colors.white),
        label: const Text('SOS', style: TextStyle(color: Colors.white, fontWeight: FontWeight.w700)),
      ),
      body: Column(
        children: [
          _StatusBar(
            tracking: _tracking,
            online: _online,
            queuedCount: _queuedCount,
            signalrStatus: _signalrStatus,
            position: _position,
          ),
          Expanded(
            child: _tracking && _position != null
                ? _PositionCard(position: _position!)
                : _IdleView(tracking: _tracking, onStart: _startTracking),
          ),
        ],
      ),
    );
  }

  Future<void> _showSosDialog() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (_) => AlertDialog(
        title: const Row(
          children: [
            Icon(Icons.sos, color: AppColors.error),
            SizedBox(width: 8),
            Text('Send SOS Alert', style: TextStyle(color: AppColors.error)),
          ],
        ),
        content: const Text(
          'This will immediately alert your dispatcher with your current location. Only use in an emergency.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            style: FilledButton.styleFrom(backgroundColor: AppColors.error),
            child: const Text('Send SOS'),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    try {
      await ref.read(dioProvider).post(
        ApiEndpoints.sos,
        data: {
          if (_position != null) 'latitude': _position!.latitude,
          if (_position != null) 'longitude': _position!.longitude,
        },
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('SOS alert sent to dispatcher'),
            backgroundColor: AppColors.error,
            duration: Duration(seconds: 5),
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('SOS failed: $e'), backgroundColor: AppColors.error),
        );
      }
    }
  }
}

class _StatusBar extends StatelessWidget {
  const _StatusBar({
    required this.tracking,
    required this.online,
    required this.queuedCount,
    required this.signalrStatus,
    this.position,
  });

  final bool tracking;
  final bool online;
  final int queuedCount;
  final String signalrStatus;
  final Position? position;

  @override
  Widget build(BuildContext context) {
    if (!tracking) return const SizedBox.shrink();

    final gpsColor = online ? AppColors.success : AppColors.warning;
    return Container(
      color: gpsColor,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Row(
        children: [
          const Icon(Icons.gps_fixed, color: Colors.white, size: 15),
          const SizedBox(width: 6),
          Text(
            online ? 'Live' : 'Offline — queued $queuedCount',
            style: const TextStyle(color: Colors.white, fontSize: 13, fontWeight: FontWeight.w600),
          ),
          const Spacer(),
          if (position != null)
            Text(
              '${(position!.speed * 3.6).toStringAsFixed(0)} km/h',
              style: const TextStyle(color: Colors.white, fontSize: 13),
            ),
          const SizedBox(width: 10),
          Icon(
            signalrStatus == 'connected' ? Icons.wifi : Icons.wifi_off,
            color: Colors.white,
            size: 15,
          ),
        ],
      ),
    );
  }
}

class _IdleView extends StatelessWidget {
  const _IdleView({required this.tracking, required this.onStart});
  final bool tracking;
  final VoidCallback onStart;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.map_outlined, size: 72, color: AppColors.textSecondary),
            const SizedBox(height: 16),
            const Text(
              'Live Tracking',
              style: TextStyle(
                  fontSize: 20, fontWeight: FontWeight.w700, color: AppColors.textPrimary),
            ),
            const SizedBox(height: 8),
            const Text(
              'Tap Start to begin sending your location to the ERP.\nPosition updates every 10 metres. Offline positions are queued in Hive and synced when connection restores.',
              textAlign: TextAlign.center,
              style: TextStyle(color: AppColors.textSecondary, fontSize: 13),
            ),
            const SizedBox(height: 24),
            if (!tracking)
              FilledButton.icon(
                onPressed: onStart,
                icon: const Icon(Icons.play_arrow_rounded),
                label: const Text('Start Tracking'),
              ),
          ],
        ),
      ),
    );
  }
}

class _PositionCard extends StatelessWidget {
  const _PositionCard({required this.position});
  final Position position;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('Current Position',
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
              const SizedBox(height: 14),
              const Divider(height: 1),
              const SizedBox(height: 14),
              _Row('Latitude', position.latitude.toStringAsFixed(6)),
              _Row('Longitude', position.longitude.toStringAsFixed(6)),
              _Row('Speed', '${(position.speed * 3.6).toStringAsFixed(1)} km/h'),
              _Row('Accuracy', '±${position.accuracy.toStringAsFixed(0)} m'),
              _Row('Altitude', '${position.altitude.toStringAsFixed(0)} m'),
            ],
          ),
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Row(
        children: [
          SizedBox(
              width: 100,
              child: Text(label,
                  style: const TextStyle(color: AppColors.textSecondary, fontSize: 13))),
          Text(value,
              style: const TextStyle(
                  fontWeight: FontWeight.w600, color: AppColors.textPrimary, fontSize: 13)),
        ],
      ),
    );
  }
}
