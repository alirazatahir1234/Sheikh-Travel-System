import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../features/drivers/data/drivers_api.dart';
import '../../../features/drivers/domain/driver_models.dart';
import '../../../features/fleet/data/fleet_api.dart';
import '../../../features/fleet/domain/fleet_models.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';

enum AssignPickerContext { driver, booking, trip, vehicle }

String _vehicleSubtitle(AssignPickerContext ctx) => switch (ctx) {
      AssignPickerContext.driver => 'Select a vehicle for this driver',
      AssignPickerContext.booking => 'Select a vehicle for this booking',
      AssignPickerContext.trip => 'Select a vehicle for this trip',
      AssignPickerContext.vehicle => 'Select a vehicle',
    };

String _driverSubtitle(AssignPickerContext ctx) => switch (ctx) {
      AssignPickerContext.driver => 'Select a driver',
      AssignPickerContext.booking => 'Select a driver for this booking',
      AssignPickerContext.trip => 'Select a driver for this trip',
      AssignPickerContext.vehicle => 'Select a driver for this vehicle',
    };

Future<int?> pickDriverId(
  BuildContext context,
  WidgetRef ref, {
  AssignPickerContext pickerContext = AssignPickerContext.booking,
  int? excludeDriverId,
  bool requireVerified = false,
}) async {
  return showModalBottomSheet<int>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    backgroundColor: context.sg.card,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadii.xl)),
    ),
    builder: (ctx) => _DriverPickerSheet(
      subtitle: _driverSubtitle(pickerContext),
      excludeDriverId: excludeDriverId,
      requireVerified: requireVerified,
    ),
  );
}

Future<int?> pickVehicleId(
  BuildContext context,
  WidgetRef ref, {
  AssignPickerContext pickerContext = AssignPickerContext.driver,
  int? currentDriverId,
  bool filterAssignableOnly = true,
  bool warnOnOccupied = true,
}) async {
  return showModalBottomSheet<int>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    backgroundColor: context.sg.card,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadii.xl)),
    ),
    builder: (ctx) => _VehiclePickerSheet(
      subtitle: _vehicleSubtitle(pickerContext),
      currentDriverId: currentDriverId,
      filterAssignableOnly: filterAssignableOnly,
      warnOnOccupied: warnOnOccupied,
    ),
  );
}

class _DriverPickerSheet extends ConsumerStatefulWidget {
  const _DriverPickerSheet({
    required this.subtitle,
    this.excludeDriverId,
    this.requireVerified = false,
  });

  final String subtitle;
  final int? excludeDriverId;
  final bool requireVerified;

  @override
  ConsumerState<_DriverPickerSheet> createState() => _DriverPickerSheetState();
}

class _DriverPickerSheetState extends ConsumerState<_DriverPickerSheet> {
  String _query = '';
  bool _loading = true;
  String? _error;
  List<DriverListItem> _items = const [];

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
      final drivers = await ref.read(driversApiProvider).list(pageSize: 200);
      if (!mounted) return;
      setState(() {
        _items = drivers;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = ErrorHandler.message(e);
        _loading = false;
      });
    }
  }

  Future<void> _onTap(DriverListItem d) async {
    final block = driverAssignmentBlockReason(
      isActive: d.isActive,
      status: d.status,
      licenseExpired: d.licenseExpired,
      verificationStatus: d.verificationStatus,
      requireVerified: widget.requireVerified,
    );
    if (block != null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(block)));
      return;
    }
    Navigator.pop(context, d.id);
  }

  @override
  Widget build(BuildContext context) {
    final q = _query.trim().toLowerCase();
    final visible = _items.where((d) {
      if (widget.excludeDriverId != null && d.id == widget.excludeDriverId) {
        return false;
      }
      if (q.isEmpty) return true;
      return d.fullName.toLowerCase().contains(q) || d.phone.contains(q);
    }).toList();

    return _AssignPickerShell(
      title: 'Assign driver',
      subtitle: widget.subtitle,
      searchHint: 'Search name or phone…',
      query: _query,
      onQueryChanged: (v) => setState(() => _query = v),
      loading: _loading,
      error: _error,
      onRetry: _load,
      emptyIcon: Icons.groups_outlined,
      emptyTitle: 'No drivers available',
      itemCount: visible.length,
      itemBuilder: (context, i) {
        final d = visible[i];
        final name = d.fullName.trim().isEmpty ? 'Driver' : d.fullName.trim();
        final block = driverAssignmentBlockReason(
          isActive: d.isActive,
          status: d.status,
          licenseExpired: d.licenseExpired,
          verificationStatus: d.verificationStatus,
          requireVerified: widget.requireVerified,
        );
        final vehicleLine = d.assignedVehicleRegistration ??
            d.assignedVehicleName ??
            'No vehicle';
        return ListTile(
          enabled: block == null,
          contentPadding:
              const EdgeInsets.symmetric(horizontal: 20, vertical: 2),
          leading: SgAvatar(label: name, size: 42),
          title: Text(
            name,
            style: TextStyle(
              fontWeight: FontWeight.w700,
              color: block == null ? null : context.sg.textMuted,
            ),
          ),
          subtitle: Text(
            block ?? '${d.phone} · ${d.status} · $vehicleLine',
            style: TextStyle(
              fontSize: 12,
              color: block != null
                  ? AppColors.warning
                  : context.sg.textSecondary,
            ),
          ),
          trailing: StatusBadge(d.status),
          onTap: block == null ? () => _onTap(d) : null,
        );
      },
    );
  }
}

class _VehiclePickerSheet extends ConsumerStatefulWidget {
  const _VehiclePickerSheet({
    required this.subtitle,
    this.currentDriverId,
    this.filterAssignableOnly = true,
    this.warnOnOccupied = true,
  });

  final String subtitle;
  final int? currentDriverId;
  final bool filterAssignableOnly;
  final bool warnOnOccupied;

  @override
  ConsumerState<_VehiclePickerSheet> createState() =>
      _VehiclePickerSheetState();
}

class _VehiclePickerSheetState extends ConsumerState<_VehiclePickerSheet> {
  String _query = '';
  bool _loading = true;
  String? _error;
  List<VehicleListItem> _items = const [];

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
      final vehicles = await ref.read(fleetApiProvider).getAllVehicles();
      if (!mounted) return;
      setState(() {
        _items = vehicles;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = ErrorHandler.message(e);
        _loading = false;
      });
    }
  }

  Future<void> _onTap(VehicleListItem v) async {
    final block = v.assignBlockReason;
    if (block != null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(block)));
      return;
    }

    final occupiedByOther = v.hasAssignee &&
        (widget.currentDriverId == null ||
            v.driverId != widget.currentDriverId);
    if (widget.warnOnOccupied && occupiedByOther) {
      final assignee = (v.driverName ?? 'another driver').trim();
      final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Vehicle already assigned'),
          content: Text(
            '${v.name} (${v.registrationNumber}) is assigned to $assignee. '
            'Reassigning will transfer this vehicle to the selected driver.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('Transfer'),
            ),
          ],
        ),
      );
      if (ok != true || !mounted) return;
    }

    if (!mounted) return;
    Navigator.pop(context, v.id);
  }

  @override
  Widget build(BuildContext context) {
    final q = _query.trim().toLowerCase();
    final visible = _items.where((v) {
      if (widget.filterAssignableOnly && !v.isAssignableForDriver) {
        return false;
      }
      if (q.isEmpty) return true;
      return v.name.toLowerCase().contains(q) ||
          v.registrationNumber.toLowerCase().contains(q) ||
          (v.driverName?.toLowerCase().contains(q) ?? false);
    }).toList();

    return _AssignPickerShell(
      title: 'Assign vehicle',
      subtitle: widget.subtitle,
      searchHint: 'Search name, plate, or driver…',
      query: _query,
      onQueryChanged: (v) => setState(() => _query = v),
      loading: _loading,
      error: _error,
      onRetry: _load,
      emptyIcon: Icons.local_shipping_outlined,
      emptyTitle: 'No vehicles available',
      itemCount: visible.length,
      itemBuilder: (context, i) {
        final v = visible[i];
        final assignee = v.hasAssignee
            ? (v.driverName?.trim().isNotEmpty == true
                ? v.driverName!.trim()
                : 'Driver #${v.driverId}')
            : null;
        final subtitle = [
          v.registrationNumber,
          v.status,
          if (assignee != null) 'Assigned to $assignee',
        ].join(' · ');
        return ListTile(
          contentPadding:
              const EdgeInsets.symmetric(horizontal: 20, vertical: 2),
          leading: const SgIconWell(
            icon: Icons.local_shipping_outlined,
            size: 42,
          ),
          title: Text(
            v.name,
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
          subtitle: Text(
            subtitle,
            style: TextStyle(
              fontSize: 12,
              color: assignee != null
                  ? AppColors.warning
                  : context.sg.textSecondary,
            ),
          ),
          trailing: StatusBadge(v.status),
          onTap: () => _onTap(v),
        );
      },
    );
  }
}

class _AssignPickerShell extends StatelessWidget {
  const _AssignPickerShell({
    required this.title,
    required this.subtitle,
    required this.searchHint,
    required this.query,
    required this.onQueryChanged,
    required this.loading,
    required this.error,
    required this.onRetry,
    required this.emptyIcon,
    required this.emptyTitle,
    required this.itemCount,
    required this.itemBuilder,
  });

  final String title;
  final String subtitle;
  final String searchHint;
  final String query;
  final ValueChanged<String> onQueryChanged;
  final bool loading;
  final String? error;
  final VoidCallback onRetry;
  final IconData emptyIcon;
  final String emptyTitle;
  final int itemCount;
  final NullableIndexedWidgetBuilder itemBuilder;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(bottom: bottomInset),
        child: SizedBox(
          height: MediaQuery.sizeOf(context).height * 0.68,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 0, 20, 4),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                            fontWeight: FontWeight.w800,
                            letterSpacing: -0.3,
                            color: p.textPrimary,
                          ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      subtitle,
                      style: TextStyle(
                        fontSize: 13,
                        color: p.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
                child: SgSearchField(
                  hintText: searchHint,
                  onChanged: onQueryChanged,
                ),
              ),
              Expanded(
                child: loading
                    ? const Center(child: CircularProgressIndicator())
                    : error != null
                        ? SgErrorView(message: error!, onRetry: onRetry)
                        : itemCount == 0
                            ? SgEmptyState(
                                icon: emptyIcon,
                                title: emptyTitle,
                                subtitle: query.trim().isEmpty
                                    ? null
                                    : 'No matches for “$query”. Try another search.',
                              )
                            : ListView.separated(
                                itemCount: itemCount,
                                separatorBuilder: (_, __) => Divider(
                                  height: 1,
                                  indent: 78,
                                  color: p.divider,
                                ),
                                itemBuilder: itemBuilder,
                              ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
