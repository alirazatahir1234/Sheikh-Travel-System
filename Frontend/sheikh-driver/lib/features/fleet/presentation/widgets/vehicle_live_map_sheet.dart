import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:share_plus/share_plus.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../../core/constants/app_theme.dart';
import '../../data/fleet_api.dart';
import '../../domain/fleet_models.dart';
import '../../domain/gps_freshness.dart';
import '../fleet_hub_notifier.dart';
import '../vehicle_detail_screen.dart';
import 'fleet_kpi_strip.dart';
import 'live_map_location.dart';
import 'vehicle_comms_buttons.dart';
import '../../../../core/time/tenant_time.dart';

/// Cached reverse-geocode lookup; keyed by rounded coordinate string so that
/// small position drift does not trigger a new API call on every update.
final _reverseGeocodeCache = <String, ReverseGeocodeInfo>{};

/// Resolves a street address for the live-map vehicle sheet.
///
/// Family key is only `(vehicleId, coordKey)` so SignalR `address` churn does
/// not restart the lookup and flash raw coordinates.
final vehicleLocationProvider = FutureProvider.autoDispose
    .family<ReverseGeocodeInfo?, ({int vehicleId, String coordKey})>(
  (ref, args) async {
    // 1. Serve from memory cache first (survives rebuild / position drift)
    final cached = _reverseGeocodeCache[args.coordKey];
    if (cached != null) return cached;

    // Prefer hub roster address when already street-level (no extra API call).
    final hubAddress = ref
        .read(fleetHubProvider)
        .valueOrNull
        ?.locations
        .where((v) => v.vehicleId == args.vehicleId)
        .map((v) => v.address)
        .firstOrNull;
    final inline = hubAddress?.trim();
    if (inline != null &&
        inline.isNotEmpty &&
        !liveMapAddressNeedsReverseGeocode(inline)) {
      final result = ReverseGeocodeInfo(formattedAddress: inline);
      _reverseGeocodeCache[args.coordKey] = result;
      return result;
    }

    final parts = args.coordKey.split(',');
    if (parts.length != 2) return null;
    final lat = double.tryParse(parts[0]);
    final lng = double.tryParse(parts[1]);
    if (lat == null || lng == null) return null;

    // Explicit marker tap: upgrade coarse / missing / coords-only cache rows.
    final forceRefresh = liveMapAddressNeedsReverseGeocode(inline);
    try {
      final info = await ref.read(fleetApiProvider).reverseGeocodeInfo(
            lat,
            lng,
            forceRefresh: forceRefresh,
          );
      if (info == null) return null;
      final formatted = info.formattedAddress.trim();
      if (formatted.isEmpty || isLiveMapCoordsOnlyAddress(formatted)) {
        return null;
      }
      _reverseGeocodeCache[args.coordKey] = info;
      return info;
    } catch (_) {
      return null;
    }
  },
);

/// Cached today's trip analytics for a vehicle (keyed by vehicle + calendar day).
final vehicleTodayAnalyticsProvider = FutureProvider.autoDispose
    .family<TripAnalyticsSummary?, int>((ref, vehicleId) async {
  final link = ref.keepAlive();
  final timer = Timer(const Duration(minutes: 8), link.close);
  ref.onDispose(timer.cancel);

  final now = DateTime.now();
  final from = TenantTime.dayBoundsUtc(now).$1.toUtc();
  final bundle = await ref.read(fleetApiProvider).getTripAnalytics(
        vehicleId,
        from: from,
        to: now.toUtc(),
      );
  return bundle.summary;
});

/// Compact bottom-aligned vehicle card for Live Map marker selection.
///
/// Intentionally **not** a full-screen [DraggableScrollableSheet]: the card
/// only occupies the bottom inset so most of the map stays visible and
/// interactive.
class VehicleLiveMapSheet extends ConsumerStatefulWidget {
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
    this.showClose = true,
    this.showDetailsAction = true,
    this.initiallyExpanded = false,
  });

  final FleetVehicleLocation vehicle;
  final bool follow;
  final VoidCallback onFollowToggle;
  final VoidCallback onClose;
  final VoidCallback onNavigate;
  final VoidCallback onCenter;
  final VoidCallback? onCommands;
  final bool canCommands;
  final bool showClose;
  final bool showDetailsAction;
  final bool initiallyExpanded;

  @override
  ConsumerState<VehicleLiveMapSheet> createState() =>
      _VehicleLiveMapSheetState();
}

class _VehicleLiveMapSheetState extends ConsumerState<VehicleLiveMapSheet> {
  late bool _expanded;

  @override
  void initState() {
    super.initState();
    _expanded = widget.initiallyExpanded;
  }

  @override
  void didUpdateWidget(covariant VehicleLiveMapSheet oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.vehicle.vehicleId != widget.vehicle.vehicleId) {
      _expanded = widget.initiallyExpanded;
    }
  }

  void _toggleExpanded() => setState(() => _expanded = !_expanded);

  @override
  Widget build(BuildContext context) {
    final vehicle = widget.vehicle;
    final color = fleetStatusColor(vehicle.status);
    final gpsAsync = ref.watch(vehicleGpsInfoProvider(vehicle.vehicleId));
    final todayAsync =
        ref.watch(vehicleTodayAnalyticsProvider(vehicle.vehicleId));
    final coordKey = liveMapCoordKey(vehicle.latitude, vehicle.longitude);
    final locationAsync = vehicle.hasMapCoords && coordKey.isNotEmpty
        ? ref.watch(
            vehicleLocationProvider((
              vehicleId: vehicle.vehicleId,
              coordKey: coordKey,
            )),
          )
        : null;

    final maxCardHeight = MediaQuery.sizeOf(context).height * 0.46;
    final p = context.sg;

    // Chat-bubble style card: rounded, soft shadow, teal rim (AI copilot parity).
    return AnimatedSize(
      duration: const Duration(milliseconds: 220),
      curve: Curves.easeOutCubic,
      alignment: Alignment.bottomCenter,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: p.card,
          borderRadius: BorderRadius.circular(AppRadii.lg),
          border: Border.all(
            color: AppColors.primary.withValues(alpha: 0.28),
          ),
          boxShadow: [
            ...AppShadows.card,
            BoxShadow(
              color: AppColors.primary.withValues(alpha: 0.08),
              blurRadius: 18,
              offset: const Offset(0, 8),
            ),
          ],
        ),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(AppRadii.lg),
          child: ConstrainedBox(
            constraints: BoxConstraints(maxHeight: maxCardHeight),
            child: Material(
              color: Colors.transparent,
              child: SafeArea(
                top: false,
                minimum: EdgeInsets.zero,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    GestureDetector(
                      behavior: HitTestBehavior.opaque,
                      onTap: _toggleExpanded,
                      onVerticalDragEnd: (details) {
                        final v = details.primaryVelocity ?? 0;
                        if (v < -200 && !_expanded) {
                          setState(() => _expanded = true);
                        } else if (v > 200 && _expanded) {
                          setState(() => _expanded = false);
                        }
                      },
                      child: Padding(
                        padding: const EdgeInsets.fromLTRB(16, 8, 8, 0),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Center(
                              child: Container(
                                width: 40,
                                height: 4,
                                decoration: BoxDecoration(
                                  color: AppColors.primary.withValues(
                                    alpha: 0.35,
                                  ),
                                  borderRadius: BorderRadius.circular(4),
                                ),
                              ),
                            ),
                            const SizedBox(height: 10),
                            Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Container(
                                  width: 10,
                                  height: 44,
                                  decoration: BoxDecoration(
                                    color: color,
                                    borderRadius: BorderRadius.circular(4),
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        vehicle.vehicleName,
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: const TextStyle(
                                          fontWeight: FontWeight.w800,
                                          fontSize: 16,
                                        ),
                                      ),
                                      const SizedBox(height: 2),
                                      Text(
                                        vehicle.registrationNumber,
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: TextStyle(
                                          fontWeight: FontWeight.w600,
                                          color: context.sg.textSecondary,
                                        ),
                                      ),
                                      if ((vehicle.driverName ?? '')
                                              .isNotEmpty ||
                                          (vehicle.driverPhone ?? '')
                                              .trim()
                                              .isNotEmpty)
                                        Row(
                                          children: [
                                            Expanded(
                                              child: Text(
                                                (vehicle.driverName ?? '')
                                                        .isNotEmpty
                                                    ? 'Driver ${vehicle.driverName}'
                                                    : 'Driver',
                                                maxLines: 1,
                                                overflow: TextOverflow.ellipsis,
                                                style: TextStyle(
                                                  fontSize: 13,
                                                  color: context.sg.textMuted,
                                                ),
                                              ),
                                            ),
                                            VehicleCommsButtons(
                                              phone: vehicle.driverPhone,
                                              vehicleLabel: vehicle.vehicleName,
                                              compact: true,
                                            ),
                                          ],
                                        ),
                                    ],
                                  ),
                                ),
                                IconButton(
                                  tooltip: _expanded ? 'Collapse' : 'Expand',
                                  onPressed: _toggleExpanded,
                                  icon: Icon(
                                    _expanded
                                        ? Icons.keyboard_arrow_down_rounded
                                        : Icons.keyboard_arrow_up_rounded,
                                  ),
                                ),
                                if (widget.showClose)
                                  IconButton(
                                    tooltip: 'Close',
                                    onPressed: widget.onClose,
                                    icon: const Icon(Icons.close_rounded),
                                  ),
                              ],
                            ),
                            const SizedBox(height: 8),
                            Align(
                              alignment: Alignment.centerLeft,
                              child: Wrap(
                                spacing: 6,
                                runSpacing: 6,
                                children: [
                                  _Chip(
                                      label: vehicle.status.label,
                                      color: color),
                                  _Chip(
                                    label: formatDisplaySpeedLabel(
                                      speed: vehicle.speed,
                                      ignition: vehicle.ignition,
                                      status: vehicle.status,
                                      latitude: vehicle.latitude,
                                      longitude: vehicle.longitude,
                                      isGpsValid: vehicle.isGpsValid,
                                    ),
                                  ),
                                  _Chip(
                                    label: vehicle.ignition == true
                                        ? 'Ignition ON'
                                        : vehicle.ignition == false
                                            ? 'Ignition OFF'
                                            : 'Ignition unavailable',
                                  ),
                                  _Chip(
                                    label: formatGpsFreshness(
                                      latitude: vehicle.latitude,
                                      longitude: vehicle.longitude,
                                      lastUpdated: vehicle.lastUpdated,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(height: 10),
                            Align(
                              alignment: Alignment.centerLeft,
                              child: Wrap(
                                spacing: 8,
                                runSpacing: 8,
                                children: [
                                  _ActionChip(
                                    icon: widget.follow
                                        ? Icons.location_disabled
                                        : Icons.my_location,
                                    label: widget.follow
                                        ? 'Following'
                                        : 'Live track',
                                    selected: widget.follow,
                                    onTap: () {
                                      widget.onFollowToggle();
                                      widget.onCenter();
                                    },
                                  ),
                                  _ActionChip(
                                    icon: Icons.timeline_outlined,
                                    label: 'Playback',
                                    onTap: () => context.push(
                                      Uri(
                                        path:
                                            '/fleet/vehicles/${vehicle.vehicleId}/history',
                                      ).toString(),
                                    ),
                                  ),
                                  _ActionChip(
                                    icon: Icons.navigation_outlined,
                                    label: 'Navigate',
                                    onTap: widget.onNavigate,
                                  ),
                                  if (widget.showDetailsAction)
                                    _ActionChip(
                                      icon: Icons.info_outline,
                                      label: 'Details',
                                      onTap: () => context.push(
                                        '/fleet/vehicles/${vehicle.vehicleId}',
                                      ),
                                    ),
                                  if (widget.canCommands &&
                                      widget.onCommands != null)
                                    _ActionChip(
                                      icon: Icons.power_settings_new_outlined,
                                      label: 'Commands',
                                      onTap: widget.onCommands!,
                                    ),
                                ],
                              ),
                            ),
                            const SizedBox(height: 10),
                          ],
                        ),
                      ),
                    ),
                    if (_expanded)
                      ConstrainedBox(
                        constraints: BoxConstraints(
                          maxHeight: (maxCardHeight * 0.58).clamp(120.0, 320.0),
                        ),
                        child: ListView(
                          padding: const EdgeInsets.fromLTRB(16, 0, 16, 14),
                          shrinkWrap: true,
                          children: [
                            const _SectionTitle('Today'),
                            todayAsync.when(
                              loading: () => const Padding(
                                padding: EdgeInsets.symmetric(vertical: 12),
                                child: Center(
                                  child: SizedBox(
                                    width: 22,
                                    height: 22,
                                    child: CircularProgressIndicator(
                                        strokeWidth: 2),
                                  ),
                                ),
                              ),
                              error: (_, __) => Text(
                                'Today stats unavailable',
                                style: TextStyle(
                                  color: context.sg.textMuted,
                                  fontSize: 13,
                                ),
                              ),
                              data: (s) {
                                if (s == null) {
                                  return Text(
                                    'No trips today',
                                    style: TextStyle(
                                      color: context.sg.textMuted,
                                      fontSize: 13,
                                    ),
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
                            const SizedBox(height: 12),
                            const _SectionTitle('Location'),
                            if (vehicle.hasMapCoords) ...[
                              Builder(builder: (context) {
                                final waiting =
                                    locationAsync?.isLoading == true &&
                                        !locationAsync!.hasValue;
                                final display = buildLiveMapLocationDisplay(
                                  vehicle: vehicle,
                                  info: locationAsync?.valueOrNull,
                                  waiting: waiting,
                                );
                                return Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    if (display.isResolving) ...[
                                      Row(
                                        children: [
                                          const SizedBox(
                                            width: 14,
                                            height: 14,
                                            child: CircularProgressIndicator(
                                              strokeWidth: 2,
                                            ),
                                          ),
                                          const SizedBox(width: 8),
                                          Expanded(
                                            child: Text(
                                              'Resolving exact location…',
                                              style: TextStyle(
                                                fontSize: 13,
                                                height: 1.35,
                                                color: context.sg.textMuted,
                                              ),
                                            ),
                                          ),
                                        ],
                                      ),
                                      const SizedBox(height: 8),
                                    ] else ...[
                                      if (display.primary != null) ...[
                                        Text(
                                          display.primary!,
                                          style: const TextStyle(
                                            fontSize: 15,
                                            fontWeight: FontWeight.w800,
                                            height: 1.3,
                                          ),
                                        ),
                                        const SizedBox(height: 4),
                                      ],
                                      if (display.secondary != null)
                                        Text(
                                          display.secondary!,
                                          style: TextStyle(
                                            fontSize: 13,
                                            height: 1.35,
                                            color: display.isUnavailable
                                                ? context.sg.textMuted
                                                : context.sg.textPrimary,
                                          ),
                                        ),
                                      if (display.primary != null ||
                                          display.secondary != null)
                                        const SizedBox(height: 6),
                                    ],
                                    SelectableText(
                                      display.coords,
                                      style: TextStyle(
                                        fontSize: 12,
                                        color: context.sg.textSecondary,
                                        fontFamily: 'monospace',
                                      ),
                                    ),
                                    const SizedBox(height: 6),
                                    TextButton.icon(
                                      onPressed: () => _openGoogleMaps(vehicle),
                                      icon: const Icon(Icons.map_outlined,
                                          size: 18),
                                      label: const Text('View on Google Maps'),
                                      style: TextButton.styleFrom(
                                        padding: EdgeInsets.zero,
                                        visualDensity: VisualDensity.compact,
                                        foregroundColor: AppColors.primary,
                                      ),
                                    ),
                                  ],
                                );
                              }),
                            ] else
                              const Text(
                                'Address unavailable',
                                style: TextStyle(fontSize: 13, height: 1.35),
                              ),
                            const SizedBox(height: 12),
                            const _SectionTitle('Device'),
                            gpsAsync.when(
                              loading: () => Text(
                                'Loading device…',
                                style: TextStyle(
                                  fontSize: 13,
                                  color: context.sg.textMuted,
                                ),
                              ),
                              error: (_, __) =>
                                  _DeviceFallback(vehicle: vehicle),
                              data: (gps) {
                                final tracker = _trackerLabel(gps);
                                final imei = (gps.uniqueId ?? '').trim();
                                final items = <_Stat>[
                                  _Stat(
                                    'Battery',
                                    (gps.batteryLevel ??
                                                vehicle.batteryLevel) !=
                                            null
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
                                    formatGpsFreshness(
                                      latitude: vehicle.latitude,
                                      longitude: vehicle.longitude,
                                      lastUpdated: vehicle.lastUpdated,
                                    ),
                                  ),
                                  _Stat('GPS Tracker', tracker),
                                  if (imei.isNotEmpty) _Stat('IMEI', imei),
                                ];
                                return _StatsGrid(items: items);
                              },
                            ),
                            const SizedBox(height: 12),
                            const _SectionTitle('More'),
                            Wrap(
                              spacing: 8,
                              runSpacing: 8,
                              children: [
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
                              ],
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  static String _trackerLabel(VehicleGpsInfo gps) {
    final brand = gps.brandName?.trim() ?? '';
    final model = gps.modelName?.trim() ?? '';
    final device = gps.deviceName?.trim() ?? '';
    if (brand.isNotEmpty && model.isNotEmpty) {
      if (model.toLowerCase().startsWith(brand.toLowerCase())) return model;
      return '$brand $model';
    }
    if (model.isNotEmpty) return model;
    if (brand.isNotEmpty) return brand;
    if (device.isNotEmpty) return device;
    return '—';
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
    final addr = v.address?.trim();
    final addrLine = (addr != null &&
            addr.isNotEmpty &&
            !liveMapAddressNeedsReverseGeocode(addr))
        ? '$addr\n'
        : '';
    final text = '${v.vehicleName} (${v.registrationNumber})\n'
        '$addrLine'
        '${formatLiveMapCoords(v.latitude!, v.longitude!)}\n'
        'https://www.google.com/maps/search/?api=1&query=${v.latitude},${v.longitude}';
    await SharePlus.instance.share(
      ShareParams(text: text, subject: 'Vehicle location'),
    );
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
        _Stat(
          'GPS',
          formatGpsFreshness(
            latitude: vehicle.latitude,
            longitude: vehicle.longitude,
            lastUpdated: vehicle.lastUpdated,
          ),
        ),
        const _Stat('GPS Tracker', '—'),
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
        style: TextStyle(
          fontWeight: FontWeight.w800,
          fontSize: 13,
          color: context.sg.textSecondary,
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
    final c = color ?? context.sg.textSecondary;
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
    return LayoutBuilder(
      builder: (context, constraints) {
        final cellW = (constraints.maxWidth - 16) / 3;
        return Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            for (final s in items)
              SizedBox(
                width: cellW,
                child: Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
                  decoration: BoxDecoration(
                    color: context.sg.surface,
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: context.sg.border),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        s.label,
                        style: TextStyle(
                          fontSize: 10,
                          color: context.sg.textMuted,
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
      },
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
          : context.sg.surface,
      borderRadius: BorderRadius.circular(AppRadii.pill),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadii.pill),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.pill),
            border: Border.all(
              color: selected ? AppColors.primary : context.sg.border,
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                icon,
                size: 16,
                color: selected ? AppColors.primary : context.sg.textPrimary,
              ),
              const SizedBox(width: 6),
              Text(
                label,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  color: selected ? AppColors.primary : context.sg.textPrimary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
