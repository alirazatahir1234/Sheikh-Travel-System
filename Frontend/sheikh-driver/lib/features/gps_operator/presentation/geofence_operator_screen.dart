import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../fleet/data/fleet_api.dart';
import '../../fleet/domain/fleet_models.dart';
import '../../fleet/presentation/fleet_hub_notifier.dart';
import '../../fleet/presentation/widgets/geofence_distance.dart';

class GeofenceOperatorScreen extends ConsumerStatefulWidget {
  const GeofenceOperatorScreen({super.key});

  @override
  ConsumerState<GeofenceOperatorScreen> createState() =>
      _GeofenceOperatorScreenState();
}

class _GeofenceOperatorScreenState extends ConsumerState<GeofenceOperatorScreen> {
  List<GpsGeofenceItem> _rows = const [];
  bool _loading = true;
  int? _vehicleId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final rows = await ref.read(fleetApiProvider).getGeofences();
      if (mounted) setState(() => _rows = rows.where((g) => g.isActive).toList());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final hub = ref.watch(fleetHubProvider).valueOrNull;
    final vehicles = hub?.locations ?? const [];
    final selected = _vehicleId == null
        ? null
        : vehicles.where((v) => v.vehicleId == _vehicleId).firstOrNull;

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Geofences'),
        actions: [
          IconButton(
            onPressed: _load,
            icon: const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
            child: DropdownButtonFormField<int?>(
              value: _vehicleId,
              decoration: const InputDecoration(
                labelText: 'Vehicle position check',
                border: OutlineInputBorder(),
              ),
              items: [
                const DropdownMenuItem(value: null, child: Text('All fences')),
                ...vehicles.map(
                  (v) => DropdownMenuItem(
                    value: v.vehicleId,
                    child: Text('${v.vehicleName} · ${v.registrationNumber}'),
                  ),
                ),
              ],
              onChanged: (v) => setState(() => _vehicleId = v),
            ),
          ),
          if (selected != null && selected.hasMapCoords) ...[
            const SizedBox(height: 8),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: SgCard(
                child: Text(
                  _vehicleGeofenceSummary(selected),
                  style: const TextStyle(fontSize: 13),
                ),
              ),
            ),
          ],
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: _rows.length,
                    itemBuilder: (_, i) {
                      final g = _rows[i];
                      String? status;
                      if (selected != null &&
                          selected.hasMapCoords &&
                          g.isCircle &&
                          g.radiusMeters > 0) {
                        final n = nearestCircleGeofence(
                          lat: selected.latitude!,
                          lng: selected.longitude!,
                          fences: [g],
                        );
                        status = n?.inside == true ? 'Inside' : 'Outside';
                      }
                      return Card(
                        child: ListTile(
                          title: Text(g.name),
                          subtitle: Text(
                            g.isCircle
                                ? 'Circle · ${g.radiusMeters.toStringAsFixed(0)} m'
                                : 'Polygon',
                          ),
                          trailing: status != null
                              ? StatusBadge(status)
                              : Icon(
                                  g.isActive
                                      ? Icons.check_circle
                                      : Icons.remove_circle_outline,
                                  color: g.isActive
                                      ? AppColors.success
                                      : AppColors.textMuted,
                                ),
                        ),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }

  String _vehicleGeofenceSummary(FleetVehicleLocation v) {
    final nearest = nearestCircleGeofence(
      lat: v.latitude!,
      lng: v.longitude!,
      fences: _rows,
    );
    if (nearest == null) return 'No circle geofences configured.';
    if (nearest.inside) {
      return 'Inside ${nearest.name}.';
    }
    return 'Nearest: ${nearest.name} (${nearest.distanceMeters.toStringAsFixed(0)} m away).';
  }
}
