import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/auth/domain/auth_models.dart';
import '../../../features/bookings/presentation/dispatch_pickers.dart';
import '../../../features/drivers/data/drivers_api.dart';
import '../../../features/drivers/presentation/drivers_notifier.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/fleet_api.dart';
import '../domain/fleet_models.dart';
import 'fleet_hub_notifier.dart';

enum _AssignBoardFilter { all, unassigned, assigned }

final fleetAssignmentsProvider =
    FutureProvider.autoDispose<List<VehicleListItem>>((ref) async {
  return ref.watch(fleetApiProvider).getAllVehicles();
});

class FleetAssignmentsScreen extends ConsumerStatefulWidget {
  const FleetAssignmentsScreen({super.key});

  @override
  ConsumerState<FleetAssignmentsScreen> createState() =>
      _FleetAssignmentsScreenState();
}

class _FleetAssignmentsScreenState
    extends ConsumerState<FleetAssignmentsScreen> {
  String _query = '';
  _AssignBoardFilter _filter = _AssignBoardFilter.all;
  final Set<int> _busyVehicleIds = {};

  void _invalidateCaches({int? vehicleId, int? driverId}) {
    ref.invalidate(fleetAssignmentsProvider);
    ref.invalidate(fleetHubProvider);
    if (vehicleId != null) {
      ref.invalidate(vehicleDetailProvider(vehicleId));
    }
    if (driverId != null) {
      ref.invalidate(driverDetailProvider(driverId));
    }
  }

  Future<void> _assignToVehicle(VehicleListItem vehicle) async {
    final session = ref.read(fleetSessionProvider);
    final canAssign = session != null &&
        (session.hasPermission(FleetPermissions.driverAssign) ||
            session.hasPermission(FleetPermissions.vehicleUpdate));
    if (!canAssign) return;

    if (!vehicle.isAssignableForDriver) {
      final reason = vehicle.assignBlockReason ?? 'Vehicle cannot be assigned.';
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(reason)));
      }
      return;
    }

    final driverId = await pickDriverId(
      context,
      ref,
      pickerContext: AssignPickerContext.vehicle,
      excludeDriverId: vehicle.driverId,
      requireVerified: true,
    );
    if (driverId == null || !mounted) return;

    if (vehicle.hasAssignee &&
        vehicle.driverId != null &&
        vehicle.driverId != driverId) {
      final assignee = vehicle.driverName?.trim().isNotEmpty == true
          ? vehicle.driverName!.trim()
          : 'another driver';
      final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Reassign vehicle?'),
          content: Text(
            '${vehicle.name} is assigned to $assignee. Continue and transfer?',
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
      if (ok != true) return;
    }

    setState(() => _busyVehicleIds.add(vehicle.id));
    try {
      await ref.read(fleetApiProvider).assignDriver(vehicle.id, driverId);
      _invalidateCaches(vehicleId: vehicle.id, driverId: driverId);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Driver assigned')),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(ErrorHandler.message(e))),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _busyVehicleIds.remove(vehicle.id));
      }
    }
  }

  Future<void> _unassignVehicle(VehicleListItem vehicle) async {
    final driverId = vehicle.driverId;
    if (driverId == null) return;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Unassign driver?'),
        content: Text(
          '${vehicle.driverName ?? 'This driver'} will be removed from ${vehicle.name}.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Unassign'),
          ),
        ],
      ),
    );
    if (ok != true) return;

    setState(() => _busyVehicleIds.add(vehicle.id));
    try {
      await ref.read(driversApiProvider).unassignVehicle(driverId);
      _invalidateCaches(vehicleId: vehicle.id, driverId: driverId);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Driver unassigned')),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(ErrorHandler.message(e))),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _busyVehicleIds.remove(vehicle.id));
      }
    }
  }

  List<VehicleListItem> _visible(List<VehicleListItem> all) {
    final q = _query.trim().toLowerCase();
    return all.where((v) {
      switch (_filter) {
        case _AssignBoardFilter.unassigned:
          if (v.hasAssignee) return false;
        case _AssignBoardFilter.assigned:
          if (!v.hasAssignee) return false;
        case _AssignBoardFilter.all:
          break;
      }
      if (q.isEmpty) return true;
      return v.name.toLowerCase().contains(q) ||
          v.registrationNumber.toLowerCase().contains(q) ||
          (v.driverName?.toLowerCase().contains(q) ?? false);
    }).toList();
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(fleetAssignmentsProvider);
    final session = ref.watch(fleetSessionProvider);
    final canAssign = session != null &&
        (session.hasPermission(FleetPermissions.driverAssign) ||
            session.hasPermission(FleetPermissions.vehicleUpdate));
    final p = context.sg;

    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(
        title: const Text('Assignments'),
        actions: [
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () => ref.invalidate(fleetAssignmentsProvider),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: SgBrandLoader()),
        error: (e, _) => SgErrorView(
          message: '$e',
          onRetry: () => ref.invalidate(fleetAssignmentsProvider),
        ),
        data: (vehicles) {
          final visible = _visible(vehicles);
          final assignedCount = vehicles.where((v) => v.hasAssignee).length;
          final unassignedCount = vehicles.length - assignedCount;

          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () async {
              ref.invalidate(fleetAssignmentsProvider);
              await ref.read(fleetAssignmentsProvider.future);
            },
            child: CustomScrollView(
              physics: const AlwaysScrollableScrollPhysics(),
              slivers: [
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Vehicle assignments',
                          style: TextStyle(
                            fontWeight: FontWeight.w800,
                            fontSize: 18,
                            color: p.textPrimary,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '$assignedCount assigned · $unassignedCount open',
                          style: TextStyle(
                            fontSize: 13,
                            color: p.textSecondary,
                          ),
                        ),
                        const SizedBox(height: 12),
                        SgSearchField(
                          hintText: 'Search vehicle or driver…',
                          onChanged: (v) => setState(() => _query = v),
                        ),
                        const SizedBox(height: 10),
                        Wrap(
                          spacing: 8,
                          children: [
                            for (final f in _AssignBoardFilter.values)
                              ChoiceChip(
                                label: Text(switch (f) {
                                  _AssignBoardFilter.all => 'All',
                                  _AssignBoardFilter.unassigned => 'Open',
                                  _AssignBoardFilter.assigned => 'Assigned',
                                }),
                                selected: _filter == f,
                                onSelected: (_) => setState(() => _filter = f),
                              ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                if (visible.isEmpty)
                  const SliverFillRemaining(
                    hasScrollBody: false,
                    child: SgEmptyState(
                      icon: Icons.event_available_outlined,
                      title: 'No vehicles match',
                      subtitle: 'Try another filter or search.',
                    ),
                  )
                else
                  SliverPadding(
                    padding: EdgeInsets.fromLTRB(
                      16,
                      8,
                      16,
                      28 + MediaQuery.of(context).padding.bottom,
                    ),
                    sliver: SliverList.separated(
                      itemCount: visible.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 10),
                      itemBuilder: (context, i) {
                        final v = visible[i];
                        final busy = _busyVehicleIds.contains(v.id);
                        final assignable = v.isAssignableForDriver;
                        return SgCard(
                          padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
                          onTap: () => context.push('/fleet/vehicles/${v.id}'),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  const SgIconWell(
                                    icon: Icons.local_shipping_outlined,
                                    size: 40,
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          v.name,
                                          style: TextStyle(
                                            fontWeight: FontWeight.w700,
                                            color: p.textPrimary,
                                          ),
                                        ),
                                        const SizedBox(height: 2),
                                        Text(
                                          '${v.registrationNumber} · ${v.status}',
                                          style: TextStyle(
                                            fontSize: 12,
                                            color: p.textSecondary,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                  StatusBadge(v.status),
                                ],
                              ),
                              const SizedBox(height: 10),
                              Text(
                                v.hasAssignee
                                    ? 'Driver: ${v.driverName ?? 'Assigned'}'
                                    : 'Driver: Unassigned',
                                style: TextStyle(
                                  fontSize: 13,
                                  fontWeight: FontWeight.w600,
                                  color: v.hasAssignee
                                      ? p.textPrimary
                                      : AppColors.warning,
                                ),
                              ),
                              if (!assignable &&
                                  v.assignBlockReason != null) ...[
                                const SizedBox(height: 4),
                                Text(
                                  v.assignBlockReason!,
                                  style: const TextStyle(
                                    fontSize: 12,
                                    color: AppColors.error,
                                  ),
                                ),
                              ],
                              if (canAssign) ...[
                                const SizedBox(height: 10),
                                Row(
                                  children: [
                                    Expanded(
                                      child: FilledButton.icon(
                                        onPressed: busy || !assignable
                                            ? null
                                            : () => _assignToVehicle(v),
                                        icon: busy
                                            ? const SizedBox(
                                                width: 16,
                                                height: 16,
                                                child:
                                                    CircularProgressIndicator(
                                                  strokeWidth: 2,
                                                ),
                                              )
                                            : const Icon(
                                                Icons.person_add_alt_1_outlined,
                                                size: 18,
                                              ),
                                        label: Text(
                                          v.hasAssignee ? 'Reassign' : 'Assign',
                                        ),
                                      ),
                                    ),
                                    if (v.hasAssignee &&
                                        v.driverId != null) ...[
                                      const SizedBox(width: 8),
                                      Expanded(
                                        child: OutlinedButton.icon(
                                          onPressed: busy
                                              ? null
                                              : () => _unassignVehicle(v),
                                          icon: const Icon(
                                            Icons.link_off_outlined,
                                            size: 18,
                                          ),
                                          label: const Text('Unassign'),
                                        ),
                                      ),
                                    ],
                                  ],
                                ),
                              ],
                            ],
                          ),
                        );
                      },
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}
