import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../gps/services/background_gps_tracker.dart';
import '../../gps/services/gps_session_store.dart';
import '../domain/settings_access.dart';

/// Location & Tracking — permissions, status, last update, battery guidance.
/// Does not invent GPS APIs; reuses [BackgroundGpsTracker] + [GpsSessionStore].
class LocationTrackingScreen extends ConsumerStatefulWidget {
  const LocationTrackingScreen({super.key});

  @override
  ConsumerState<LocationTrackingScreen> createState() =>
      _LocationTrackingScreenState();
}

class _LocationTrackingScreenState
    extends ConsumerState<LocationTrackingScreen> {
  PermissionStatus? _whenInUse;
  PermissionStatus? _always;
  bool _serviceEnabled = true;
  bool _loadingPerms = true;

  @override
  void initState() {
    super.initState();
    BackgroundGpsTracker.instance.addListener(_onTracker);
    _refreshPermissions();
  }

  @override
  void dispose() {
    BackgroundGpsTracker.instance.removeListener(_onTracker);
    super.dispose();
  }

  void _onTracker() {
    if (mounted) setState(() {});
  }

  Future<void> _refreshPermissions() async {
    setState(() => _loadingPerms = true);
    final whenInUse = await Permission.locationWhenInUse.status;
    final always = await Permission.locationAlways.status;
    final service = await Geolocator.isLocationServiceEnabled();
    if (!mounted) return;
    setState(() {
      _whenInUse = whenInUse;
      _always = always;
      _serviceEnabled = service;
      _loadingPerms = false;
    });
  }

  String _statusLabel(PermissionStatus? s) {
    if (s == null) return '…';
    if (s.isGranted) return 'Granted';
    if (s.isDenied) return 'Denied';
    if (s.isPermanentlyDenied) return 'Blocked';
    if (s.isRestricted) return 'Restricted';
    if (s.isLimited) return 'Limited';
    return s.name;
  }

  Color _statusColor(PermissionStatus? s) {
    if (s == null) return context.sg.textMuted;
    if (s.isGranted) return AppColors.success;
    if (s.isPermanentlyDenied || s.isDenied) return AppColors.error;
    return AppColors.warning;
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final p = context.sg;
    final session = ref.watch(fleetSessionProvider);
    final tracker = BackgroundGpsTracker.instance;
    final tracking = tracker.isTracking || GpsSessionStore.isActive;
    final canStop = SettingsAccess.canStopBackgroundTracking(session);
    final lastPos = tracker.lastPosition;
    final fmt = DateFormat('dd MMM, HH:mm:ss');
    final lastUpdate = tracker.lastHeartbeatAt ??
        (lastPos != null ? DateTime.now() : GpsSessionStore.startedAt);

    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(title: Text(l10n.locationTracking)),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 32),
        children: [
          _StatusHero(
            tracking: tracking,
            subtitle: tracking
                ? l10n.locationTrackingActive
                : l10n.locationTrackingIdle,
          ),
          const SizedBox(height: 16),
          _SectionCard(
            title: l10n.locationPermissions,
            children: [
              if (_loadingPerms)
                const Padding(
                  padding: EdgeInsets.all(16),
                  child: Center(
                    child: SizedBox(
                      width: 22,
                      height: 22,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  ),
                )
              else ...[
                _InfoRow(
                  label: l10n.locationWhenInUse,
                  value: _statusLabel(_whenInUse),
                  valueColor: _statusColor(_whenInUse),
                ),
                _InfoRow(
                  label: l10n.locationAlways,
                  value: _statusLabel(_always),
                  valueColor: _statusColor(_always),
                ),
                _InfoRow(
                  label: l10n.locationServices,
                  value: _serviceEnabled ? l10n.onLabel : l10n.offLabel,
                  valueColor:
                      _serviceEnabled ? AppColors.success : AppColors.error,
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 4, 16, 12),
                  child: OutlinedButton.icon(
                    onPressed: () async {
                      await openAppSettings();
                      await _refreshPermissions();
                    },
                    icon: const Icon(Icons.settings_outlined, size: 18),
                    label: Text(l10n.openSystemSettings),
                  ),
                ),
              ],
            ],
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: l10n.trackingStatus,
            children: [
              _InfoRow(
                label: l10n.status,
                value: tracking ? l10n.active : l10n.inactive,
                valueColor: tracking ? AppColors.success : p.textSecondary,
              ),
              _InfoRow(
                label: l10n.lastUpdate,
                value: lastUpdate != null
                    ? fmt.format(lastUpdate.toLocal())
                    : l10n.notAvailable,
              ),
              if (GpsSessionStore.vehicleId != null)
                _InfoRow(
                  label: l10n.vehicleIdLabel,
                  value: '${GpsSessionStore.vehicleId}',
                ),
              if (tracker.queuedCount > 0)
                _InfoRow(
                  label: l10n.queuedPoints,
                  value: '${tracker.queuedCount}',
                  valueColor: AppColors.warning,
                ),
              if (tracker.lastError != null)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                  child: Text(
                    tracker.lastError!,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.error,
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 12),
          _SectionCard(
            title: l10n.batteryGuidance,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                child: Text(
                  l10n.batteryGuidanceBody,
                  style: TextStyle(
                    fontSize: 13,
                    height: 1.4,
                    color: p.textSecondary,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),
          if (tracking && session?.isDriverSession == true) ...[
            FilledButton.icon(
              onPressed: () => context.push('/live'),
              icon: const Icon(Icons.map_outlined),
              label: Text(l10n.openLiveMap),
            ),
            const SizedBox(height: 8),
            if (!canStop)
              Text(
                l10n.trackingMandatoryHint,
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 12, color: p.textMuted),
              ),
          ],
          if (tracking && canStop) ...[
            OutlinedButton.icon(
              onPressed: () async {
                final stop = await showDialog<bool>(
                  context: context,
                  builder: (dialogContext) => AlertDialog(
                    title: Text(l10n.stopTrackingTitle),
                    content: Text(l10n.stopTrackingBody),
                    actions: [
                      TextButton(
                        onPressed: () => Navigator.of(dialogContext).pop(false),
                        child: Text(l10n.cancel),
                      ),
                      FilledButton(
                        onPressed: () => Navigator.of(dialogContext).pop(true),
                        child: Text(l10n.stop),
                      ),
                    ],
                  ),
                );
                if (stop == true) {
                  await BackgroundGpsTracker.instance.stop();
                  if (context.mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text(l10n.backgroundGpsStopped)),
                    );
                    setState(() {});
                  }
                }
              },
              icon: const Icon(Icons.stop_circle_outlined,
                  color: AppColors.error),
              label: Text(
                l10n.stopTracking,
                style: const TextStyle(color: AppColors.error),
              ),
              style: OutlinedButton.styleFrom(
                side: const BorderSide(color: AppColors.error),
              ),
            ),
          ],
          if (!tracking && session?.isDriverSession == true) ...[
            FilledButton.icon(
              onPressed: () => context.push('/live'),
              icon: const Icon(Icons.gps_fixed),
              label: Text(l10n.openLiveMap),
            ),
          ],
        ],
      ),
    );
  }
}

class _StatusHero extends StatelessWidget {
  const _StatusHero({required this.tracking, required this.subtitle});
  final bool tracking;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final color = tracking ? AppColors.emerald : AppColors.accent;
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [
            AppColors.navy,
            AppColors.navy.withValues(alpha: 0.92),
            color.withValues(alpha: 0.85),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(AppRadii.lg),
      ),
      child: Row(
        children: [
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(
              tracking ? Icons.gps_fixed : Icons.gps_not_fixed,
              color: Colors.white,
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  tracking ? 'Tracking' : 'Not tracking',
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                    fontSize: 17,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  subtitle,
                  style: TextStyle(
                    color: Colors.white.withValues(alpha: 0.85),
                    fontSize: 13,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionCard extends StatelessWidget {
  const _SectionCard({required this.title, required this.children});
  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Container(
      decoration: BoxDecoration(
        color: p.card,
        borderRadius: BorderRadius.circular(AppRadii.md),
        border: Border.all(color: p.border.withValues(alpha: 0.7)),
        boxShadow: AppShadows.card,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 14, 16, 6),
            child: Text(
              title.toUpperCase(),
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w700,
                letterSpacing: 1.1,
                color: p.textSecondary,
              ),
            ),
          ),
          ...children,
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.label,
    required this.value,
    this.valueColor,
  });
  final String label;
  final String value;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Row(
        children: [
          Expanded(
            child: Text(label, style: TextStyle(color: p.textSecondary)),
          ),
          Text(
            value,
            style: TextStyle(
              fontWeight: FontWeight.w600,
              color: valueColor ?? p.textPrimary,
            ),
          ),
        ],
      ),
    );
  }
}
