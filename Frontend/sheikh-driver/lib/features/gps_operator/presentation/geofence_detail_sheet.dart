import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../alerts/domain/gps_alert_models.dart';
import '../../fleet/domain/fleet_models.dart';
import '../data/geofence_api.dart';
import '../domain/geofence_models.dart';

Future<void> showGeofenceDetailSheet(
  BuildContext context, {
  required GpsGeofenceItem zone,
  required bool canManage,
  required VoidCallback onEdit,
}) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    backgroundColor: context.sg.card,
    builder: (_) => DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.6,
      maxChildSize: 0.95,
      builder: (ctx, scroll) => _GeofenceDetail(
        zone: zone,
        canManage: canManage,
        onEdit: () {
          Navigator.of(ctx).pop();
          onEdit();
        },
        scroll: scroll,
      ),
    ),
  );
}

class _GeofenceDetail extends ConsumerStatefulWidget {
  const _GeofenceDetail({
    required this.zone,
    required this.canManage,
    required this.onEdit,
    required this.scroll,
  });

  final GpsGeofenceItem zone;
  final bool canManage;
  final VoidCallback onEdit;
  final ScrollController scroll;

  @override
  ConsumerState<_GeofenceDetail> createState() => _GeofenceDetailState();
}

class _GeofenceDetailState extends ConsumerState<_GeofenceDetail> {
  List<GeofenceAssignment>? _assignments;
  List<GeofenceVehicleState>? _states;
  List<GpsAlertEvent>? _events;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final api = ref.read(geofenceApiProvider);
    final id = widget.zone.id;
    try {
      final results = await Future.wait([
        api.assignments(id),
        api.states(geofenceId: id),
        api.events(id),
      ]);
      if (!mounted) return;
      setState(() {
        _assignments = results[0] as List<GeofenceAssignment>;
        _states = results[1] as List<GeofenceVehicleState>;
        _events = (results[2] as List<GpsAlertEvent>).take(20).toList();
      });
    } catch (e) {
      if (mounted) setState(() => _error = ErrorHandler.message(e));
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final z = widget.zone;
    final shape = z.isCircle
        ? 'Circle · ${z.radiusMeters.toStringAsFixed(0)} m radius'
        : (z.areaType.toLowerCase().contains('rect') ? 'Rectangle' : 'Polygon');

    return ListView(
      controller: widget.scroll,
      padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
      children: [
        Center(
          child: Container(
            width: 40,
            height: 4,
            decoration: BoxDecoration(
              color: p.divider,
              borderRadius: BorderRadius.circular(2),
            ),
          ),
        ),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(
              child: Text(
                z.name,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                  color: p.textPrimary,
                ),
              ),
            ),
            StatusBadge(
              z.isActive ? 'Active' : 'Inactive',
              color: z.isActive ? AppColors.success : p.textMuted,
            ),
            if (widget.canManage)
              IconButton(
                tooltip: 'Edit zone',
                onPressed: widget.onEdit,
                icon: const Icon(Icons.edit_outlined),
              ),
          ],
        ),
        const SizedBox(height: 4),
        Text(
          [shape, if ((z.category ?? '').isNotEmpty) z.category!].join(' · '),
          style: TextStyle(color: p.textSecondary),
        ),
        const SizedBox(height: 4),
        Text(
          z.alertOnEnter || z.alertOnExit
              ? 'Alerts: ${[
                  if (z.alertOnEnter) 'enter',
                  if (z.alertOnExit) 'exit',
                ].join(' & ')}'
              : 'Alerts off — edit the zone to enable enter/exit alerts.',
          style: TextStyle(
            color: z.alertOnEnter || z.alertOnExit
                ? p.textSecondary
                : AppColors.warning,
          ),
        ),
        if ((z.description ?? '').isNotEmpty) ...[
          const SizedBox(height: 8),
          Text(z.description!, style: TextStyle(color: p.textSecondary)),
        ],
        if (_error != null) ...[
          const SizedBox(height: 16),
          SgErrorView(message: _error!, onRetry: _load),
        ] else ...[
          const SizedBox(height: 16),
          const SgSectionTitle('Vehicles in this zone'),
          ..._section(
            _states,
            empty: 'No vehicle state yet — appears after the next position.',
            build: (s) => ListTile(
              dense: true,
              contentPadding: EdgeInsets.zero,
              title: Text(
                s.vehicleLabel,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              subtitle: Text(
                '${s.isInside ? 'Inside' : 'Outside'} since '
                '${formatFreshness(s.sinceUtc)} · position '
                '${formatFreshness(s.lastPositionUtc)}',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(color: p.textSecondary),
              ),
              trailing: StatusBadge(
                s.isInside ? 'Inside' : 'Outside',
                color: s.isInside ? AppColors.success : p.textMuted,
              ),
            ),
          ),
          const SizedBox(height: 12),
          const SgSectionTitle('Assigned'),
          ..._section(
            _assignments,
            empty: 'Not assigned to any vehicle, branch, or department.',
            build: (a) => ListTile(
              dense: true,
              contentPadding: EdgeInsets.zero,
              leading: Icon(
                a.vehicleId != null
                    ? Icons.directions_car_outlined
                    : Icons.apartment_outlined,
                color: p.textSecondary,
              ),
              title: Text(a.label, maxLines: 1, overflow: TextOverflow.ellipsis),
            ),
          ),
          const SizedBox(height: 12),
          const SgSectionTitle('Recent events (7 days)'),
          ..._section(
            _events,
            empty: 'No enter/exit events in the last 7 days.',
            build: (e) => ListTile(
              dense: true,
              contentPadding: EdgeInsets.zero,
              leading: Icon(
                e.eventType.contains('enter')
                    ? Icons.login_rounded
                    : Icons.logout_rounded,
                color: AppColors.primary,
              ),
              title: Text(
                e.registrationNumber ?? e.vehicleName ?? 'Vehicle #${e.vehicleId}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              subtitle: Text(
                '${e.eventType.contains('enter') ? 'Entered' : 'Exited'} · '
                '${formatFreshness(e.timestamp)}',
                style: TextStyle(color: p.textSecondary),
              ),
            ),
          ),
        ],
      ],
    );
  }

  List<Widget> _section<T>(
    List<T>? items, {
    required String empty,
    required Widget Function(T) build,
  }) {
    if (items == null) {
      return const [
        Padding(
          padding: EdgeInsets.symmetric(vertical: 12),
          child: Center(child: SgBrandLoader()),
        ),
      ];
    }
    if (items.isEmpty) {
      return [
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Text(empty, style: TextStyle(color: context.sg.textMuted)),
        ),
      ];
    }
    return items.map(build).toList();
  }
}
