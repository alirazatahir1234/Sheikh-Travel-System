import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../fleet/data/fleet_api.dart';
import '../../fleet/domain/fleet_models.dart';
import '../../fleet/domain/gps_freshness.dart';
import '../../fleet/presentation/fleet_hub_notifier.dart';
import '../../fleet/presentation/widgets/fleet_kpi_strip.dart';
import '../../fleet/presentation/widgets/geofence_distance.dart';

/// Parking hub — zone cards + parked vehicle cards.
///
/// There is no dedicated Parking.* API. Cards are fed from existing GPS data:
/// geofences categorized/named as parking, and live fleet vehicles with
/// [FleetTrackStatus.parked].
class ParkingScreen extends ConsumerStatefulWidget {
  const ParkingScreen({super.key});

  @override
  ConsumerState<ParkingScreen> createState() => _ParkingScreenState();
}

class _ParkingScreenState extends ConsumerState<ParkingScreen> {
  List<GpsGeofenceItem> _zones = const [];
  bool _zonesLoading = true;
  String? _zonesError;

  @override
  void initState() {
    super.initState();
    _loadZones();
  }

  Future<void> _loadZones() async {
    setState(() {
      _zonesLoading = true;
      _zonesError = null;
    });
    try {
      final rows = await ref.read(fleetApiProvider).getGeofences();
      if (!mounted) return;
      setState(() {
        _zones = rows.where((g) => g.isActive && g.isParkingZone).toList()
          ..sort((a, b) => a.name.compareTo(b.name));
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _zonesError = ErrorHandler.message(e));
    } finally {
      if (mounted) setState(() => _zonesLoading = false);
    }
  }

  Future<void> _refreshAll() async {
    await Future.wait([
      _loadZones(),
      ref.read(fleetHubProvider.notifier).refresh(),
    ]);
  }

  @override
  Widget build(BuildContext context) {
    final hubAsync = ref.watch(fleetHubProvider);
    final hub = hubAsync.valueOrNull;
    final parked = (hub?.locations ?? const <FleetVehicleLocation>[])
        .where((v) => v.status == FleetTrackStatus.parked)
        .toList()
      ..sort((a, b) => a.vehicleName.compareTo(b.vehicleName));

    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(
        title: const Text('Parking'),
        actions: [
          IconButton(
            onPressed: _refreshAll,
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'Refresh',
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _refreshAll,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
          children: [
            SgCard(
              elevated: false,
              padding: const EdgeInsets.all(14),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    Icons.info_outline_rounded,
                    size: 20,
                    color: AppColors.primary.withValues(alpha: 0.9),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'No dedicated Parking API yet. Zone cards use geofences '
                      'tagged as Parking in the ERP. Vehicle cards use live '
                      'fleet status (Parked) and show charge / ignition metrics.',
                      style: TextStyle(
                        fontSize: 13,
                        height: 1.35,
                        color: context.sg.textSecondary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),
            _SectionHeader(
              icon: Icons.local_parking_rounded,
              title: 'Parking zones',
              count: _zonesLoading ? null : _zones.length,
            ),
            const SizedBox(height: 10),
            if (_zonesLoading)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 24),
                child: Center(child: SgBrandLoader()),
              )
            else if (_zonesError != null)
              SgErrorView(message: _zonesError!, onRetry: _loadZones)
            else if (_zones.isEmpty)
              const SgEmptyState(
                icon: Icons.local_parking_outlined,
                title: 'No parking zones',
                subtitle:
                    'Create a geofence in the ERP with category “Parking” '
                    'to see zone cards here.',
              )
            else
              ..._zones.map(
                (z) => _ParkingZoneCard(
                  zone: z,
                  vehicles: hub?.locations ?? const [],
                ),
              ),
            const SizedBox(height: 24),
            _SectionHeader(
              icon: Icons.directions_car_filled_rounded,
              title: 'Parked vehicles',
              count: hubAsync.isLoading && hub == null ? null : parked.length,
            ),
            const SizedBox(height: 10),
            if (hubAsync.isLoading && hub == null)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 24),
                child: Center(child: SgBrandLoader()),
              )
            else if (hubAsync.hasError && hub == null)
              SgErrorView(
                message: ErrorHandler.message(hubAsync.error!),
                onRetry: () => ref.read(fleetHubProvider.notifier).refresh(),
              )
            else if (parked.isEmpty)
              const SgEmptyState(
                icon: Icons.directions_car_outlined,
                title: 'No vehicles parked',
                subtitle:
                    'Vehicles marked Parked on the live fleet feed will appear '
                    'here with charge and status metrics.',
              )
            else
              ...parked.map((v) => _ParkedVehicleCard(vehicle: v)),
          ],
        ),
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({
    required this.icon,
    required this.title,
    this.count,
  });

  final IconData icon;
  final String title;
  final int? count;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, size: 20, color: AppColors.primary),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            title,
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: context.sg.textPrimary,
            ),
          ),
        ),
        if (count != null)
          StatusBadge(
            '$count',
            color: AppColors.primary,
            radius: AppRadii.md,
          ),
      ],
    );
  }
}

/// Zone card — same SgCard / metric-grid language as fleet vehicle tiles.
class _ParkingZoneCard extends StatelessWidget {
  const _ParkingZoneCard({
    required this.zone,
    required this.vehicles,
  });

  final GpsGeofenceItem zone;
  final List<FleetVehicleLocation> vehicles;

  @override
  Widget build(BuildContext context) {
    final accent = _parseColor(zone.color) ?? const Color(0xFF0D9488);
    final inside = _vehiclesInsideZone(zone, vehicles);
    final shape = zone.isCircle
        ? 'Circle · ${zone.radiusMeters.toStringAsFixed(0)} m'
        : 'Polygon';
    final category = (zone.category ?? '').trim().isEmpty
        ? 'Parking'
        : zone.category!.trim();

    return SgCard(
      margin: const EdgeInsets.only(bottom: 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  color: accent.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadii.sm),
                ),
                child: Icon(Icons.local_parking_rounded,
                    color: accent, size: 20),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      zone.name,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                        color: context.sg.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '$category · $shape',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 12,
                        color: context.sg.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              StatusBadge(
                zone.isActive ? 'Active' : 'Inactive',
                color: zone.isActive ? AppColors.success : context.sg.textMuted,
              ),
            ],
          ),
          if ((zone.description ?? '').trim().isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(
              zone.description!.trim(),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: 12,
                color: context.sg.textSecondary,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
          const SizedBox(height: 10),
          const Divider(height: 1),
          const SizedBox(height: 10),
          _MetricGrid(
            items: [
              _MetricItem('Vehicles inside', '${inside.length}'),
              _MetricItem(
                'Assigned',
                zone.assignedVehicleCount > 0
                    ? '${zone.assignedVehicleCount}'
                    : '—',
              ),
              _MetricItem(
                'Center',
                '${zone.centerLat.toStringAsFixed(4)}, '
                '${zone.centerLng.toStringAsFixed(4)}',
              ),
              _MetricItem('Status', zone.isActive ? 'Monitoring' : 'Off'),
            ],
          ),
          if (inside.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(
              inside
                  .take(3)
                  .map((v) => v.registrationNumber)
                  .join(' · '),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: 12,
                color: context.sg.textSecondary,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ],
      ),
    );
  }

  static List<FleetVehicleLocation> _vehiclesInsideZone(
    GpsGeofenceItem zone,
    List<FleetVehicleLocation> vehicles,
  ) {
    if (!zone.isCircle || zone.radiusMeters <= 0) return const [];
    final out = <FleetVehicleLocation>[];
    for (final v in vehicles) {
      if (!v.hasMapCoords) continue;
      final n = nearestCircleGeofence(
        lat: v.latitude!,
        lng: v.longitude!,
        fences: [zone],
      );
      if (n?.inside == true) out.add(v);
    }
    return out;
  }

  static Color? _parseColor(String? raw) {
    if (raw == null || raw.trim().isEmpty) return null;
    var hex = raw.trim();
    if (hex.startsWith('#')) hex = hex.substring(1);
    if (hex.length == 6) hex = 'FF$hex';
    if (hex.length != 8) return null;
    final value = int.tryParse(hex, radix: 16);
    if (value == null) return null;
    return Color(value);
  }
}

/// Parked vehicle card — mirrors [FleetVehicleTile] online layout with charge.
class _ParkedVehicleCard extends StatelessWidget {
  const _ParkedVehicleCard({required this.vehicle});

  final FleetVehicleLocation vehicle;

  @override
  Widget build(BuildContext context) {
    final badgeColor = fleetStatusColor(FleetTrackStatus.parked);
    final charge = vehicle.batteryLevel == null
        ? 'Not available'
        : '${vehicle.batteryLevel!.toStringAsFixed(0)}%';

    return SgCard(
      margin: const EdgeInsets.only(bottom: 10),
      onTap: () => context.push('/fleet/vehicles/${vehicle.vehicleId}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  color: badgeColor.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadii.sm),
                ),
                child: Icon(Icons.local_parking_rounded,
                    color: badgeColor, size: 20),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      vehicle.vehicleName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: 15,
                        color: context.sg.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      vehicle.registrationNumber,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 12,
                        color: context.sg.textSecondary,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'Driver: ${(vehicle.driverName ?? '').trim().isEmpty ? 'Not assigned' : vehicle.driverName!}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 13,
                        color: context.sg.textPrimary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
              StatusBadge('Parked', color: badgeColor),
            ],
          ),
          const SizedBox(height: 10),
          const Divider(height: 1),
          const SizedBox(height: 10),
          _MetricGrid(
            items: [
              _MetricItem('Charge', charge),
              _MetricItem(
                'Ignition',
                vehicle.ignition == true
                    ? 'ON'
                    : vehicle.ignition == false
                        ? 'OFF'
                        : 'Not available',
              ),
              _MetricItem(
                'Speed',
                formatDisplaySpeedLabel(
                  speed: vehicle.speed,
                  ignition: vehicle.ignition,
                  status: vehicle.status,
                  latitude: vehicle.latitude,
                  longitude: vehicle.longitude,
                ),
              ),
              _MetricItem(
                'Last GPS update',
                formatRelativeAge(vehicle.lastUpdated),
              ),
            ],
          ),
          if ((vehicle.address ?? '').trim().isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(
              vehicle.address!,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: 12,
                color: context.sg.textSecondary,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
          const SizedBox(height: 8),
          const Align(
            alignment: Alignment.centerRight,
            child: Text(
              'View →',
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
                color: AppColors.primary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _MetricGrid extends StatelessWidget {
  const _MetricGrid({required this.items});

  final List<_MetricItem> items;

  @override
  Widget build(BuildContext context) {
    final rows = <Widget>[];
    for (var i = 0; i < items.length; i += 2) {
      final left = items[i];
      final right = i + 1 < items.length ? items[i + 1] : null;
      rows.add(
        Padding(
          padding: EdgeInsets.only(bottom: i + 2 < items.length ? 10 : 0),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: _Metric(label: left.label, value: left.value),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: right == null
                    ? const SizedBox.shrink()
                    : _Metric(label: right.label, value: right.value),
              ),
            ],
          ),
        ),
      );
    }
    return Column(children: rows);
  }
}

class _Metric extends StatelessWidget {
  const _Metric({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            fontSize: 11,
            color: context.sg.textMuted,
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: 3),
        Text(
          value,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            fontSize: 13,
            color: context.sg.textPrimary,
            fontWeight: FontWeight.w700,
            height: 1.2,
          ),
        ),
      ],
    );
  }
}

class _MetricItem {
  const _MetricItem(this.label, this.value);

  final String label;
  final String value;
}
