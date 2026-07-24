import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';

Future<void> showVehicleCommandsSheet(
  BuildContext context,
  int vehicleId,
) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (_) => _VehicleCommandsSheet(vehicleId: vehicleId),
  );
}

class _VehicleCommandsSheet extends ConsumerStatefulWidget {
  const _VehicleCommandsSheet({required this.vehicleId});
  final int vehicleId;

  @override
  ConsumerState<_VehicleCommandsSheet> createState() =>
      _VehicleCommandsSheetState();
}

class _VehicleCommandsSheetState extends ConsumerState<_VehicleCommandsSheet> {
  static const _preferred = {
    'engineStop',
    'engineResume',
    'positionSingle',
    'restart',
  };

  bool _loading = true;
  bool _sending = false;
  String? _error;
  int? _deviceId;
  List<SupportedGpsCommand> _supported = const [];
  List<GpsDeviceCommandItem> _recent = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final api = ref.read(fleetApiProvider);
      final gps = await api.getVehicleGps(widget.vehicleId);
      final deviceId = gps.gpsDeviceId;
      if (deviceId == null) {
        setState(() {
          _loading = false;
          _error = 'No GPS device assigned to this vehicle.';
        });
        return;
      }

      List<SupportedGpsCommand> supported = const [];
      List<GpsDeviceCommandItem> recent = const [];
      try {
        supported = await api.getSupportedCommands(deviceId);
      } catch (_) {}
      try {
        recent = await api.getVehicleCommands(widget.vehicleId);
      } catch (_) {}

      if (!mounted) return;
      setState(() {
        _deviceId = deviceId;
        _supported = supported;
        _recent = recent.take(8).toList();
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _send(String type, String label) async {
    final deviceId = _deviceId;
    if (deviceId == null) return;
    final session = ref.read(fleetSessionProvider);
    if (session == null ||
        !session.hasPermission(FleetPermissions.gpsCommandSend)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Missing Gps.CommandSend permission')),
      );
      return;
    }
    if (type == 'engineStop' &&
        !session.hasPermission(FleetPermissions.gpsCommandEngineCutoff)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Missing Gps.CommandEngineCutoff permission'),
        ),
      );
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Send $label?'),
        content: Text(
          'This sends a $type command to the vehicle tracker.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Send'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _sending = true);
    try {
      await ref.read(fleetApiProvider).sendDeviceCommand(
            gpsDeviceId: deviceId,
            commandType: type,
            reason: 'Fleet app · $label',
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('$label queued')),
      );
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('$e')),
      );
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(fleetSessionProvider);
    final canSend =
        session?.hasPermission(FleetPermissions.gpsCommandSend) ?? false;

    final preferred = _supported
        .where((c) => _preferred.contains(c.type))
        .toList();
    final fallback = _preferred
        .map(
          (t) => SupportedGpsCommand(
            type: t,
            label: switch (t) {
              'engineStop' => 'Engine stop',
              'engineResume' => 'Engine resume',
              'positionSingle' => 'Request position',
              'restart' => 'Restart device',
              _ => t,
            },
            available: true,
          ),
        )
        .toList();
    final actions = preferred.isNotEmpty ? preferred : fallback;

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          left: 20,
          right: 20,
          bottom: MediaQuery.viewInsetsOf(context).bottom + 20,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'GPS commands',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 12),
            if (_loading)
              const Padding(
                padding: EdgeInsets.all(24),
                child: Center(child: CircularProgressIndicator()),
              )
            else if (_error != null)
              Padding(
                padding: const EdgeInsets.all(12),
                child: Text(_error!, textAlign: TextAlign.center),
              )
            else ...[
              if (_sending) const LinearProgressIndicator(),
              for (final cmd in actions)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(cmd.label),
                  subtitle: cmd.reason != null && !cmd.available
                      ? Text(cmd.reason!)
                      : Text(cmd.type),
                  trailing: FilledButton(
                    onPressed: !canSend || _sending || !cmd.available
                        ? null
                        : () => _send(cmd.type, cmd.label),
                    child: const Text('Send'),
                  ),
                ),
              if (_recent.isNotEmpty) ...[
                const SizedBox(height: 8),
                const Text(
                  'Recent',
                  style: TextStyle(fontWeight: FontWeight.w700),
                ),
                for (final r in _recent)
                  ListTile(
                    dense: true,
                    contentPadding: EdgeInsets.zero,
                    title: Text(r.commandType),
                    subtitle: Text(r.status),
                  ),
              ],
            ],
          ],
        ),
      ),
    );
  }
}
