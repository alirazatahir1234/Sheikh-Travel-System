import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:intl/intl.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../../shared/widgets/sg_ui.dart';
import '../../domain/alert_display.dart';
import '../alerts_notifier.dart';
import 'in_cab_radio_modal.dart';

Future<void> showIncidentDetailSheet(
  BuildContext context,
  WidgetRef ref,
  int alertId,
) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    backgroundColor: AppColors.surface,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
    ),
    builder: (_) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.92,
      minChildSize: 0.5,
      maxChildSize: 0.98,
      builder: (context, scrollController) => IncidentDetailSheet(
        alertId: alertId,
        scrollController: scrollController,
      ),
    ),
  );
}

class IncidentDetailSheet extends ConsumerStatefulWidget {
  const IncidentDetailSheet({
    super.key,
    required this.alertId,
    this.scrollController,
  });

  final int alertId;
  final ScrollController? scrollController;

  @override
  ConsumerState<IncidentDetailSheet> createState() =>
      _IncidentDetailSheetState();
}

class _IncidentDetailSheetState extends ConsumerState<IncidentDetailSheet> {
  final _notes = TextEditingController();

  @override
  void dispose() {
    _notes.dispose();
    super.dispose();
  }

  Color _sevColor(AlertDisplaySeverity s) => switch (s) {
        AlertDisplaySeverity.critical => AppColors.error,
        AlertDisplaySeverity.warning => AppColors.warning,
        AlertDisplaySeverity.resolved => AppColors.success,
        AlertDisplaySeverity.info => AppColors.info,
      };

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(alertDetailProvider(widget.alertId));
    final df = DateFormat('dd MMM yyyy, HH:mm');

    return async.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text('$e', textAlign: TextAlign.center),
        ),
      ),
      data: (a) {
        final sev = alertDisplaySeverity(a);
        final hasMap = a.latitude != 0 || a.longitude != 0;
        final limit = parseSpeedLimitFromMessage(a.message);
        final timeline = alertTimelineNodes(a);
        final isDemo =
            ref.watch(alertsProvider).valueOrNull?.isDemo(a.id) ?? a.id < 0;

        return ListView(
          controller: widget.scrollController,
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                margin: const EdgeInsets.only(bottom: 12),
                decoration: BoxDecoration(
                  color: AppColors.border,
                  borderRadius: BorderRadius.circular(99),
                ),
              ),
            ),
            Row(
              children: [
                Expanded(
                  child: Text(
                    alertTitle(a),
                    style: const TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
                if (isDemo)
                  const StatusBadge('DEMO', color: AppColors.primary),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              a.message.trim().isNotEmpty ? a.message : alertDescription(a),
              style: const TextStyle(
                color: AppColors.textSecondary,
                height: 1.35,
              ),
            ),
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                StatusBadge(alertSeverityLabel(sev), color: _sevColor(sev)),
                StatusBadge(
                  a.status.toUpperCase(),
                  color: AppColors.textSecondary,
                ),
              ],
            ),
            const SizedBox(height: 16),
            if (hasMap)
              ClipRRect(
                borderRadius: BorderRadius.circular(AppRadii.md),
                child: SizedBox(
                  height: 200,
                  child: GoogleMap(
                    initialCameraPosition: CameraPosition(
                      target: LatLng(a.latitude, a.longitude),
                      zoom: 14,
                    ),
                    markers: {
                      Marker(
                        markerId: MarkerId('alert-${a.id}'),
                        position: LatLng(a.latitude, a.longitude),
                        infoWindow: InfoWindow(title: alertTitle(a)),
                      ),
                    },
                    circles: a.geofenceName != null
                        ? {
                            Circle(
                              circleId: CircleId('gf-${a.id}'),
                              center: LatLng(a.latitude, a.longitude),
                              radius: 250,
                              fillColor:
                                  AppColors.error.withValues(alpha: 0.12),
                              strokeColor: AppColors.error,
                              strokeWidth: 2,
                            ),
                          }
                        : {},
                    myLocationButtonEnabled: false,
                    zoomControlsEnabled: false,
                    liteModeEnabled: false,
                  ),
                ),
              )
            else
              SgCard(
                child: Text(
                  'No GPS coordinates for this incident.',
                  style: TextStyle(color: AppColors.textMuted),
                ),
              ),
            if (a.geofenceName != null) ...[
              const SizedBox(height: 8),
              Text(
                'Geofence: ${a.geofenceName}',
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  color: AppColors.textSecondary,
                ),
              ),
            ],
            const SizedBox(height: 16),
            const Text(
              'Telemetry',
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15),
            ),
            const SizedBox(height: 8),
            GridView.count(
              crossAxisCount: 2,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              mainAxisSpacing: 8,
              crossAxisSpacing: 8,
              childAspectRatio: 2.2,
              children: [
                _MetricTile(
                  label: 'Speed recorded',
                  value: a.speed > 0
                      ? '${a.speed.toStringAsFixed(0)} km/h'
                      : '—',
                ),
                _MetricTile(
                  label: 'Speed limit',
                  value: limit != null
                      ? '${limit.toStringAsFixed(0)} km/h'
                      : '—',
                ),
                _MetricTile(
                  label: 'Coordinates',
                  value: hasMap
                      ? '${a.latitude.toStringAsFixed(4)}, ${a.longitude.toStringAsFixed(4)}'
                      : '—',
                ),
                _MetricTile(
                  label: 'Detected',
                  value: df.format(a.timestamp.toLocal()),
                ),
              ],
            ),
            const SizedBox(height: 16),
            SgCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Vehicle & driver',
                    style: TextStyle(fontWeight: FontWeight.w800),
                  ),
                  const SizedBox(height: 8),
                  Text(a.vehicleName ?? 'Vehicle #${a.vehicleId}'),
                  if (a.driverName != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      a.driverName!,
                      style: const TextStyle(color: AppColors.textSecondary),
                    ),
                    const SizedBox(height: 10),
                    OutlinedButton.icon(
                      onPressed: () => showInCabRadioModal(
                        context,
                        driverName: a.driverName!,
                        vehicleLabel: a.vehicleName,
                      ),
                      icon: const Icon(Icons.cell_tower_rounded, size: 18),
                      label: const Text('In-cab radio (demo)'),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 16),
            const Text(
              'Incident timeline',
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15),
            ),
            const SizedBox(height: 8),
            ...timeline.map((n) {
              final c = _sevColor(n.severity);
              return Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      width: 12,
                      height: 12,
                      margin: const EdgeInsets.only(top: 4),
                      decoration: BoxDecoration(
                        color: c,
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            n.label,
                            style: const TextStyle(fontWeight: FontWeight.w700),
                          ),
                          Text(
                            df.format(n.at.toLocal()),
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.textMuted,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              );
            }),
            if (a.resolutionNotes != null &&
                a.resolutionNotes!.trim().isNotEmpty) ...[
              const SizedBox(height: 8),
              SgCard(
                child: Text('Prior notes: ${a.resolutionNotes}'),
              ),
            ],
            const SizedBox(height: 16),
            const Text(
              'Dispatcher resolution',
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _notes,
              maxLines: 3,
              decoration: const InputDecoration(
                hintText: 'Resolution notes…',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                if ((a.canAcknowledge || isDemo) && !a.isAcknowledged)
                  FilledButton(
                    onPressed: () async {
                      await ref
                          .read(alertsProvider.notifier)
                          .acknowledge(a.id);
                      if (context.mounted) Navigator.pop(context);
                    },
                    child: const Text('Acknowledge'),
                  ),
                if (a.canResolve || isDemo)
                  FilledButton.tonal(
                    onPressed: () async {
                      await ref.read(alertsProvider.notifier).resolve(
                            a.id,
                            notes: _notes.text.trim().isEmpty
                                ? null
                                : _notes.text.trim(),
                          );
                      if (context.mounted) Navigator.pop(context);
                    },
                    child: const Text('Resolve'),
                  ),
                if (a.canArchive || isDemo)
                  OutlinedButton(
                    onPressed: () async {
                      await ref
                          .read(alertsProvider.notifier)
                          .archive(a.id, reason: _notes.text.trim());
                      if (context.mounted) Navigator.pop(context);
                    },
                    child: const Text('Archive'),
                  ),
              ],
            ),
          ],
        );
      },
    );
  }
}

class _MetricTile extends StatelessWidget {
  const _MetricTile({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(AppRadii.md),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            label,
            style: const TextStyle(fontSize: 11, color: AppColors.textMuted),
          ),
          const SizedBox(height: 4),
          Text(
            value,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 13),
          ),
        ],
      ),
    );
  }
}

/// Deep-link full-screen route wrapper around the same detail content.
class AlertDetailScreen extends ConsumerWidget {
  const AlertDetailScreen({super.key, required this.alertId});
  final int alertId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(title: const Text('Alert')),
      body: IncidentDetailSheet(alertId: alertId),
    );
  }
}
