import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../../auth/data/auth_repository.dart';
import '../../fleet/domain/fleet_models.dart';
import '../../fleet/presentation/fleet_hub_notifier.dart';
import '../../fleet/presentation/vehicle_commands_sheet.dart';
import '../../../shared/widgets/sg_brand_loader.dart';

class GpsCommandsHubScreen extends ConsumerWidget {
  const GpsCommandsHubScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(fleetSessionProvider);
    if (session == null || !session.canUseGpsCommands) {
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

    final hubAsync = ref.watch(fleetHubProvider);
    final top = MediaQuery.paddingOf(context).top;

    return Scaffold(
      backgroundColor: const Color(0xFFF2F7F6),
      body: Column(
        children: [
          Container(
            width: double.infinity,
            padding: EdgeInsets.fromLTRB(16, top + 12, 16, 18),
            decoration: const BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [AppColors.primary, AppColors.primaryDark],
              ),
            ),
            child: const Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'GPS Commands',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 24,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                SizedBox(height: 4),
                Text(
                  'Select a vehicle to manage GPS commands remotely.',
                  style: TextStyle(color: Color(0xFFE0F2F1), fontSize: 13.5),
                ),
              ],
            ),
          ),
          Expanded(
            child: hubAsync.when(
              loading: () => const Center(child: SgBrandLoader()),
              error: (e, _) => Center(child: Text('$e')),
              data: (hub) {
                final vehicles = hub.locations;
                if (vehicles.isEmpty) {
                  return const Center(child: Text('No vehicles with GPS'));
                }
                return ListView.separated(
                  padding: const EdgeInsets.fromLTRB(16, 16, 16, 100),
                  itemCount: vehicles.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 10),
                  itemBuilder: (context, i) {
                    final v = vehicles[i];
                    return _VehiclePickCard(
                      vehicle: v,
                      onTap: () =>
                          showVehicleCommandsSheet(context, v.vehicleId),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _VehiclePickCard extends StatelessWidget {
  const _VehiclePickCard({required this.vehicle, required this.onTap});

  final FleetVehicleLocation vehicle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final online = vehicle.status == FleetTrackStatus.moving ||
        vehicle.status == FleetTrackStatus.idle ||
        vehicle.status == FleetTrackStatus.parked ||
        vehicle.status == FleetTrackStatus.sos ||
        vehicle.status == FleetTrackStatus.unknown;
    final plate = vehicle.registrationNumber.trim().isNotEmpty
        ? vehicle.registrationNumber
        : vehicle.vehicleName;
    final sub = [
      if (vehicle.vehicleName != plate) vehicle.vehicleName,
      if (vehicle.vehicleType != null && vehicle.vehicleType!.isNotEmpty)
        vehicle.vehicleType!,
    ].join(' • ');

    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(16),
      elevation: 1,
      shadowColor: Colors.black12,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  color: const Color(0xFFCCFBF1),
                  borderRadius: BorderRadius.circular(12),
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
                        fontWeight: FontWeight.w800,
                        fontSize: 15,
                        color: Color(0xFF172033),
                      ),
                    ),
                    if (sub.isNotEmpty) ...[
                      const SizedBox(height: 2),
                      Text(
                        sub,
                        softWrap: true,
                        style: const TextStyle(
                          fontSize: 12,
                          color: Color(0xFF64748B),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: online
                      ? const Color(0xFFDCFCE7)
                      : const Color(0xFFF1F5F9),
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  online ? 'Online' : 'Offline',
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: online
                        ? const Color(0xFF16A34A)
                        : const Color(0xFF64748B),
                  ),
                ),
              ),
              const SizedBox(width: 4),
              const Icon(Icons.chevron_right, color: Color(0xFF94A3B8)),
            ],
          ),
        ),
      ),
    );
  }
}
