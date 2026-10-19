import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import 'package:permission_handler/permission_handler.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/api/api_endpoints.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/analytics/analytics_service.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../features/trips/domain/trip_model.dart';
import '../../../features/trips/presentation/trips_notifier.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../fleet/domain/fleet_models.dart';
import '../services/background_gps_tracker.dart';
import '../services/signalr_service.dart';
import 'background_location_disclosure.dart';

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
  bool _batteryHintShown = false;

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
    // Keep SignalR alive while background GPS sharing is still active.
    // Disconnect only when the user stops sharing (see _stopTracking).
    if (!BackgroundGpsTracker.instance.isTracking) {
      SignalRService.instance.disconnect();
    }
    super.dispose();
  }

  void _ensureFallbackPoll() {
    if (_trackingVehicleId == null) return;
    if (_fallbackPoll != null) return;
    // Staff vehicle-GPS poll is often unauthorized for driver-only sessions;
    // outbound background tracker remains the source of truth.
    final session = ref.read(authRepositoryProvider).session;
    if (session != null &&
        (session.isDriverOnly || session.authMode == AuthMode.driver)) {
      return;
    }
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

  Trip? _activeTripWithVehicle(List<Trip> trips) {
    return trips
        .where((t) => (t.isStarted || t.isConfirmed) && t.vehicleId != null)
        .firstOrNull;
  }

  Future<void> _startTracking() async {
    final trips = ref.read(tripsProvider).valueOrNull ?? [];
    final active = _activeTripWithVehicle(trips);
    if (active?.vehicleId == null) {
      // Gate is also shown in the body; keep a soft fallback if Start is tapped.
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Start or confirm a trip with a vehicle before sharing location.',
          ),
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
          onBackgroundLocationDisclosure: () async {
            if (!mounted) return false;
            return showBackgroundLocationDisclosure(context);
          },
        );

    if (!SignalRService.instance.isConnected) {
      _ensureFallbackPoll();
    }

    if (err != null && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(err), backgroundColor: AppColors.error),
      );
    } else if (err == null &&
        mounted &&
        Platform.isAndroid &&
        !_batteryHintShown) {
      _batteryHintShown = true;
      // No REQUEST_IGNORE_BATTERY_OPTIMIZATIONS — optional settings link only.
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text(
            'For reliable tracking, allow unrestricted battery use in Settings.',
          ),
          action: SnackBarAction(
            label: 'Settings',
            onPressed: openAppSettings,
          ),
          duration: const Duration(seconds: 6),
        ),
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
    final trips = ref.watch(tripsProvider).valueOrNull ?? [];
    final activeTrip = _activeTripWithVehicle(trips);
    final canShare = gps.isTracking || activeTrip?.vehicleId != null;
    final lastUpdate = gps.lastHeartbeatAt ??
        (gps.lastPosition != null ? DateTime.now() : null);
    final timeFmt = DateFormat('hh:mm a');
    final p = context.sg;
    final pos = gps.lastPosition;
    final statusLabel = gps.isTracking
        ? (gps.isOnline ? 'Sharing location' : 'Saving offline')
        : 'Not sharing';
    final statusColor = gps.isTracking
        ? (gps.isOnline ? AppColors.success : AppColors.warning)
        : p.textSecondary;

    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(title: const Text('GPS')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 28),
        children: [
          if (!canShare) ...[
            SgEmptyState(
              icon: Icons.location_off_outlined,
              title: 'Trip required to share location',
              subtitle:
                  'Location sharing needs an active trip with a vehicle so dispatch can map points correctly. Confirm or start a trip first.',
              actionLabel: 'Go to Trips',
              onAction: () => context.go('/trips'),
            ),
            const SizedBox(height: 20),
            SgDangerOutlineButton(
              label: 'Emergency SOS',
              onPressed: () => _showSosDialog(gps.lastPosition),
            ),
          ] else ...[
            ClipRRect(
              borderRadius: BorderRadius.circular(AppRadii.xl),
              child: SizedBox(
                height: 220,
                child: pos == null
                    ? Container(
                        color: p.chipBg,
                        alignment: Alignment.center,
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(Icons.map_outlined,
                                size: 40, color: p.textMuted),
                            const SizedBox(height: 8),
                            Text(
                              'Start sharing to see your position',
                              style: TextStyle(color: p.textSecondary),
                            ),
                          ],
                        ),
                      )
                    : GoogleMap(
                        initialCameraPosition: CameraPosition(
                          target: LatLng(pos.latitude, pos.longitude),
                          zoom: 15.2,
                        ),
                        myLocationEnabled: true,
                        myLocationButtonEnabled: false,
                        zoomControlsEnabled: false,
                        liteModeEnabled: false,
                        markers: {
                          Marker(
                            markerId: const MarkerId('me'),
                            position: LatLng(pos.latitude, pos.longitude),
                          ),
                        },
                      ),
              ),
            ),
            const SizedBox(height: 14),
            SgCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(
                        width: 10,
                        height: 10,
                        decoration: BoxDecoration(
                          color: statusColor,
                          shape: BoxShape.circle,
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        statusLabel,
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  color: p.textPrimary,
                                  fontWeight: FontWeight.w700,
                                ),
                      ),
                      const Spacer(),
                      Text(
                        lastUpdate != null
                            ? timeFmt.format(lastUpdate.toLocal())
                            : '—',
                        style: TextStyle(color: p.textMuted, fontSize: 12),
                      ),
                    ],
                  ),
                  if (_serverAddress != null && _serverAddress!.isNotEmpty) ...[
                    const SizedBox(height: 10),
                    Text(
                      _serverAddress!,
                      style: TextStyle(color: p.textSecondary, height: 1.35),
                    ),
                  ],
                  const SizedBox(height: 6),
                  Text(
                    _signalrStatus == 'connected'
                        ? 'Live link connected'
                        : 'Live link $_signalrStatus',
                    style: TextStyle(fontSize: 12, color: p.textMuted),
                  ),
                  if (_lastServerFixAt != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      'Last server update ${timeFmt.format(_lastServerFixAt!)}',
                      style: TextStyle(fontSize: 12, color: p.textMuted),
                    ),
                  ],
                  if (gps.queuedCount > 0) ...[
                    const SizedBox(height: 8),
                    Text(
                      '${gps.queuedCount} points waiting to sync',
                      style: const TextStyle(
                        color: AppColors.warning,
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                  if (gps.lastError != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      gps.lastError!,
                      style:
                          const TextStyle(color: AppColors.error, fontSize: 12),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 16),
            SgPrimaryButton(
              label: gps.isTracking ? 'Stop sharing' : 'Start sharing',
              icon:
                  gps.isTracking ? Icons.stop_rounded : Icons.play_arrow_rounded,
              onPressed: gps.isTracking ? _stopTracking : _startTracking,
            ),
            const SizedBox(height: 10),
            SgDangerOutlineButton(
              label: 'Emergency SOS',
              onPressed: () => _showSosDialog(gps.lastPosition),
            ),
            const SizedBox(height: 8),
            Text(
              'Live location is sent to dispatch while a trip is active.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 12, color: p.textMuted),
            ),
          ],
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

    if (position == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Location required for SOS. Enable GPS and wait for a fix, then try again.',
          ),
          backgroundColor: AppColors.error,
        ),
      );
      return;
    }

    try {
      await ref.read(dioProvider).post(
        ApiEndpoints.sos,
        data: {
          'latitude': position.latitude,
          'longitude': position.longitude,
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
