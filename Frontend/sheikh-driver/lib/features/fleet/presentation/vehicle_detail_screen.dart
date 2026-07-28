import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../alerts/data/gps_alerts_api.dart';
import '../../alerts/domain/gps_alert_models.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import '../domain/fleet_status.dart';
import 'fleet_hub_notifier.dart';
import 'vehicle_commands_sheet.dart';
import 'widgets/fleet_kpi_strip.dart';
import 'widgets/vehicle_comms_buttons.dart';

final vehicleFuelProvider =
    FutureProvider.family<VehicleFuelSummary, int>((ref, id) {
  return ref.watch(fleetApiProvider).getVehicleFuel(id);
});

final vehicleGpsInfoProvider =
    FutureProvider.family<VehicleGpsInfo, int>((ref, id) {
  return ref.watch(fleetApiProvider).getVehicleGps(id);
});

final vehicleGpsAlertsProvider =
    FutureProvider.family<List<GpsAlertEvent>, int>((ref, vehicleId) {
  return ref.read(gpsAlertsApiProvider).listEvents(
        vehicleId: vehicleId,
        datePreset: 'last7',
      );
});

class VehicleDetailScreen extends ConsumerWidget {
  const VehicleDetailScreen({super.key, required this.vehicleId});
  final int vehicleId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(vehicleDetailProvider(vehicleId));
    final hubLocations =
        ref.watch(fleetHubProvider).valueOrNull?.locations ?? const [];
    final hubLoc =
        hubLocations.where((v) => v.vehicleId == vehicleId).firstOrNull;
    final session = ref.watch(fleetSessionProvider);
    final canCommands = session != null &&
        session.hasAnyPermission(const [
          FleetPermissions.gpsCommandSend,
          FleetPermissions.gpsCommandView,
        ]);

    return DefaultTabController(
      length: 6,
      child: Scaffold(
        backgroundColor: AppColors.surface,
        appBar: AppBar(
          title: const Text('Vehicle'),
          actions: [
            IconButton(
              tooltip: 'History playback',
              icon: const Icon(Icons.timeline_outlined),
              onPressed: () =>
                  context.push('/fleet/vehicles/$vehicleId/history'),
            ),
            if (hubLoc?.hasMapCoords == true)
              IconButton(
                tooltip: 'Show on map',
                icon: const Icon(Icons.map_outlined),
                onPressed: () => context.push('/fleet/map'),
              ),
            if (canCommands)
              IconButton(
                tooltip: 'GPS commands',
                icon: const Icon(Icons.power_settings_new_outlined),
                onPressed: () => showVehicleCommandsSheet(context, vehicleId),
              ),
          ],
          bottom: const TabBar(
            isScrollable: true,
            tabs: [
              Tab(text: 'Overview'),
              Tab(text: 'Live'),
              Tab(text: 'Playback'),
              Tab(text: 'Alerts'),
              Tab(text: 'Fuel'),
              Tab(text: 'Device'),
            ],
          ),
        ),
        body: async.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (e, _) => Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text('$e', textAlign: TextAlign.center),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: () =>
                        ref.invalidate(vehicleDetailProvider(vehicleId)),
                    child: const Text('Retry'),
                  ),
                ],
              ),
            ),
          ),
          data: (v) => TabBarView(
            children: [
              _OverviewTab(vehicle: v, live: hubLoc),
              _GpsTab(vehicleId: vehicleId, vehicle: v, live: hubLoc),
              _PlaybackTab(vehicleId: vehicleId),
              _AlertsShortcutTab(vehicleId: vehicleId),
              _FuelTab(vehicleId: vehicleId),
              _DeviceTab(vehicleId: vehicleId),
            ],
          ),
        ),
      ),
    );
  }
}

class _OverviewTab extends StatelessWidget {
  const _OverviewTab({required this.vehicle, this.live});
  final VehicleDetail vehicle;
  final FleetVehicleLocation? live;

  @override
  Widget build(BuildContext context) {
    final status = live?.status ??
        resolveFleetStatus(
          speed: vehicle.locationSpeed,
          ignition: vehicle.engineIgnition,
          lastUpdated: vehicle.locationLastUpdate,
          hasGps: vehicle.hasGpsDevice,
        );
    final color = fleetStatusColor(status);
    final df = DateFormat('dd MMM yyyy, HH:mm');

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
      children: [
        if (vehicle.imageUrl != null && vehicle.imageUrl!.trim().isNotEmpty)
          ClipRRect(
            borderRadius: BorderRadius.circular(AppRadii.md),
            child: Image.network(
              vehicle.imageUrl!,
              height: 160,
              width: double.infinity,
              fit: BoxFit.cover,
              errorBuilder: (_, __, ___) => const SizedBox.shrink(),
            ),
          ),
        if (vehicle.imageUrl != null && vehicle.imageUrl!.trim().isNotEmpty)
          const SizedBox(height: 12),
        SgCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      vehicle.name,
                      style: const TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.w800,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                  StatusBadge(status.label, color: color),
                ],
              ),
              const SizedBox(height: 4),
              Text(
                vehicle.registrationNumber,
                style: const TextStyle(
                  fontSize: 14,
                  color: AppColors.textSecondary,
                  fontWeight: FontWeight.w600,
                ),
              ),
              if (vehicle.make != null || vehicle.model != null) ...[
                const SizedBox(height: 4),
                Text(
                  [
                    vehicle.make,
                    vehicle.model,
                    if (vehicle.year != null) '${vehicle.year}',
                  ].whereType<String>().where((s) => s.isNotEmpty).join(' · '),
                  style: const TextStyle(
                    fontSize: 13,
                    color: AppColors.textMuted,
                  ),
                ),
              ],
            ],
          ),
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(
              child: OutlinedButton.icon(
                onPressed: () =>
                    context.push('/fleet/vehicles/${vehicle.id}/history'),
                icon: const Icon(Icons.history),
                label: const Text('Playback'),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: OutlinedButton.icon(
                onPressed: () => context.push('/fleet/map'),
                icon: const Icon(Icons.map_outlined),
                label: const Text('Live map'),
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        VehicleCommsButtons(
          phone: vehicle.driverPhone,
          vehicleLabel: vehicle.name,
        ),
        const SizedBox(height: 12),
        const SgSectionTitle('Live telemetry'),
        const SizedBox(height: 8),
        SgCard(
          child: Column(
            children: [
              _Row(
                'Speed',
                live != null
                    ? '${live!.speed.toStringAsFixed(0)} km/h'
                    : vehicle.locationSpeed != null
                        ? '${vehicle.locationSpeed!.toStringAsFixed(0)} km/h'
                        : '—',
              ),
              _Row(
                'Ignition',
                (live?.ignition ?? vehicle.engineIgnition) == null
                    ? '—'
                    : (live?.ignition ?? vehicle.engineIgnition)!
                        ? 'On'
                        : 'Off',
              ),
              _Row(
                'Last comms',
                live?.lastUpdated != null
                    ? df.format(live!.lastUpdated!.toLocal())
                    : vehicle.locationLastUpdate != null
                        ? df.format(vehicle.locationLastUpdate!.toLocal())
                        : '—',
              ),
              _Row(
                'Battery',
                live?.batteryLevel != null
                    ? '${live!.batteryLevel!.toStringAsFixed(0)}%'
                    : '—',
              ),
              _Row('GSM', live?.gsmSignal != null ? '${live!.gsmSignal}' : '—'),
            ],
          ),
        ),
        const SizedBox(height: 16),
        const SgSectionTitle('Assignment'),
        const SizedBox(height: 8),
        SgCard(
          child: Column(
            children: [
              _Row('Driver', vehicle.driverName ?? 'Unassigned'),
              _Row('Phone', vehicle.driverPhone ?? '—'),
              _Row('Status', vehicle.status),
              _Row(
                'Mileage',
                vehicle.currentMileage != null
                    ? '${vehicle.currentMileage!.toStringAsFixed(0)} km'
                    : '—',
              ),
              _Row(
                'Next service',
                vehicle.nextServiceDue != null
                    ? df.format(vehicle.nextServiceDue!.toLocal())
                    : '—',
              ),
              if (vehicle.serviceAlert != null &&
                  vehicle.serviceAlert!.isNotEmpty)
                _Row('Service alert', vehicle.serviceAlert!),
            ],
          ),
        ),
      ],
    );
  }
}

class _GpsTab extends ConsumerWidget {
  const _GpsTab({
    required this.vehicleId,
    required this.vehicle,
    this.live,
  });

  final int vehicleId;
  final VehicleDetail vehicle;
  final FleetVehicleLocation? live;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(vehicleGpsInfoProvider(vehicleId));
    final df = DateFormat('dd MMM yyyy, HH:mm');

    return async.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => _ErrorRetry(
        error: e,
        onRetry: () => ref.invalidate(vehicleGpsInfoProvider(vehicleId)),
      ),
      data: (gps) {
        final lat = live?.latitude ?? gps.latitude;
        final lng = live?.longitude ?? gps.longitude;
        final hasCoords = lat != null && lng != null;
        return ListView(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
          children: [
            if (hasCoords)
              ClipRRect(
                borderRadius: BorderRadius.circular(AppRadii.md),
                child: SizedBox(
                  height: 200,
                  child: GoogleMap(
                    initialCameraPosition: CameraPosition(
                      target: LatLng(lat!, lng!),
                      zoom: 15,
                    ),
                    markers: {
                      Marker(
                        markerId: MarkerId('v$vehicleId'),
                        position: LatLng(lat!, lng!),
                        rotation: live?.heading ?? 0,
                      ),
                    },
                    liteModeEnabled: true,
                    zoomControlsEnabled: false,
                    myLocationButtonEnabled: false,
                    mapToolbarEnabled: false,
                  ),
                ),
              ),
            if (hasCoords) const SizedBox(height: 12),
            SgCard(
              child: Column(
                children: [
                  _Row('Device', gps.deviceName ?? '—'),
                  _Row('IMEI / Unique ID', gps.uniqueId ?? vehicle.gpsImei ?? '—'),
                  _Row(
                    'Online',
                    gps.gpsOnline || vehicle.gpsOnline ? 'Yes' : 'No',
                  ),
                  _Row(
                    'Model',
                    [gps.brandName, gps.modelName]
                        .whereType<String>()
                        .where((s) => s.isNotEmpty)
                        .join(' · ')
                        .ifEmpty('—'),
                  ),
                  _Row('SIM', gps.simNumber ?? '—'),
                  _Row(
                    'Speed',
                    live != null
                        ? '${live!.speed.toStringAsFixed(0)} km/h'
                        : gps.speed != null
                            ? '${gps.speed!.toStringAsFixed(0)} km/h'
                            : '—',
                  ),
                  _Row(
                    'Ignition',
                    (live?.ignition ?? gps.lastIgnition) == null
                        ? '—'
                        : (live?.ignition ?? gps.lastIgnition)!
                            ? 'On'
                            : 'Off',
                  ),
                  _Row(
                    'Last update',
                    (live?.lastUpdated ?? gps.lastUpdate) != null
                        ? df.format(
                            (live?.lastUpdated ?? gps.lastUpdate)!.toLocal())
                        : '—',
                  ),
                  _Row(
                    'Coords',
                    (live?.latitude ?? gps.latitude) != null
                        ? '${(live?.latitude ?? gps.latitude)!.toStringAsFixed(5)}, '
                            '${(live?.longitude ?? gps.longitude)!.toStringAsFixed(5)}'
                        : '—',
                  ),
                  _Row('Address', live?.address ?? gps.address ?? '—'),
                  _Row(
                    'Battery',
                    (live?.batteryLevel ?? gps.batteryLevel) != null
                        ? '${(live?.batteryLevel ?? gps.batteryLevel)!.toStringAsFixed(0)}%'
                        : '—',
                  ),
                  _Row(
                    'Odometer',
                    gps.totalDistanceKm != null
                        ? '${gps.totalDistanceKm!.toStringAsFixed(0)} km'
                        : '—',
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: () =>
                  context.push('/fleet/vehicles/$vehicleId/history'),
              icon: const Icon(Icons.play_circle_outline),
              label: const Text('Open history playback'),
            ),
            if (gps.gpsDeviceId != null) ...[
              const SizedBox(height: 8),
              OutlinedButton.icon(
                onPressed: () => showVehicleCommandsSheet(context, vehicleId),
                icon: const Icon(Icons.power_settings_new),
                label: const Text('Engine / GPS commands'),
              ),
            ],
          ],
        );
      },
    );
  }
}

class _FuelTab extends ConsumerWidget {
  const _FuelTab({required this.vehicleId});
  final int vehicleId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(vehicleFuelProvider(vehicleId));
    final df = DateFormat('dd MMM yyyy');

    return async.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => _ErrorRetry(
        error: e,
        onRetry: () => ref.invalidate(vehicleFuelProvider(vehicleId)),
      ),
      data: (summary) {
        return ListView(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
          children: [
            SgCard(
              child: Row(
                children: [
                  Expanded(
                    child: _Kpi(
                      'Liters',
                      summary.totalLiters.toStringAsFixed(0),
                    ),
                  ),
                  Expanded(
                    child: _Kpi(
                      'Cost',
                      summary.totalCost.toStringAsFixed(0),
                    ),
                  ),
                  Expanded(
                    child: _Kpi('Fills', '${summary.totalCount}'),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            if (summary.items.isEmpty)
              const Padding(
                padding: EdgeInsets.all(24),
                child: Center(child: Text('No fuel logs.')),
              )
            else
              ...summary.items.take(12).map(
                (f) {
                  final maxLiters = summary.items
                      .map((e) => e.liters)
                      .fold<double>(0, (a, b) => a > b ? a : b);
                  final frac = maxLiters > 0 ? f.liters / maxLiters : 0;
                  return Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: SgCard(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            '${f.liters.toStringAsFixed(1)} L · PKR ${f.totalCost.toStringAsFixed(0)}',
                            style: const TextStyle(fontWeight: FontWeight.w700),
                          ),
                          const SizedBox(height: 6),
                          LinearProgressIndicator(
                            value: frac.toDouble(),
                            backgroundColor: AppColors.border,
                            color: AppColors.warning,
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${df.format(f.fuelDate.toLocal())}'
                            '${f.station != null ? ' · ${f.station}' : ''}',
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.textSecondary,
                            ),
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
            TextButton(
              onPressed: () => context.push('/fuel'),
              child: const Text('Open fuel logs'),
            ),
          ],
        );
      },
    );
  }
}

class _Kpi extends StatelessWidget {
  const _Kpi(this.label, this.value);
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          value,
          style: const TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w800,
            color: AppColors.primary,
          ),
        ),
        Text(
          label,
          style: const TextStyle(fontSize: 12, color: AppColors.textMuted),
        ),
      ],
    );
  }
}

class _ErrorRetry extends StatelessWidget {
  const _ErrorRetry({required this.error, required this.onRetry});
  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('$error', textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton(onPressed: onRetry, child: const Text('Retry')),
          ],
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
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(
              label,
              style: const TextStyle(
                fontSize: 13,
                color: AppColors.textMuted,
              ),
            ),
          ),
          Expanded(
            child: Text(
              value,
              style: const TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

extension on String {
  String ifEmpty(String fallback) => isEmpty ? fallback : this;
}

class _PlaybackTab extends StatelessWidget {
  const _PlaybackTab({required this.vehicleId});
  final int vehicleId;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        const Text(
          'Pick a range and open the full playback player with map trail and stats.',
          style: TextStyle(color: AppColors.textSecondary, height: 1.4),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            for (final label in ['Last 6h', 'Today', 'Yesterday', 'Last 3 days'])
              ActionChip(
                label: Text(label),
                onPressed: () =>
                    context.push('/fleet/vehicles/$vehicleId/history'),
              ),
          ],
        ),
        const SizedBox(height: 16),
        FilledButton.icon(
          onPressed: () =>
              context.push('/fleet/vehicles/$vehicleId/history'),
          icon: const Icon(Icons.play_circle_outline),
          label: const Text('Open history playback'),
        ),
      ],
    );
  }
}

class _AlertsShortcutTab extends ConsumerWidget {
  const _AlertsShortcutTab({required this.vehicleId});
  final int vehicleId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(vehicleGpsAlertsProvider(vehicleId));
    final df = DateFormat('dd MMM, HH:mm');

    return async.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => _ErrorRetry(
        error: e,
        onRetry: () => ref.invalidate(vehicleGpsAlertsProvider(vehicleId)),
      ),
      data: (alerts) {
        if (alerts.isEmpty) {
          return const Center(child: Text('No GPS alerts in the last 7 days.'));
        }
        return ListView.builder(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
          itemCount: alerts.length,
          itemBuilder: (_, i) {
            final a = alerts[i];
            return Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: SgCard(
                onTap: () => context.push('/alerts/${a.id}'),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            a.eventType.replaceAll('_', ' '),
                            style: const TextStyle(fontWeight: FontWeight.w700),
                          ),
                          Text(
                            a.message,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                          ),
                          Text(
                            df.format(a.timestamp.toLocal()),
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.textMuted,
                            ),
                          ),
                        ],
                      ),
                    ),
                    if (a.canAcknowledge)
                      TextButton(
                        onPressed: () async {
                          await ref.read(gpsAlertsApiProvider).acknowledge(a.id);
                          ref.invalidate(vehicleGpsAlertsProvider(vehicleId));
                        },
                        child: const Text('ACK'),
                      ),
                  ],
                ),
              ),
            );
          },
        );
      },
    );
  }
}

class _DeviceTab extends ConsumerWidget {
  const _DeviceTab({required this.vehicleId});
  final int vehicleId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(vehicleGpsInfoProvider(vehicleId));
    final session = ref.watch(fleetSessionProvider);
    final canCommands = session != null &&
        session.hasAnyPermission(const [
          FleetPermissions.gpsCommandSend,
          FleetPermissions.gpsCommandView,
        ]);

    return async.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => _ErrorRetry(
        error: e,
        onRetry: () => ref.invalidate(vehicleGpsInfoProvider(vehicleId)),
      ),
      data: (info) => ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        children: [
          const SgSectionTitle('Diagnostics'),
          const SizedBox(height: 8),
          SgCard(
            child: Column(
              children: [
                _Row('Device', info.deviceName ?? info.uniqueId ?? '—'),
                _Row('IMEI / Unique ID', info.uniqueId ?? '—'),
                _Row('SIM', info.simNumber ?? '—'),
                _Row('Model', info.modelName ?? info.brandName ?? '—'),
                _Row('Status', info.gpsOnline ? 'Online' : 'Offline'),
                _Row(
                  'Battery',
                  info.batteryLevel != null
                      ? '${info.batteryLevel!.toStringAsFixed(0)}%'
                      : '—',
                ),
                _Row('GSM signal', '${info.gsmSignal ?? '—'}'),
                _Row(
                  'Last packet',
                  info.lastUpdate?.toLocal().toString() ?? '—',
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          if (canCommands)
            FilledButton.icon(
              onPressed: () => showVehicleCommandsSheet(context, vehicleId),
              icon: const Icon(Icons.power_settings_new),
              label: const Text('Device commands'),
            ),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: () =>
                context.push('/fleet/vehicles/$vehicleId/device'),
            icon: const Icon(Icons.sensors),
            label: const Text('Full device health screen'),
          ),
        ],
      ),
    );
  }
}
