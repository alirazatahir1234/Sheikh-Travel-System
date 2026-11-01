import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/errors/error_handler.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import '../../../shared/widgets/sg_brand_loader.dart';

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
    'status',
    'check',
    'iccid',
    'imsi',
    'apn',
    'gprsset',
    'server',
  };

  bool _loading = true;
  bool _sending = false;
  String? _error;
  int? _deviceId;
  List<SupportedGpsCommand> _supported = const [];
  List<GpsDeviceCommandItem> _recent = const [];
  int? _expandedCommandId;
  GpsDeviceCommandDetail? _expandedDetail;
  bool _expandedLoading = false;
  String? _expandedError;

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
        _recent = recent.take(12).toList();
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = ErrorHandler.message(e);
      });
    }
  }

  String _toastForSend(String label, String apiMessage) {
    final lower = apiMessage.toLowerCase();
    if (lower.contains('approval') || lower.contains('awaiting')) {
      return '$label queued for approval';
    }
    if (lower.contains('sent to device')) {
      return '$label sent to device';
    }
    if (lower.contains('not configured')) {
      return '$label logged — delivery not configured';
    }
    if (apiMessage.trim().isNotEmpty) return apiMessage;
    return '$label submitted';
  }

  Future<void> _send(String type, String label) async {
    final deviceId = _deviceId;
    if (deviceId == null) return;
    final session = ref.read(fleetSessionProvider);
    if (session == null || !session.canUseGpsCommands) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Device commands require Super Admin')),
      );
      return;
    }
    if (!session.hasPermission(FleetPermissions.gpsCommandSend)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Missing Gps.CommandSend permission')),
      );
      return;
    }
    if ((type == 'engineStop' || type == 'engineResume') &&
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
          type == 'engineStop'
              ? 'This queues an engine cut-off. It may require approval before the device receives it.'
              : 'This sends a $type command to the vehicle tracker.',
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
      final result = await ref.read(fleetApiProvider).sendDeviceCommand(
            gpsDeviceId: deviceId,
            commandType: type,
            reason: 'Fleet app · $label',
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(_toastForSend(label, result.message))),
      );
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(ErrorHandler.message(e))),
      );
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  Future<void> _approve(GpsDeviceCommandItem cmd) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Approve ${cmd.commandType}?'),
        content: const Text(
          'This approves the command and sends it to the device via Traccar.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Approve'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _sending = true);
    try {
      await ref.read(fleetApiProvider).approveDeviceCommand(
            commandId: cmd.id,
            approve: true,
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Approved — dispatching to device')),
      );
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(ErrorHandler.message(e))),
      );
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  Future<void> _cancel(GpsDeviceCommandItem cmd) async {
    setState(() => _sending = true);
    try {
      await ref.read(fleetApiProvider).cancelDeviceCommand(cmd.id);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Command cancelled')),
      );
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(ErrorHandler.message(e))),
      );
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  Future<void> _toggleDetail(GpsDeviceCommandItem cmd) async {
    if (_expandedCommandId == cmd.id) {
      setState(() {
        _expandedCommandId = null;
        _expandedDetail = null;
        _expandedError = null;
        _expandedLoading = false;
      });
      return;
    }

    setState(() {
      _expandedCommandId = cmd.id;
      _expandedDetail = null;
      _expandedError = null;
      _expandedLoading = true;
    });

    try {
      final detail = await ref.read(fleetApiProvider).getCommandById(cmd.id);
      if (!mounted || _expandedCommandId != cmd.id) return;
      setState(() {
        _expandedDetail = detail;
        _expandedLoading = false;
      });
    } catch (e) {
      if (!mounted || _expandedCommandId != cmd.id) return;
      setState(() {
        _expandedError = ErrorHandler.message(e);
        _expandedLoading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(fleetSessionProvider);
    final isSuperAdmin = session?.canUseGpsCommands ?? false;
    if (!isSuperAdmin) {
      return SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 8, 24, 32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                'Device commands are restricted to Super Admin.',
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('Close'),
              ),
            ],
          ),
        ),
      );
    }
    final canSend = session!.hasPermission(FleetPermissions.gpsCommandSend);
    final canApprove =
        session.hasPermission(FleetPermissions.gpsCommandApprove);
    final canCancel = session.hasPermission(FleetPermissions.gpsCommandCancel);

    // Prefer API-supported + available commands only (model templates + capabilities).
    final preferred = _supported
        .where((c) => _preferred.contains(c.type) && c.available)
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
              'status' => 'STATUS',
              'check' => 'CHECK',
              'iccid' => 'ICCID',
              'imsi' => 'IMSI',
              'apn' => 'APN',
              'gprsset' => 'GPRSSET',
              'server' => 'Network settings',
              _ => t,
            },
            available: true,
          ),
        )
        .toList();
    // If the API returned a list, never invent unsupported cards (e.g. buzzer).
    final actions = _supported.isNotEmpty ? preferred : fallback;

    // isScrollControlled sheets get unbounded max height; without a cap + scroll
    // the Column overflows (yellow/black stripes) once many command rows and/or
    // STATUS detail expand panels are present.
    final maxHeight = MediaQuery.sizeOf(context).height * 0.85;
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          left: 20,
          right: 20,
          bottom: bottomInset + 20,
        ),
        child: ConstrainedBox(
          constraints: BoxConstraints(maxHeight: maxHeight),
          child: SingleChildScrollView(
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
                    child: Center(child: SgBrandLoader()),
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
                    for (final r in _recent) ...[
                      ListTile(
                        dense: true,
                        contentPadding: EdgeInsets.zero,
                        onTap: () => _toggleDetail(r),
                        title: Text(r.commandType),
                        subtitle: Text(
                          r.errorMessage == null || r.errorMessage!.isEmpty
                              ? r.status
                              : '${r.status} · ${r.errorMessage}',
                        ),
                        trailing: Icon(
                          _expandedCommandId == r.id
                              ? Icons.expand_less
                              : Icons.expand_more,
                          size: 20,
                          color: Colors.black54,
                        ),
                      ),
                      if ((canApprove && r.isPendingApproval) ||
                          (canCancel && r.isCancellable))
                        Padding(
                          padding: const EdgeInsets.only(bottom: 4),
                          child: Wrap(
                            spacing: 4,
                            children: [
                              if (canApprove && r.isPendingApproval)
                                TextButton(
                                  onPressed:
                                      _sending ? null : () => _approve(r),
                                  child: const Text('Approve'),
                                ),
                              if (canCancel && r.isCancellable)
                                TextButton(
                                  onPressed: _sending ? null : () => _cancel(r),
                                  child: const Text('Cancel'),
                                ),
                            ],
                          ),
                        ),
                      if (_expandedCommandId == r.id)
                        Padding(
                          padding: const EdgeInsets.only(bottom: 12),
                          child: _CommandResponsePanel(
                            loading: _expandedLoading,
                            error: _expandedError,
                            detail: _expandedDetail,
                          ),
                        ),
                    ],
                  ],
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _CommandResponsePanel extends StatelessWidget {
  const _CommandResponsePanel({
    required this.loading,
    required this.error,
    required this.detail,
  });

  final bool loading;
  final String? error;
  final GpsDeviceCommandDetail? detail;

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 8),
        child: LinearProgressIndicator(),
      );
    }
    if (error != null) {
      return Text(
        error!,
        style: TextStyle(color: Theme.of(context).colorScheme.error),
      );
    }
    final body = detail?.responseBodyText.trim() ?? '';
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: const Color(0xFF0F172A),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Text(
        body.isEmpty
            ? 'No response text yet. STATUS/CHECK replies appear here when the tracker answers.'
            : body,
        style: TextStyle(
          color:
              body.isEmpty ? const Color(0xFF94A3B8) : const Color(0xFFE2E8F0),
          fontFamily: 'monospace',
          fontSize: 12.5,
          height: 1.45,
        ),
      ),
    );
  }
}
