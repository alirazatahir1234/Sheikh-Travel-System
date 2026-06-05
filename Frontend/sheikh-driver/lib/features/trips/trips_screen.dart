import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import '../../core/api/driver_api_client.dart';
import '../../core/services/session_storage.dart';
import '../auth/login_screen.dart';

class TripsScreen extends ConsumerStatefulWidget {
  const TripsScreen({super.key});

  @override
  ConsumerState<TripsScreen> createState() => _TripsScreenState();
}

class _TripsScreenState extends ConsumerState<TripsScreen> {
  List<dynamic> _trips = [];
  var _loading = true;
  Timer? _gpsTimer;
  int? _activeVehicleId;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _gpsTimer?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      _trips = await ref.read(driverApiProvider).getTrips();
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _start(int id, int? vehicleId) async {
    await ref.read(driverApiProvider).startTrip(id);
    if (vehicleId != null) {
      _activeVehicleId = vehicleId;
      _startGps();
    }
    await _load();
  }

  Future<void> _complete(int id) async {
    await ref.read(driverApiProvider).completeTrip(id);
    _gpsTimer?.cancel();
    _activeVehicleId = null;
    await _load();
  }

  void _startGps() {
    _gpsTimer?.cancel();
    _gpsTimer = Timer.periodic(const Duration(seconds: 20), (_) async {
      final vId = _activeVehicleId;
      if (vId == null) return;
      final pos = await Geolocator.getCurrentPosition();
      await ref.read(driverApiProvider).postLocation(
            vehicleId: vId,
            lat: pos.latitude,
            lng: pos.longitude,
            speed: pos.speed * 3.6,
          );
    });
  }

  Future<void> _logout() async {
    await ref.read(sessionTokenProvider.notifier).setToken(null);
    if (!mounted) return;
    Navigator.of(context).pushReplacement(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('My trips'),
        actions: [
          IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
          IconButton(onPressed: _logout, icon: const Icon(Icons.logout)),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _trips.isEmpty
              ? const Center(child: Text('No assigned trips'))
              : ListView.builder(
                  itemCount: _trips.length,
                  itemBuilder: (_, i) {
                    final t = _trips[i] as Map<String, dynamic>;
                    final id = t['id'] as int;
                    final status = t['status'] as int? ?? 0;
                    final vehicleId = t['vehicleId'] as int?;
                    return Card(
                      margin: const EdgeInsets.all(12),
                      child: ListTile(
                        title: Text(t['bookingNumber']?.toString() ?? 'Trip #$id'),
                        subtitle: Text(
                          '${t['routeName'] ?? ''}\n${t['customerName'] ?? ''}',
                        ),
                        isThreeLine: true,
                        trailing: status == 3
                            ? FilledButton(
                                onPressed: () => _complete(id),
                                child: const Text('End'),
                              )
                            : FilledButton(
                                onPressed: () => _start(id, vehicleId),
                                child: const Text('Start'),
                              ),
                      ),
                    );
                  },
                ),
    );
  }
}
