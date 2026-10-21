import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../../auth/data/auth_repository.dart';
import '../../auth/domain/auth_models.dart';
import '../../bookings/presentation/dispatch_pickers.dart';
import '../data/maintenance_api.dart';
import 'maintenance_notifier.dart';

const _requestTypes = ['Corrective', 'Preventive', 'Breakdown'];
const _priorities = ['Low', 'Medium', 'High', 'Critical'];
const _categories = [
  'Engine',
  'Transmission',
  'Brake',
  'Tire',
  'Electrical',
  'Battery',
  'AC',
  'Body Damage',
  'Inspection',
  'Oil Change',
  'Breakdown',
  'Other',
];
const _maintenanceTypes = ['Preventive', 'Corrective', 'Emergency'];

Future<void> showCreateMaintenanceRequestSheet(
  BuildContext context,
  WidgetRef ref,
) async {
  final session = ref.read(fleetSessionProvider);
  if (session == null ||
      !session.hasPermission(FleetPermissions.maintenanceRequestCreate)) {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Missing Maintenance.Request.Create')),
    );
    return;
  }

  final created = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    backgroundColor: context.sg.card,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadii.xl)),
    ),
    builder: (_) => const _CreateRequestSheet(),
  );
  if (created == true && context.mounted) {
    await ref.read(maintenanceHubProvider.notifier).refresh();
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Service request created')),
      );
    }
  }
}

Future<void> showCreateWorkOrderSheet(
  BuildContext context,
  WidgetRef ref, {
  int? vehicleId,
  int? requestId,
  int? scheduleId,
}) async {
  final session = ref.read(fleetSessionProvider);
  if (session == null ||
      !session.hasPermission(FleetPermissions.maintenanceWorkOrderManage)) {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Missing Maintenance.WorkOrder.Manage')),
    );
    return;
  }

  final created = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    backgroundColor: context.sg.card,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadii.xl)),
    ),
    builder: (_) => _CreateWorkOrderSheet(
      initialVehicleId: vehicleId,
      requestId: requestId,
      scheduleId: scheduleId,
    ),
  );
  if (created == true && context.mounted) {
    await ref.read(maintenanceHubProvider.notifier).refresh();
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Work order created')),
      );
    }
  }
}

class _CreateRequestSheet extends ConsumerStatefulWidget {
  const _CreateRequestSheet();

  @override
  ConsumerState<_CreateRequestSheet> createState() =>
      _CreateRequestSheetState();
}

class _CreateRequestSheetState extends ConsumerState<_CreateRequestSheet> {
  final _desc = TextEditingController();
  final _location = TextEditingController();
  int? _vehicleId;
  String _type = _requestTypes.first;
  String _priority = 'Medium';
  String _category = _categories.first;
  bool _busy = false;

  @override
  void dispose() {
    _desc.dispose();
    _location.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_vehicleId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Select a vehicle')),
      );
      return;
    }
    final desc = _desc.text.trim();
    if (desc.length < 10) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Description needs at least 10 characters')),
      );
      return;
    }
    setState(() => _busy = true);
    try {
      await ref.read(maintenanceApiProvider).createRequest(
            vehicleId: _vehicleId!,
            requestType: _type,
            priority: _priority,
            issueCategory: _category,
            description: desc,
            breakdownLocation:
                _location.text.trim().isEmpty ? null : _location.text.trim(),
          );
      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(ErrorHandler.message(e))),
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final inset = MediaQuery.viewInsetsOf(context).bottom;
    return Padding(
      padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + inset),
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'New service request',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 14),
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text(
                _vehicleId == null ? 'Select vehicle' : 'Vehicle #$_vehicleId',
              ),
              trailing: const Icon(Icons.directions_car_outlined),
              onTap: _busy
                  ? null
                  : () async {
                      final id = await pickVehicleId(
                        context,
                        ref,
                        pickerContext: AssignPickerContext.vehicle,
                        filterAssignableOnly: false,
                        warnOnOccupied: false,
                      );
                      if (id != null) setState(() => _vehicleId = id);
                    },
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              value: _type,
              decoration: const InputDecoration(labelText: 'Request type'),
              items: _requestTypes
                  .map((e) => DropdownMenuItem(value: e, child: Text(e)))
                  .toList(),
              onChanged: _busy ? null : (v) => setState(() => _type = v!),
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              value: _priority,
              decoration: const InputDecoration(labelText: 'Priority'),
              items: _priorities
                  .map((e) => DropdownMenuItem(value: e, child: Text(e)))
                  .toList(),
              onChanged: _busy ? null : (v) => setState(() => _priority = v!),
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              value: _category,
              decoration: const InputDecoration(labelText: 'Category'),
              items: _categories
                  .map((e) => DropdownMenuItem(value: e, child: Text(e)))
                  .toList(),
              onChanged: _busy ? null : (v) => setState(() => _category = v!),
            ),
            const SizedBox(height: 8),
            SgSheetTextFormField(
              controller: _desc,
              label: 'Description',
              hint: 'Describe the issue (min 10 chars)',
              maxLines: 4,
            ),
            const SizedBox(height: 8),
            SgSheetTextFormField(
              controller: _location,
              label: 'Breakdown location (optional)',
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: _busy ? null : _submit,
              child: _busy
                  ? const SizedBox(
                      height: 20,
                      width: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Create request'),
            ),
          ],
        ),
      ),
    );
  }
}

class _CreateWorkOrderSheet extends ConsumerStatefulWidget {
  const _CreateWorkOrderSheet({
    this.initialVehicleId,
    this.requestId,
    this.scheduleId,
  });

  final int? initialVehicleId;
  final int? requestId;
  final int? scheduleId;

  @override
  ConsumerState<_CreateWorkOrderSheet> createState() =>
      _CreateWorkOrderSheetState();
}

class _CreateWorkOrderSheetState extends ConsumerState<_CreateWorkOrderSheet> {
  final _service = TextEditingController();
  final _notes = TextEditingController();
  late int? _vehicleId;
  String _type = _maintenanceTypes.first;
  String _priority = 'Medium';
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _vehicleId = widget.initialVehicleId;
  }

  @override
  void dispose() {
    _service.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_vehicleId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Select a vehicle')),
      );
      return;
    }
    setState(() => _busy = true);
    try {
      await ref.read(maintenanceApiProvider).createWorkOrder(
            vehicleId: _vehicleId!,
            requestId: widget.requestId,
            scheduleId: widget.scheduleId,
            serviceTypeName:
                _service.text.trim().isEmpty ? null : _service.text.trim(),
            maintenanceType: _type,
            priority: _priority,
            notes: _notes.text.trim().isEmpty ? null : _notes.text.trim(),
          );
      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(ErrorHandler.message(e))),
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final inset = MediaQuery.viewInsetsOf(context).bottom;
    return Padding(
      padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + inset),
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'New work order',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 14),
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text(
                _vehicleId == null ? 'Select vehicle' : 'Vehicle #$_vehicleId',
              ),
              trailing: const Icon(Icons.directions_car_outlined),
              onTap: _busy || widget.initialVehicleId != null
                  ? null
                  : () async {
                      final id = await pickVehicleId(
                        context,
                        ref,
                        pickerContext: AssignPickerContext.vehicle,
                        filterAssignableOnly: false,
                        warnOnOccupied: false,
                      );
                      if (id != null) setState(() => _vehicleId = id);
                    },
            ),
            DropdownButtonFormField<String>(
              value: _type,
              decoration: const InputDecoration(labelText: 'Maintenance type'),
              items: _maintenanceTypes
                  .map((e) => DropdownMenuItem(value: e, child: Text(e)))
                  .toList(),
              onChanged: _busy ? null : (v) => setState(() => _type = v!),
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              value: _priority,
              decoration: const InputDecoration(labelText: 'Priority'),
              items: _priorities
                  .map((e) => DropdownMenuItem(value: e, child: Text(e)))
                  .toList(),
              onChanged: _busy ? null : (v) => setState(() => _priority = v!),
            ),
            const SizedBox(height: 8),
            SgSheetTextFormField(
              controller: _service,
              label: 'Service type',
              hint: 'e.g. Oil Change',
            ),
            const SizedBox(height: 8),
            SgSheetTextFormField(
              controller: _notes,
              label: 'Notes (optional)',
              maxLines: 3,
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: _busy ? null : _submit,
              child: _busy
                  ? const SizedBox(
                      height: 20,
                      width: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Create work order'),
            ),
          ],
        ),
      ),
    );
  }
}
