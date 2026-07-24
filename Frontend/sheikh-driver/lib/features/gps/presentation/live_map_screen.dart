import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:intl/intl.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/analytics/analytics_service.dart';
import '../../../features/trips/presentation/trips_notifier.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../fleet/domain/fleet_models.dart';
import '../services/background_gps_tracker.dart';
import '../services/signalr_service.dart';

final backgroundGpsProvider = ChangeNotifierProvider<BackgroundGpsTracker>(
  (ref) {
    final tracker = BackgroundGpsTracker.instance;
    tracker.bindDio(ref.read(dioProvider));
    return tracker;
  },
);

class LiveMapScreen extends ConsumerStatefulWidget {
  const LiveMapScreen({super.key});

  @override
  ConsumerState<LiveMapScreen> createState() => _LiveMapScreenState();
}

class _LiveMapScreenState extends ConsumerState<LiveMapScreen> {
  String _signalrStatus = 'disconnected';
  String? _serverAddress;
  DateTime? _lastServerFixAt;
  StreamSubscription<String>? _statusSub;
  StreamSubscription<LiveLocationUpdate>? _locationSub;
  Timer? _fallbackPoll;
  int? _trackingVehicleId;

  @override
  void initState() {
    super.initState();
    _statusSub = SignalRService.instance.statusStream.listen((status) {
      if (!mounted) return;
      setState(() => _signalrStatus = status);
      if (status == 'connected') {
        _fallbackPoll?.cancel();
        _fallbackPoll = null;
      } else if (status == 'disconnected' || status == 'reconnecting') {
        _ensureFallbackPoll();
      }
    });
    _locationSub = SignalRService.instance.locationUpdates.listen((update) {
      if (!mounted) return;
      if (_trackingVehicleId != null && update.vehicleId != _trackingVehicleId) {
        return;
      }
      setState(() {
        _lastServerFixAt = update.timestamp.toLocal();
        _serverAddress = update.address;
      });
    });
  }

  @override
  void dispose() {
    _statusSub?.cancel();
    _locationSub?.cancel();
    _fallbackPoll?.cancel();
    SignalRService.instance.disconnect();
    super.dispose();
  }

  void _ensureFallbackPoll() {
    if (_trackingVehicleId == null) return;
    if (_fallbackPoll != null) return;
    _fallbackPoll = Timer.periodic(const Duration(seconds: 10), (_) {
      unawaited(_pollVehicleGps(_trackingVehicleId!));
    });
    unawaited(_pollVehicleGps(_trackingVehicleId!));
  }

  Future<void> _pollVehicleGps(int vehicleId) async {
    try {
      final res = await ref.read(dioProvider).get(ApiEndpoints.vehicleGps(vehicleId));
      final data = res.data;
      if (data is! Map) return;
      final map = Map<String, dynamic>.from(data);
      final body = map['data'] is Map
          ? Map<String, dynamic>.from(map['data'] as Map)
          : map;
      final info = VehicleGpsInfo.fromJson(body);
      if (info.latitude == null || info.longitude == null) return;
      if (!mounted) return;
      setState(() {
        _lastServerFixAt = info.lastUpdate?.toLocal() ?? DateTime.now();
        _serverAddress = info.address;
      });
    } catch (_) {
      // Ignore fallback errors — local GPS tracker remains source of truth for outbound.
    }
  }

  Future<void> _startTracking() async {
    final trips = ref.read(tripsProvider).valueOrNull ?? [];
    final active = trips.where((t) => t.isStarted || t.isConfirmed).firstOrNull;
    if (active?.vehicleId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Assign/start a trip with a vehicle before tracking'),
        ),
      );
      return;
    }

    _trackingVehicleId = active!.vehicleId;

    await SignalRService.instance.connect(
      (type, id) async {
        await ref.read(dioProvider).post(ApiEndpoints.completeCommand(id));
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text('Engine command received: $type'),
              backgroundColor: AppColors.warning,
            ),
          );
        }
      },
      vehicleId: active.vehicleId,
    );

    final err = await ref.read(backgroundGpsProvider).start(
          vehicleId: active.vehicleId!,
          bookingId: active.bookingId ?? active.id,
          dio: ref.read(dioProvider),
        );

    if (!SignalRService.instance.isConnected) {
      _ensureFallbackPoll();
    }

    if (err != null && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(err), backgroundColor: AppColors.error),
      );
    }
  }

  Future<void> _stopTracking() async {
    _fallbackPoll?.cancel();
    _fallbackPoll = null;
    _trackingVehicleId = null;
    await ref.read(backgroundGpsProvider).stop();
    await SignalRService.instance.disconnect();
  }

  @override
  Widget build(BuildContext context) {
    final gps = ref.watch(backgroundGpsProvider);
    final lastUpdate = gps.lastHeartbeatAt ??
        (gps.lastPosition != null ? DateTime.now() : null);
    final timeFmt = DateFormat('hh:mm:ss a');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Live Tracking')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 32),
        children: [
          SgCard(
            child: Column(
              children: [
                _StatusRow(
                  label: 'GPS Status',
                  value: gps.isTracking
                      ? (gps.isOnline ? 'Active' : 'Offline buffer')
                      : 'Idle',
                  valueColor: gps.isTracking
                      ? (gps.isOnline ? AppColors.success : AppColors.warning)
                      : AppColors.textSecondary,
                ),
                const Divider(height: 1),
                _StatusRow(
                  label: 'SignalR',
                  value: _signalrStatus == 'connected'
                      ? 'Connected'
                      : _signalrStatus,
                  valueColor: _signalrStatus == 'connected'
                      ? AppColors.success
                      : AppColors.textSecondary,
                ),
                const Divider(height: 1),
                _StatusRow(
                  label: 'Internet',
                  value: gps.isOnline ? 'Online' : 'Offline',
                  valueColor:
                      gps.isOnline ? AppColors.success : AppColors.warning,
                ),
                const Divider(height: 1),
                _StatusRow(
                  label: 'Last Update',
                  value: lastUpdate != null
                      ? timeFmt.format(lastUpdate.toLocal())
                      : '—',
                ),
                if (_lastServerFixAt != null) ...[
                  const Divider(height: 1),
                  _StatusRow(
                    label: 'Server Fix',
                    value: timeFmt.format(_lastServerFixAt!),
                    valueColor: AppColors.success,
                  ),
                ],
                if (_serverAddress != null && _serverAddress!.isNotEmpty) ...[
                  const Divider(height: 1),
                  _StatusRow(
                    label: 'Address',
                    value: _serverAddress!,
                  ),
                ],
                if (gps.queuedCount > 0) ...[
                  const Divider(height: 1),
                  _StatusRow(
                    label: 'Queued points',
                    value: '${gps.queuedCount}',
                    valueColor: AppColors.warning,
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: 36),
          Center(
            child: GestureDetector(
              onTap: gps.isTracking ? _stopTracking : _startTracking,
              child: Container(
                width: 168,
                height: 168,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: Colors.white,
                  border: Border.all(
                    color: gps.isTracking
                        ? AppColors.primary
                        : AppColors.primary.withValues(alpha: 0.45),
                    width: 6,
                  ),
                  boxShadow: AppShadows.card,
                ),
                alignment: Alignment.center,
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(
                      gps.isTracking
                          ? Icons.stop_rounded
                          : Icons.play_arrow_rounded,
                      size: 40,
                      color: AppColors.primary,
                    ),
                    const SizedBox(height: 6),
                    Text(
                      gps.isTracking ? 'STOP\nTRACKING' : 'START\nTRACKING',
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w800,
                        color: AppColors.primary,
                        height: 1.2,
                        letterSpacing: 0.5,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          if (gps.lastPosition != null) ...[
            const SizedBox(height: 28),
            SgCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Current Position',
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                    ),
                  ),
                  const SizedBox(height: 12),
                  _PosRow('Latitude',
                      gps.lastPosition!.latitude.toStringAsFixed(6)),
                  _PosRow('Longitude',
                      gps.lastPosition!.longitude.toStringAsFixed(6)),
                  _PosRow(
                    'Speed',
                    '${(gps.lastPosition!.speed * 3.6).toStringAsFixed(1)} km/h',
                  ),
                  if (gps.lastError != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Text(
                        gps.lastError!,
                        style: const TextStyle(
                          color: AppColors.error,
                          fontSize: 12,
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ],
          const SizedBox(height: 28),
          SgDangerOutlineButton(
            label: 'SOS',
            onPressed: () => _showSosDialog(gps.lastPosition),
          ),
        ],
      ),
    );
  }

  Future<void> _showSosDialog(Position? position) async {
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
          if (position != null) 'latitude': position.latitude,
          if (position != null) 'longitude': position.longitude,
        },
      );
      // ignore: unawaited_futures
      AnalyticsService.instance.sosSent();
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
          SnackBar(
              content: Text('SOS failed: $e'),
              backgroundColor: AppColors.error),
        );
      }
    }
  }
}

class _StatusRow extends StatelessWidget {
  const _StatusRow({
    required this.label,
    required this.value,
    this.valueColor,
  });

  final String label;
  final String value;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 12),
      child: Row(
        children: [
          Text(
            label,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 14,
            ),
          ),
          const Spacer(),
          Text(
            value,
            style: TextStyle(
              color: valueColor ?? AppColors.textPrimary,
              fontSize: 14,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

class _PosRow extends StatelessWidget {
  const _PosRow(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: [
          SizedBox(
            width: 100,
            child: Text(
              label,
              style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 13,
              ),
            ),
          ),
          Text(
            value,
            style: const TextStyle(
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
              fontSize: 13,
            ),
          ),
        ],
      ),
    );
  }
}
