import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:share_plus/share_plus.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../../core/constants/app_theme.dart';
import '../../data/fleet_api.dart';
import '../../domain/fleet_models.dart';
import '../vehicle_detail_screen.dart';
import 'fleet_kpi_strip.dart';
import 'vehicle_comms_buttons.dart';

/// Cached today's trip analytics for a vehicle (keyed by vehicle + calendar day).
final vehicleTodayAnalyticsProvider = FutureProvider.autoDispose
    .family<TripAnalyticsSummary?, int>((ref, vehicleId) async {
  final link = ref.keepAlive();
  final timer = Timer(const Duration(minutes: 8), link.close);
  ref.onDispose(timer.cancel);

  final now = DateTime.now();
  final from = DateTime(now.year, now.month, now.day).toUtc();
  final bundle = await ref.read(fleetApiProvider).getTripAnalytics(
        vehicleId,
        from: from,
        to: now.toUtc(),
      );
  return bundle.summary;
});

/// Enterprise vehicle info sheet for Live Map marker selection.
class VehicleLiveMapSheet extends ConsumerWidget {
  const VehicleLiveMapSheet({
    super.key,
    required this.vehicle,
    required this.follow,
    required this.onFollowToggle,
    required this.onClose,
    required this.onNavigate,
    required this.onCenter,
    this.onCommands,
    this.canCommands = false,
  });

  final FleetVehicleLocation vehicle;
  final bool follow;
  final VoidCallback onFollowToggle;
  final VoidCallback onClose;
  final VoidCallback onNavigate;
  final VoidCallback onCenter;
  final VoidCallback? onCommands;
  final bool canCommands;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final color = fleetStatusColor(vehicle.status);
    final gpsAsync = ref.watch(vehicleGpsInfoProvider(vehicle.vehicleId));
    final todayAsync =
        ref.watch(vehicleTodayAnalyticsProvider(vehicle.vehicleId));
    final tf = DateFormat('dd MMM, HH:mm:ss');

    return DraggableScrollableSheet(
      initialChildSize: 0.28,
      minChildSize: 0.18,
      maxChildSize: 0.58,
      snap: true,
      snapSizes: const [0.18, 0.28, 0.58],
      builder: (context, scrollController) {
        return Material(
          color: Colors.white,
          elevation: 10,
          shadowColor: const Color(0x33000000),
          borderRadius: const BorderRadius.vertical(top: Radius.circular(20)),
          clipBehavior: Clip.antiAlias,
          child: SafeArea(
            top: false,
            child: ListView(
              controller: scrollController,
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 20),
              children: [
                Center(
                  child: Container(
                    width: 40,
                    height: 4,
                    decoration: BoxDecoration(
                      color: AppColors.border,
                      borderRadius: BorderRadius.circular(4),
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      width: 10,
                      height: 48,
                      decoration: BoxDecoration(
                        color: color,
                        borderRadius: BorderRadius.circular(4),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            vehicle.vehicleName,
                            style: const TextStyle(
                              fontWeight: FontWeight.w800,
                              fontSize: 17,
                            ),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            vehicle.registrationNumber,
                            style: const TextStyle(
                              fontWeight: FontWeight.w600,
                              color: AppColors.textSecondary,
                            ),
                          ),
                          if ((vehicle.driverName ?? '').isNotEmpty)
                            Text(
                              'Driver ${vehicle.driverName}',
                              style: const TextStyle(
                                fontSize: 13,
                                color: AppColors.textMuted,
                              ),
                            ),
                        ],
                      ),
                    ),
                    IconButton(
                      tooltip: 'Close',
                      onPressed: onClose,
                      icon: const Icon(Icons.close_rounded),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: [
                    _Chip(label: vehicle.status.label, color: color),
                    _Chip(label: '${vehicle.speed.toStringAsFixed(0)} km/h'),
                    _Chip(
                      label: vehicle.ignition == true
                          ? 'Ignition ON'
                          : vehicle.ignition == false
                              ? 'Ignition OFF'
                              : 'Ignition —',
                    ),
                    _Chip(label: vehicle.hasGps ? 'GPS OK' : 'No GPS'),
                    if (vehicle.lastUpdated != null)
                      _Chip(label: tf.format(vehicle.lastUpdated!.toLocal())),
                  ],
                ),
                const SizedBox(height: 14),
                const _SectionTitle('Today'),
                todayAsync.when(
                  loading: () => const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: Center(
                      child: SizedBox(
                        width: 22,
                        height: 22,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      ),
                    ),
                  ),
                  error: (_, __) => const Text(
                    'Today stats unavailable',
                    style: TextStyle(color: AppColors.textMuted, fontSize: 13),
                  ),
                  data: (s) {
                    if (s == null) {
                      return const Text(
                        'No trips today',
                        style:
                            TextStyle(color: AppColors.textMuted, fontSize: 13),
                      );
                    }
                    return _StatsGrid(
                      items: [
                        _Stat(
                          'Distance',
                          '${s.distanceKm.toStringAsFixed(1)} km',
                        ),
                        _Stat('Driving', _fmtMin(s.drivingMinutes)),
                        _Stat('Idle', _fmtMin(s.idleMinutes)),
                        _Stat(
                          'Max',
                          '${s.maxSpeedKmh.toStringAsFixed(0)} km/h',
                        ),
                        _Stat(
                          'Avg',
                          '${s.avgSpeedKmh.toStringAsFixed(0)} km/h',
                        ),
                        _Stat(
                          'Engine',
                          s.engineHours != null
                              ? '${s.engineHours!.toStringAsFixed(1)} h'
                              : '—',
                        ),
                      ],
                    );
                  },
                ),
                const SizedBox(height: 14),
                const _SectionTitle('Location'),
                if (vehicle.hasMapCoords)
                  FutureBuilder<ReverseGeocodeInfo?>(
                    future: _resolveExactLocation(ref, vehicle),
                    builder: (context, snap) {
                      final info = snap.data;
                      final place = info?.placeName?.trim();
                      final address = info?.formattedAddress.trim().isNotEmpty == true
                          ? info!.formattedAddress.trim()
                          : (vehicle.address?.trim().isNotEmpty == true
                              ? vehicle.address!.trim()
                              : (snap.connectionState == ConnectionState.waiting
                                  ? 'Resolving exact location…'
                                  : 'Address unavailable'));
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (place != null && place.isNotEmpty) ...[
                            Text(
                              place,
                              style: const TextStyle(
                                fontSize: 15,
                                fontWeight: FontWeight.w800,
                                height: 1.3,
                              ),
                            ),
                            if ((info?.placeType ?? '').isNotEmpty)
                              Padding(
                                padding: const EdgeInsets.only(top: 2),
                                child: Text(
                                  info!.placeType!.replaceAll('_', ' '),
                                  style: const TextStyle(
                                    fontSize: 11,
                                    color: AppColors.textMuted,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ),
                            const SizedBox(height: 4),
                          ],
                          Text(
                            address,
                            style: const TextStyle(fontSize: 13, height: 1.35),
                          ),
                          const SizedBox(height: 6),
                          SelectableText(
                            '${vehicle.latitude!.toStringAsFixed(5)}, ${vehicle.longitude!.toStringAsFixed(5)}',
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.textSecondary,
                              fontFamily: 'monospace',
                            ),
                          ),
                          const SizedBox(height: 6),
                          TextButton.icon(
                            onPressed: () => _openGoogleMaps(vehicle),
                            icon: const Icon(Icons.map_outlined, size: 18),
                            label: const Text('View on Google Maps'),
                            style: TextButton.styleFrom(
                              padding: EdgeInsets.zero,
                              visualDensity: VisualDensity.compact,
                              foregroundColor: AppColors.primary,
                            ),
                          ),
                        ],
                      );
                    },
                  )
                else
                  const Text(
                    'Address unavailable',
                    style: TextStyle(fontSize: 13, height: 1.35),
                  ),
                const SizedBox(height: 14),
                const _SectionTitle('Device'),
                gpsAsync.when(
                  loading: () => const Text(
                    'Loading device…',
                    style: TextStyle(fontSize: 13, color: AppColors.textMuted),
                  ),
                  error: (_, __) => _DeviceFallback(vehicle: vehicle),
                  data: (gps) {
                    final model = [
                      if ((gps.brandName ?? '').isNotEmpty) gps.brandName!,
                      if ((gps.modelName ?? '').isNotEmpty) gps.modelName!,
                    ].join(' ');
                    return _StatsGrid(
                      items: [
                        _Stat(
                          'Battery',
                          (gps.batteryLevel ?? vehicle.batteryLevel) != null
                              ? '${(gps.batteryLevel ?? vehicle.batteryLevel)!.toStringAsFixed(0)}%'
                              : '—',
                        ),
                        _Stat(
                          'GSM',
                          (gps.gsmSignal ?? vehicle.gsmSignal) != null
                              ? '${gps.gsmSignal ?? vehicle.gsmSignal}'
                              : '—',
                        ),
                        _Stat(
                          'GPS',
                          gps.gpsOnline || vehicle.hasGps ? 'Online' : 'Offline',
                        ),
                        _Stat('Model', model.isEmpty ? '—' : model),
                      ],
                    );
                  },
                ),
                if ((vehicle.driverPhone ?? '').isNotEmpty) ...[
                  const SizedBox(height: 12),
                  VehicleCommsButtons(
                    phone: vehicle.driverPhone,
                    vehicleLabel: vehicle.vehicleName,
                  ),
                ],
                const SizedBox(height: 14),
                const _SectionTitle('Actions'),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    _ActionChip(
                      icon: follow ? Icons.gps_fixed : Icons.gps_not_fixed,
                      label: follow ? 'Following' : 'Live track',
                      selected: follow,
                      onTap: () {
                        onFollowToggle();
                        onCenter();
                      },
                    ),
                    _ActionChip(
                      icon: Icons.timeline_outlined,
                      label: 'Playback',
                      onTap: () => context.push(
                        '/fleet/vehicles/${vehicle.vehicleId}/history',
                      ),
                    ),
                    _ActionChip(
                      icon: Icons.navigation_outlined,
                      label: 'Navigate',
                      onTap: onNavigate,
                    ),
                    _ActionChip(
                      icon: Icons.share_outlined,
                      label: 'Share',
                      onTap: () => _share(vehicle),
                    ),
                    _ActionChip(
                      icon: Icons.streetview_outlined,
                      label: 'Street View',
                      onTap: () => _streetView(vehicle),
                    ),
                    _ActionChip(
                      icon: Icons.info_outline,
                      label: 'Details',
                      onTap: () =>
                          context.push('/fleet/vehicles/${vehicle.vehicleId}'),
                    ),
                    if (canCommands && onCommands != null)
                      _ActionChip(
                        icon: Icons.power_settings_new_outlined,
                        label: 'Commands',
                        onTap: onCommands!,
                      ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  static Future<ReverseGeocodeInfo?> _resolveExactLocation(
    WidgetRef ref,
    FleetVehicleLocation v,
  ) async {
    if (!v.hasMapCoords) return null;
    final inline = v.address?.trim();
    final coarse = inline == null || inline.isEmpty || _isCoarse(inline);
    try {
      return await ref.read(fleetApiProvider).reverseGeocodeInfo(
            v.latitude!,
            v.longitude!,
            forceRefresh: coarse,
          );
    } catch (_) {
      if (inline != null && inline.isNotEmpty) {
        return ReverseGeocodeInfo(formattedAddress: inline);
      }
      return ReverseGeocodeInfo(
        formattedAddress:
            '${v.latitude!.toStringAsFixed(5)}, ${v.longitude!.toStringAsFixed(5)}',
      );
    }
  }

  static bool _isCoarse(String address) {
    final lower = address.toLowerCase();
    if (lower.contains('tehsil') ||
        lower.contains('district') ||
        lower.contains('division')) {
      return true;
    }
    final parts =
        address.split(',').map((e) => e.trim()).where((e) => e.isNotEmpty).toList();
    if (parts.any((p) => RegExp(r'\d').hasMatch(p))) return false;
    return parts.length <= 3;
  }

  static Future<void> _openGoogleMaps(FleetVehicleLocation v) async {
    if (!v.hasMapCoords) return;
    final uri = Uri.parse(
      'https://www.google.com/maps/search/?api=1&query=${v.latitude},${v.longitude}',
    );
    await launchUrl(uri, mode: LaunchMode.externalApplication);
  }

  static String _fmtMin(int minutes) {
    if (minutes < 60) return '${minutes}m';
    final h = minutes ~/ 60;
    final m = minutes % 60;
    return m == 0 ? '${h}h' : '${h}h ${m}m';
  }

  static Future<void> _share(FleetVehicleLocation v) async {
    if (!v.hasMapCoords) return;
    final text =
        '${v.vehicleName} (${v.registrationNumber})\n'
        '${v.latitude}, ${v.longitude}\n'
        'https://www.google.com/maps/search/?api=1&query=${v.latitude},${v.longitude}';
    await Share.share(text, subject: 'Vehicle location');
  }

  static Future<void> _streetView(FleetVehicleLocation v) async {
    if (!v.hasMapCoords) return;
    final uri = Uri.parse(
      'https://www.google.com/maps/@?api=1&map_action=pano'
      '&viewpoint=${v.latitude},${v.longitude}',
    );
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    } else {
      await Clipboard.setData(
        ClipboardData(text: '${v.latitude}, ${v.longitude}'),
      );
    }
  }
}

class _DeviceFallback extends StatelessWidget {
  const _DeviceFallback({required this.vehicle});
  final FleetVehicleLocation vehicle;

  @override
  Widget build(BuildContext context) {
    return _StatsGrid(
      items: [
        _Stat(
          'Battery',
          vehicle.batteryLevel != null
              ? '${vehicle.batteryLevel!.toStringAsFixed(0)}%'
              : '—',
        ),
        _Stat(
          'GSM',
          vehicle.gsmSignal != null ? '${vehicle.gsmSignal}' : '—',
        ),
        _Stat('GPS', vehicle.hasGps ? 'Online' : 'Offline'),
        const _Stat('Model', '—'),
      ],
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Text(
        text,
        style: const TextStyle(
          fontWeight: FontWeight.w800,
          fontSize: 13,
          color: AppColors.textSecondary,
        ),
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.label, this.color});
  final String label;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final c = color ?? AppColors.textSecondary;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: c.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(AppRadii.pill),
        border: Border.all(color: c.withValues(alpha: 0.35)),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w700,
          color: c,
        ),
      ),
    );
  }
}

class _Stat {
  const _Stat(this.label, this.value);
  final String label;
  final String value;
}

class _StatsGrid extends StatelessWidget {
  const _StatsGrid({required this.items});
  final List<_Stat> items;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        for (final s in items)
          SizedBox(
            width: (MediaQuery.sizeOf(context).width - 48) / 3 - 6,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: AppColors.border),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    s.label,
                    style: const TextStyle(
                      fontSize: 10,
                      color: AppColors.textMuted,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    s.value,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ],
              ),
            ),
          ),
      ],
    );
  }
}

class _ActionChip extends StatelessWidget {
  const _ActionChip({
    required this.icon,
    required this.label,
    required this.onTap,
    this.selected = false,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool selected;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected
          ? AppColors.primary.withValues(alpha: 0.12)
          : AppColors.surface,
      borderRadius: BorderRadius.circular(AppRadii.pill),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadii.pill),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.pill),
            border: Border.all(
              color: selected ? AppColors.primary : AppColors.border,
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                icon,
                size: 16,
                color: selected ? AppColors.primary : AppColors.textPrimary,
              ),
              const SizedBox(width: 6),
              Text(
                label,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  color: selected ? AppColors.primary : AppColors.textPrimary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
