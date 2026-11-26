import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import 'fleet_hub_notifier.dart';
import '../../../shared/widgets/sg_brand_loader.dart';
import '../../../core/time/tenant_time.dart';

/// Opens the premium full-screen GPS Commands UI for [vehicleId].
/// Kept name for call-site compatibility (detail / map / hub).
Future<void> showVehicleCommandsSheet(
  BuildContext context,
  int vehicleId,
) {
  return Navigator.of(context).push(
    MaterialPageRoute<void>(
      builder: (_) => VehicleCommandsScreen(vehicleId: vehicleId),
    ),
  );
}

enum _CommandCategory { all, engine, device, gps, network }

class _CommandMeta {
  const _CommandMeta({
    required this.type,
    required this.title,
    required this.description,
    required this.category,
    required this.icon,
    required this.iconBg,
    required this.iconColor,
    this.confirmDanger = false,
  });

  final String type;
  final String title;
  final String description;
  final _CommandCategory category;
  final IconData icon;
  final Color iconBg;
  final Color iconColor;
  final bool confirmDanger;
}

const _commandCatalog = <_CommandMeta>[
  _CommandMeta(
    type: 'engineStop',
    title: 'Engine Stop',
    description: 'Stop vehicle engine remotely.',
    category: _CommandCategory.engine,
    icon: Icons.power_settings_new,
    iconBg: Color(0xFFFEE2E2),
    iconColor: Color(0xFFDC2626),
    confirmDanger: true,
  ),
  _CommandMeta(
    type: 'engineResume',
    title: 'Engine Resume',
    description: 'Resume vehicle engine remotely.',
    category: _CommandCategory.engine,
    icon: Icons.power,
    iconBg: Color(0xFFDCFCE7),
    iconColor: Color(0xFF16A34A),
  ),
  _CommandMeta(
    type: 'restart',
    title: 'Restart Device',
    description: 'Restart the GPS tracker.',
    category: _CommandCategory.device,
    icon: Icons.restart_alt,
    iconBg: Color(0xFFFFEDD5),
    iconColor: Color(0xFFEA580C),
    confirmDanger: true,
  ),
  _CommandMeta(
    type: 'status',
    title: 'Device Status',
    description: 'Battery, GPS, ACC, network snapshot.',
    category: _CommandCategory.device,
    icon: Icons.monitor_heart_outlined,
    iconBg: Color(0xFFCCFBF1),
    iconColor: Color(0xFF0D9488),
  ),
  _CommandMeta(
    type: 'check',
    title: 'Device Check',
    description: 'Full device diagnostic snapshot.',
    category: _CommandCategory.device,
    icon: Icons.fact_check_outlined,
    iconBg: Color(0xFFE0E7FF),
    iconColor: Color(0xFF4F46E5),
  ),
  _CommandMeta(
    type: 'iccid',
    title: 'ICCID',
    description: 'Query SIM ICCID from the tracker.',
    category: _CommandCategory.device,
    icon: Icons.sim_card_outlined,
    iconBg: Color(0xFFF1F5F9),
    iconColor: Color(0xFF475569),
  ),
  _CommandMeta(
    type: 'imsi',
    title: 'IMSI',
    description: 'Query SIM IMSI from the tracker.',
    category: _CommandCategory.device,
    icon: Icons.pin_outlined,
    iconBg: Color(0xFFF1F5F9),
    iconColor: Color(0xFF475569),
  ),
  _CommandMeta(
    type: 'positionSingle',
    title: 'URL / Location',
    description: 'Request live map link or position.',
    category: _CommandCategory.gps,
    icon: Icons.location_on_outlined,
    iconBg: Color(0xFFDBEAFE),
    iconColor: Color(0xFF2563EB),
  ),
  _CommandMeta(
    type: 'gprsset',
    title: 'GPRSSET',
    description: 'Network settings snapshot from device.',
    category: _CommandCategory.gps,
    icon: Icons.cell_tower,
    iconBg: Color(0xFFE0F2FE),
    iconColor: Color(0xFF0284C7),
  ),
  _CommandMeta(
    type: 'server',
    title: 'Network Settings',
    description: 'APN / server via GPRSSET# query.',
    category: _CommandCategory.network,
    icon: Icons.dns_outlined,
    iconBg: Color(0xFFF3E8FF),
    iconColor: Color(0xFF7C3AED),
  ),
  _CommandMeta(
    type: 'apn',
    title: 'Work Settings',
    description: 'Query current APN / work profile.',
    category: _CommandCategory.network,
    icon: Icons.settings_ethernet,
    iconBg: Color(0xFFECFDF5),
    iconColor: Color(0xFF059669),
  ),
];

class VehicleCommandsScreen extends ConsumerStatefulWidget {
  const VehicleCommandsScreen({super.key, required this.vehicleId});

  final int vehicleId;

  @override
  ConsumerState<VehicleCommandsScreen> createState() =>
      _VehicleCommandsScreenState();
}

class _VehicleCommandsScreenState extends ConsumerState<VehicleCommandsScreen> {
  static const _preferredTypes = {
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
  _CommandCategory _category = _CommandCategory.all;
  bool _showAllRecent = false;

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
        _recent = recent;
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

  Future<void> _send(
    _CommandMeta meta,
    String vehicleLabel,
  ) async {
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
    if ((meta.type == 'engineStop' || meta.type == 'engineResume') &&
        !session.hasPermission(FleetPermissions.gpsCommandEngineCutoff)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Missing Gps.CommandEngineCutoff permission'),
        ),
      );
      return;
    }

    if (meta.confirmDanger) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text('Send ${meta.title}?'),
          content: Text(
            'Vehicle: $vehicleLabel\n\n'
            '${meta.type == 'engineStop' ? 'This queues an engine cut-off. Confirm the vehicle is safely stopped.' : 'This will restart the GPS tracker.'}\n\n'
            'It may require approval before the device receives it.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              style: FilledButton.styleFrom(backgroundColor: AppColors.error),
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Send'),
            ),
          ],
        ),
      );
      if (confirmed != true) return;
    }

    setState(() => _sending = true);
    try {
      final result = await ref.read(fleetApiProvider).sendDeviceCommand(
            gpsDeviceId: deviceId,
            commandType: meta.type,
            reason: 'Fleet app · ${meta.title}',
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(_toastForSend(meta.title, result.message))),
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

  _CommandCategory _guessCategory(String type) {
    final t = type.toLowerCase();
    if (t.contains('engine') || t.contains('stop') || t.contains('resume')) {
      return _CommandCategory.engine;
    }
    if (t.contains('gprs') ||
        t.contains('position') ||
        t.contains('location') ||
        t.contains('url') ||
        t.contains('gps')) {
      return _CommandCategory.gps;
    }
    if (t.contains('apn') ||
        t.contains('server') ||
        t.contains('network') ||
        t.contains('work')) {
      return _CommandCategory.network;
    }
    return _CommandCategory.device;
  }

  List<_CommandMeta> _visibleCommands() {
    final catalogByType = {for (final m in _commandCatalog) m.type: m};
    final useApi = _supported.isNotEmpty;

    late final List<_CommandMeta> base;
    if (!useApi) {
      // Offline / empty catalog fallback — show known preferred actions.
      base = _commandCatalog
          .where((m) => _preferredTypes.contains(m.type))
          .toList();
    } else {
      final seen = <String>{};
      final list = <_CommandMeta>[];
      // Prefer catalog order for known types.
      for (final m in _commandCatalog) {
        final s = _supported.cast<SupportedGpsCommand?>().firstWhere(
              (c) => c?.type == m.type,
              orElse: () => null,
            );
        if (s == null || !s.available) continue;
        list.add(m);
        seen.add(m.type);
      }
      // Every other backend-supported command.
      for (final s in _supported) {
        if (!s.available || seen.contains(s.type)) continue;
        final known = catalogByType[s.type];
        list.add(
          known ??
              _CommandMeta(
                type: s.type,
                title: s.label.isNotEmpty ? s.label : s.type,
                description: s.reason ?? 'Device command',
                category: _guessCategory(s.type),
                icon: Icons.terminal,
                iconBg: const Color(0xFFF1F5F9),
                iconColor: const Color(0xFF475569),
              ),
        );
        seen.add(s.type);
      }
      base = list;
    }

    if (_category == _CommandCategory.all) return base;
    return base.where((m) => m.category == _category).toList();
  }

  Map<_CommandCategory, List<_CommandMeta>> _grouped(List<_CommandMeta> list) {
    final map = <_CommandCategory, List<_CommandMeta>>{
      _CommandCategory.engine: [],
      _CommandCategory.device: [],
      _CommandCategory.gps: [],
      _CommandCategory.network: [],
    };
    for (final m in list) {
      map[m.category]!.add(m);
    }
    return map;
  }

  String _groupTitle(_CommandCategory c) => switch (c) {
        _CommandCategory.engine => 'Engine Control',
        _CommandCategory.device => 'Device Management',
        _CommandCategory.gps => 'GPS & Location',
        _CommandCategory.network => 'Network & Configuration',
        _CommandCategory.all => 'All Commands',
      };

  @override
  Widget build(BuildContext context) {
    final session = ref.watch(fleetSessionProvider);
    final isSuperAdmin = session?.canUseGpsCommands ?? false;
    final hub = ref.watch(fleetHubProvider).valueOrNull;
    final vehicle = hub?.locations
        .cast<FleetVehicleLocation?>()
        .firstWhere(
          (v) => v?.vehicleId == widget.vehicleId,
          orElse: () => null,
        );

    final plate = vehicle?.registrationNumber.trim().isNotEmpty == true
        ? vehicle!.registrationNumber
        : (vehicle?.vehicleName ?? 'Vehicle #${widget.vehicleId}');
    final modelLine = [
      if (vehicle?.vehicleName != null &&
          vehicle!.vehicleName != plate)
        vehicle.vehicleName,
      if (vehicle?.vehicleType != null && vehicle!.vehicleType!.isNotEmpty)
        vehicle.vehicleType,
    ].join(' • ');
    final online = vehicle?.status == FleetTrackStatus.moving ||
        vehicle?.status == FleetTrackStatus.idle ||
        vehicle?.status == FleetTrackStatus.parked ||
        vehicle?.status == FleetTrackStatus.sos ||
        vehicle?.status == FleetTrackStatus.unknown;
    final gsm = vehicle?.gsmSignal;

    if (!isSuperAdmin) {
      return Scaffold(
        backgroundColor: const Color(0xFFF2F7F6),
        appBar: AppBar(
          title: const Text('GPS Commands'),
          backgroundColor: AppColors.primary,
          foregroundColor: Colors.white,
        ),
        body: const Center(
          child: Padding(
            padding: EdgeInsets.all(24),
            child: Text(
              'Device commands are restricted to Super Admin.',
              textAlign: TextAlign.center,
            ),
          ),
        ),
      );
    }

    final canSend = session!.hasPermission(FleetPermissions.gpsCommandSend);
    final canApprove =
        session.hasPermission(FleetPermissions.gpsCommandApprove);
    final canCancel = session.hasPermission(FleetPermissions.gpsCommandCancel);
    final actions = _visibleCommands();
    final grouped = _grouped(actions);
    final width = MediaQuery.sizeOf(context).width;
    final columns = width >= 720 ? 3 : (width >= 380 ? 2 : 1);

    return Scaffold(
      backgroundColor: const Color(0xFFF2F7F6),
      body: Column(
        children: [
          _PremiumHeader(
            onBack: () => Navigator.pop(context),
            onRefresh: _sending ? null : _load,
          ),
          Expanded(
            child: _loading
                ? const Center(child: SgBrandLoader())
                : _error != null
                    ? Center(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: Text(_error!, textAlign: TextAlign.center),
                        ),
                      )
                    : RefreshIndicator(
                        color: AppColors.primary,
                        onRefresh: _load,
                        child: ListView(
                          padding: const EdgeInsets.fromLTRB(16, 12, 16, 100),
                          children: [
                            if (_sending) ...[
                              const LinearProgressIndicator(
                                color: AppColors.primary,
                                minHeight: 2,
                              ),
                              const SizedBox(height: 8),
                            ],
                            _VehicleInfoCard(
                              plate: plate,
                              subtitle: modelLine.isEmpty
                                  ? 'GPS-linked vehicle'
                                  : modelLine,
                              online: online,
                              gsmSignal: gsm,
                            ),
                            const SizedBox(height: 14),
                            _CategoryChips(
                              selected: _category,
                              onSelect: (c) => setState(() => _category = c),
                            ),
                            const SizedBox(height: 16),
                            if (actions.isEmpty)
                              const Padding(
                                padding: EdgeInsets.all(24),
                                child: Text(
                                  'No commands available for this device.',
                                  textAlign: TextAlign.center,
                                ),
                              )
                            else
                              for (final entry in grouped.entries)
                                if (entry.value.isNotEmpty) ...[
                                  _SectionLabel(title: _groupTitle(entry.key)),
                                  const SizedBox(height: 10),
                                  _CommandGrid(
                                    commands: entry.value,
                                    columns: columns,
                                    sending: _sending,
                                    canSend: canSend,
                                    onSend: (m) => _send(m, plate),
                                  ),
                                  const SizedBox(height: 18),
                                ],
                            if (_recent.isNotEmpty) ...[
                              Row(
                                children: [
                                  const Expanded(
                                    child: Text(
                                      'Recent Commands',
                                      style: TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w800,
                                        color: Color(0xFF172033),
                                      ),
                                    ),
                                  ),
                                  if (_recent.length > 5)
                                    TextButton(
                                      onPressed: () => setState(
                                        () => _showAllRecent = !_showAllRecent,
                                      ),
                                      child: Text(
                                        _showAllRecent ? 'Show Less' : 'View All',
                                        style: const TextStyle(
                                          color: AppColors.primary,
                                          fontWeight: FontWeight.w700,
                                        ),
                                      ),
                                    ),
                                ],
                              ),
                              const SizedBox(height: 6),
                              for (final r in (_showAllRecent
                                  ? _recent
                                  : _recent.take(5)))
                                _RecentCommandTile(
                                  item: r,
                                  expanded: _expandedCommandId == r.id,
                                  expandedLoading: _expandedLoading &&
                                      _expandedCommandId == r.id,
                                  expandedError: _expandedCommandId == r.id
                                      ? _expandedError
                                      : null,
                                  expandedDetail: _expandedCommandId == r.id
                                      ? _expandedDetail
                                      : null,
                                  canApprove: canApprove && r.isPendingApproval,
                                  canCancel: canCancel && r.isCancellable,
                                  sending: _sending,
                                  onTap: () => _toggleDetail(r),
                                  onApprove: () => _approve(r),
                                  onCancel: () => _cancel(r),
                                ),
                            ],
                          ],
                        ),
                      ),
          ),
        ],
      ),
    );
  }
}

class _PremiumHeader extends StatelessWidget {
  const _PremiumHeader({required this.onBack, this.onRefresh});

  final VoidCallback onBack;
  final VoidCallback? onRefresh;

  @override
  Widget build(BuildContext context) {
    final top = MediaQuery.paddingOf(context).top;
    return Container(
      width: double.infinity,
      padding: EdgeInsets.fromLTRB(8, top + 8, 8, 18),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [AppColors.primary, AppColors.primaryDark],
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              IconButton(
                onPressed: onBack,
                icon: const Icon(Icons.arrow_back, color: Colors.white),
              ),
              const Expanded(
                child: Text(
                  'GPS Commands',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              IconButton(
                onPressed: onRefresh,
                tooltip: 'Refresh',
                icon: const Icon(Icons.refresh, color: Colors.white),
              ),
              IconButton(
                onPressed: () {
                  showDialog<void>(
                    context: context,
                    builder: (ctx) => AlertDialog(
                      title: const Text('GPS Commands'),
                      content: const Text(
                        'Send engine, device, GPS, and network commands to the selected vehicle tracker. Dangerous actions require confirmation.',
                      ),
                      actions: [
                        TextButton(
                          onPressed: () => Navigator.pop(ctx),
                          child: const Text('Close'),
                        ),
                      ],
                    ),
                  );
                },
                tooltip: 'Info',
                icon: const Icon(Icons.info_outline, color: Colors.white),
              ),
            ],
          ),
          const Padding(
            padding: EdgeInsets.only(left: 16, right: 16),
            child: Text(
              'Manage vehicle GPS commands remotely.',
              style: TextStyle(
                color: Color(0xFFE0F2F1),
                fontSize: 13.5,
                height: 1.35,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _VehicleInfoCard extends StatelessWidget {
  const _VehicleInfoCard({
    required this.plate,
    required this.subtitle,
    required this.online,
    this.gsmSignal,
  });

  final String plate;
  final String subtitle;
  final bool online;
  final int? gsmSignal;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      elevation: 1.5,
      shadowColor: Colors.black26,
      borderRadius: BorderRadius.circular(18),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          children: [
            Container(
              width: 52,
              height: 52,
              decoration: BoxDecoration(
                color: const Color(0xFFCCFBF1),
                borderRadius: BorderRadius.circular(14),
              ),
              child: const Icon(
                Icons.local_shipping_outlined,
                color: AppColors.primaryDark,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    plate,
                    softWrap: true,
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w800,
                      color: Color(0xFF172033),
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    softWrap: true,
                    style: const TextStyle(
                      fontSize: 12.5,
                      color: Color(0xFF64748B),
                    ),
                  ),
                ],
              ),
            ),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: online
                        ? const Color(0xFFDCFCE7)
                        : const Color(0xFFF1F5F9),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(
                        Icons.circle,
                        size: 8,
                        color: online
                            ? const Color(0xFF16A34A)
                            : const Color(0xFF94A3B8),
                      ),
                      const SizedBox(width: 4),
                      Text(
                        online ? 'Online' : 'Offline',
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                          color: online
                              ? const Color(0xFF16A34A)
                              : const Color(0xFF64748B),
                        ),
                      ),
                    ],
                  ),
                ),
                if (gsmSignal != null) ...[
                  const SizedBox(height: 6),
                  Icon(
                    Icons.signal_cellular_alt,
                    size: 18,
                    color: online
                        ? const Color(0xFF16A34A)
                        : const Color(0xFF94A3B8),
                  ),
                ],
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _CategoryChips extends StatelessWidget {
  const _CategoryChips({required this.selected, required this.onSelect});

  final _CommandCategory selected;
  final ValueChanged<_CommandCategory> onSelect;

  @override
  Widget build(BuildContext context) {
    final items = <(_CommandCategory, String, IconData)>[
      (_CommandCategory.all, 'All Commands', Icons.grid_view_rounded),
      (_CommandCategory.engine, 'Engine', Icons.power_settings_new),
      (_CommandCategory.device, 'Device', Icons.smartphone_outlined),
      (_CommandCategory.gps, 'GPS', Icons.location_on_outlined),
      (_CommandCategory.network, 'Network', Icons.wifi_tethering),
    ];

    return SizedBox(
      height: 42,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: items.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (context, i) {
          final (cat, label, icon) = items[i];
          final active = selected == cat;
          return Material(
            color: active ? AppColors.primary : Colors.white,
            borderRadius: BorderRadius.circular(999),
            child: InkWell(
              onTap: () => onSelect(cat),
              borderRadius: BorderRadius.circular(999),
              child: Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(999),
                  border: Border.all(
                    color: active
                        ? AppColors.primary
                        : const Color(0xFFE2E8F0),
                  ),
                ),
                child: Row(
                  children: [
                    Icon(
                      icon,
                      size: 16,
                      color: active ? Colors.white : AppColors.primary,
                    ),
                    const SizedBox(width: 6),
                    Text(
                      label,
                      style: TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w700,
                        color: active ? Colors.white : const Color(0xFF334155),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel({required this.title});
  final String title;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 3,
          height: 16,
          decoration: BoxDecoration(
            color: AppColors.primary,
            borderRadius: BorderRadius.circular(2),
          ),
        ),
        const SizedBox(width: 8),
        Text(
          title,
          style: const TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w800,
            color: AppColors.primaryDark,
          ),
        ),
      ],
    );
  }
}

class _CommandGrid extends StatelessWidget {
  const _CommandGrid({
    required this.commands,
    required this.columns,
    required this.sending,
    required this.canSend,
    required this.onSend,
  });

  final List<_CommandMeta> commands;
  final int columns;
  final bool sending;
  final bool canSend;
  final ValueChanged<_CommandMeta> onSend;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        const gap = 10.0;
        final cardW =
            (constraints.maxWidth - gap * (columns - 1)) / columns;
        return Wrap(
          spacing: gap,
          runSpacing: gap,
          children: [
            for (final cmd in commands)
              SizedBox(
                width: cardW,
                child: _CommandCard(
                  meta: cmd,
                  enabled: canSend && !sending,
                  onSend: () => onSend(cmd),
                ),
              ),
          ],
        );
      },
    );
  }
}

class _CommandCard extends StatelessWidget {
  const _CommandCard({
    required this.meta,
    required this.enabled,
    required this.onSend,
  });

  final _CommandMeta meta;
  final bool enabled;
  final VoidCallback onSend;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(12, 12, 12, 12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: const Color(0xFFE2E8F0)),
        boxShadow: const [
          BoxShadow(
            color: Color(0x0F0F172A),
            blurRadius: 10,
            offset: Offset(0, 3),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Container(
                width: 36,
                height: 36,
                decoration: BoxDecoration(
                  color: meta.iconBg,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(meta.icon, size: 20, color: meta.iconColor),
              ),
              const Spacer(),
            ],
          ),
          const SizedBox(height: 10),
          Text(
            meta.title,
            softWrap: true,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w800,
              color: Color(0xFF172033),
            ),
          ),
          const SizedBox(height: 4),
          Align(
            alignment: Alignment.centerLeft,
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
              decoration: BoxDecoration(
                color: const Color(0xFFF1F5F9),
                borderRadius: BorderRadius.circular(6),
              ),
              child: Text(
                meta.type,
                style: const TextStyle(
                  fontSize: 10.5,
                  fontWeight: FontWeight.w600,
                  color: Color(0xFF64748B),
                  fontFamily: 'monospace',
                ),
              ),
            ),
          ),
          const SizedBox(height: 6),
          Text(
            meta.description,
            softWrap: true,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 11.5,
              height: 1.35,
              color: Color(0xFF64748B),
            ),
          ),
          const SizedBox(height: 12),
          SizedBox(
            height: 36,
            child: FilledButton.icon(
              onPressed: enabled ? onSend : null,
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                foregroundColor: Colors.white,
                disabledBackgroundColor: const Color(0xFFCBD5E1),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(10),
                ),
                padding: const EdgeInsets.symmetric(horizontal: 10),
              ),
              icon: const Icon(Icons.send_rounded, size: 16),
              label: const Text(
                'Send',
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _RecentCommandTile extends StatelessWidget {
  const _RecentCommandTile({
    required this.item,
    required this.expanded,
    required this.expandedLoading,
    required this.expandedError,
    required this.expandedDetail,
    required this.canApprove,
    required this.canCancel,
    required this.sending,
    required this.onTap,
    required this.onApprove,
    required this.onCancel,
  });

  final GpsDeviceCommandItem item;
  final bool expanded;
  final bool expandedLoading;
  final String? expandedError;
  final GpsDeviceCommandDetail? expandedDetail;
  final bool canApprove;
  final bool canCancel;
  final bool sending;
  final VoidCallback onTap;
  final VoidCallback onApprove;
  final VoidCallback onCancel;

  Color get _badgeBg {
    final s = item.status.toLowerCase();
    if (s.contains('ack') || s == 'sent') return const Color(0xFFDCFCE7);
    if (s.contains('fail') || s.contains('timeout') || s.contains('error')) {
      return const Color(0xFFFEE2E2);
    }
    if (s.contains('pending') || s.contains('approval')) {
      return const Color(0xFFFEF3C7);
    }
    if (s.contains('cancel')) return const Color(0xFFF1F5F9);
    return const Color(0xFFF1F5F9);
  }

  Color get _badgeFg {
    final s = item.status.toLowerCase();
    if (s.contains('ack') || s == 'sent') return const Color(0xFF16A34A);
    if (s.contains('fail') || s.contains('timeout') || s.contains('error')) {
      return const Color(0xFFDC2626);
    }
    if (s.contains('pending') || s.contains('approval')) {
      return const Color(0xFFD97706);
    }
    return const Color(0xFF64748B);
  }

  IconData get _badgeIcon {
    final s = item.status.toLowerCase();
    if (s.contains('ack') || s == 'sent') return Icons.check_circle_outline;
    if (s.contains('fail') || s.contains('timeout') || s.contains('error')) {
      return Icons.cancel_outlined;
    }
    if (s.contains('pending') || s.contains('approval')) {
      return Icons.hourglass_empty;
    }
    return Icons.info_outline;
  }

  String _formatWhen(DateTime dt) {
    final local = TenantTime.inZone(dt);
    final now = DateTime.now();
    final sameDay = local.year == now.year &&
        local.month == now.month &&
        local.day == now.day;
    final hh = local.hour.toString().padLeft(2, '0');
    final mm = local.minute.toString().padLeft(2, '0');
    if (sameDay) return 'Today, $hh:$mm';
    return '${local.day.toString().padLeft(2, '0')}/'
        '${local.month.toString().padLeft(2, '0')} $hh:$mm';
  }

  IconData _cmdIcon(String type) {
    final t = type.toLowerCase();
    if (t.contains('stop')) return Icons.power_settings_new;
    if (t.contains('resume')) return Icons.power;
    if (t.contains('restart')) return Icons.restart_alt;
    if (t.contains('position') || t.contains('url')) {
      return Icons.location_on_outlined;
    }
    return Icons.terminal;
  }

  @override
  Widget build(BuildContext context) {
    final statusLabel = item.errorMessage == null || item.errorMessage!.isEmpty
        ? item.status
        : '${item.status} · ${item.errorMessage}';

    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Column(
        children: [
          InkWell(
            onTap: onTap,
            borderRadius: BorderRadius.circular(14),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 12, 8, 12),
              child: Row(
                children: [
                  Container(
                    width: 36,
                    height: 36,
                    decoration: BoxDecoration(
                      color: const Color(0xFFCCFBF1),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Icon(
                      _cmdIcon(item.commandType),
                      size: 18,
                      color: AppColors.primaryDark,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          item.commandType,
                          softWrap: true,
                          style: const TextStyle(
                            fontSize: 13.5,
                            fontWeight: FontWeight.w800,
                            color: Color(0xFF172033),
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          _formatWhen(item.requestedAt),
                          style: const TextStyle(
                            fontSize: 11.5,
                            color: Color(0xFF64748B),
                          ),
                        ),
                      ],
                    ),
                  ),
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: _badgeBg,
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(_badgeIcon, size: 12, color: _badgeFg),
                        const SizedBox(width: 4),
                        ConstrainedBox(
                          constraints: const BoxConstraints(maxWidth: 110),
                          child: Text(
                            statusLabel,
                            softWrap: true,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              fontSize: 10.5,
                              fontWeight: FontWeight.w700,
                              color: _badgeFg,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  Icon(
                    expanded ? Icons.expand_less : Icons.expand_more,
                    color: Colors.black45,
                  ),
                ],
              ),
            ),
          ),
          if (canApprove || canCancel)
            Padding(
              padding: const EdgeInsets.only(left: 8, right: 8, bottom: 4),
              child: Row(
                children: [
                  if (canApprove)
                    TextButton(
                      onPressed: sending ? null : onApprove,
                      child: const Text('Approve'),
                    ),
                  if (canCancel)
                    TextButton(
                      onPressed: sending ? null : onCancel,
                      child: const Text('Cancel'),
                    ),
                ],
              ),
            ),
          if (expanded)
            Padding(
              padding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
              child: _CommandResponsePanel(
                loading: expandedLoading,
                error: expandedError,
                detail: expandedDetail,
              ),
            ),
        ],
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
        child: LinearProgressIndicator(color: AppColors.primary),
      );
    }
    if (error != null) {
      return Text(
        error!,
        style: TextStyle(color: Theme.of(context).colorScheme.error),
      );
    }
    final d = detail;
    if (d == null) {
      return const Text(
        'No detail available.',
        style: TextStyle(color: Color(0xFF64748B)),
      );
    }

    final cmd = d.command;
    final body = d.responseBodyText.trim();
    final lines = <String>[
      'Status: ${cmd.status}',
      'Requested: ${TenantTime.format(cmd.requestedAt, DateFormat('dd MMM yyyy, HH:mm:ss'))}',
      if (cmd.reason != null && cmd.reason!.trim().isNotEmpty)
        'Reason: ${cmd.reason}',
      if (cmd.errorMessage != null && cmd.errorMessage!.trim().isNotEmpty)
        'Failure: ${cmd.errorMessage}',
      if (d.attributesJson != null && d.attributesJson!.trim().isNotEmpty)
        'Payload: ${d.attributesJson}',
      if (body.isNotEmpty) 'Device response:\n$body',
      if (body.isEmpty)
        'Device response: (none yet — STATUS/CHECK replies appear when the tracker answers)',
    ];

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: const Color(0xFF0F172A),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Text(
        lines.join('\n'),
        style: const TextStyle(
          color: Color(0xFFE2E8F0),
          fontFamily: 'monospace',
          fontSize: 12,
          height: 1.45,
        ),
      ),
    );
  }
}
