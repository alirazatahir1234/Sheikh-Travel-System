import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../data/drivers_api.dart';
import '../domain/driver_models.dart';
import 'drivers_notifier.dart';

/// Create or edit a driver (OWNER / staff with Driver.Create|Update).
class DriverFormScreen extends ConsumerStatefulWidget {
  const DriverFormScreen({super.key, this.driverId});

  final int? driverId;

  bool get isEdit => driverId != null;

  @override
  ConsumerState<DriverFormScreen> createState() => _DriverFormScreenState();
}

class _DriverFormScreenState extends ConsumerState<DriverFormScreen> {
  final _formKey = GlobalKey<FormState>();
  final _firstName = TextEditingController();
  final _lastName = TextEditingController();
  final _phone = TextEditingController();
  final _email = TextEditingController();
  final _license = TextEditingController();
  final _cnic = TextEditingController();
  final _address = TextEditingController();
  final _emergencyName = TextEditingController();
  final _emergencyPhone = TextEditingController();

  DateTime? _licenseExpiry;
  DateTime? _dateOfBirth;
  String _status = 'Available';
  bool _isActive = true;
  bool _loading = false;
  bool _saving = false;
  String? _loadError;

  @override
  void initState() {
    super.initState();
    if (widget.isEdit) {
      _loadExisting();
    } else {
      _licenseExpiry = DateTime.now().toUtc().add(const Duration(days: 365));
      _dateOfBirth = DateTime.now().toUtc().subtract(const Duration(days: 365 * 25));
    }
  }

  Future<void> _loadExisting() async {
    setState(() {
      _loading = true;
      _loadError = null;
    });
    try {
      final d = await ref.read(driversApiProvider).getById(widget.driverId!);
      if (!mounted) return;
      _applyDetail(d);
      setState(() => _loading = false);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _loadError = ErrorHandler.message(e);
      });
    }
  }

  void _applyDetail(DriverDetail d) {
    final names = _splitName(d);
    _firstName.text = names.$1;
    _lastName.text = names.$2;
    _phone.text = d.phone;
    _email.text = d.email ?? '';
    _license.text = d.licenseNumber;
    _cnic.text = d.cnic ?? '';
    _address.text = d.address ?? '';
    _emergencyName.text = d.emergencyContactName ?? '';
    _emergencyPhone.text = d.emergencyContact ?? '';
    _licenseExpiry = d.licenseExpiryDate;
    _dateOfBirth = d.dateOfBirth;
    _status = d.status.isNotEmpty ? d.status : 'Available';
    _isActive = d.isActive;
  }

  (String, String) _splitName(DriverDetail d) {
    final first = (d.firstName ?? '').trim();
    final last = (d.lastName ?? '').trim();
    if (first.isNotEmpty || last.isNotEmpty) return (first, last);
    final parts = d.fullName.trim().split(RegExp(r'\s+'));
    if (parts.isEmpty) return ('', '');
    if (parts.length == 1) return (parts.first, '');
    return (parts.first, parts.sublist(1).join(' '));
  }

  @override
  void dispose() {
    _firstName.dispose();
    _lastName.dispose();
    _phone.dispose();
    _email.dispose();
    _license.dispose();
    _cnic.dispose();
    _address.dispose();
    _emergencyName.dispose();
    _emergencyPhone.dispose();
    super.dispose();
  }

  Future<void> _pickDate({
    required DateTime? current,
    required ValueChanged<DateTime> onPicked,
    DateTime? firstDate,
    DateTime? lastDate,
  }) async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: current ?? now,
      firstDate: firstDate ?? DateTime(1950),
      lastDate: lastDate ?? DateTime(now.year + 20),
    );
    if (picked != null) onPicked(picked);
  }

  Map<String, dynamic> _buildPayload() {
    final payload = <String, dynamic>{
      'firstName': _firstName.text.trim(),
      'lastName': _lastName.text.trim(),
      'phone': _phone.text.trim(),
      'email': _email.text.trim(),
      'licenseNumber': _license.text.trim(),
      'licenseExpiryDate': _licenseExpiry!.toUtc().toIso8601String(),
      'dateOfBirth': _dateOfBirth!.toUtc().toIso8601String(),
      if (_cnic.text.trim().isNotEmpty) 'cnic': _cnic.text.trim(),
      if (_address.text.trim().isNotEmpty) 'address': _address.text.trim(),
      if (_emergencyName.text.trim().isNotEmpty)
        'emergencyContactName': _emergencyName.text.trim(),
      if (_emergencyPhone.text.trim().isNotEmpty)
        'emergencyContact': _emergencyPhone.text.trim(),
    };
    if (widget.isEdit) {
      payload['status'] = _status;
      payload['isActive'] = _isActive;
    }
    return payload;
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    if (_licenseExpiry == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('License expiry is required')),
      );
      return;
    }
    if (_dateOfBirth == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Date of birth is required')),
      );
      return;
    }
    setState(() => _saving = true);
    try {
      final api = ref.read(driversApiProvider);
      final payload = _buildPayload();
      if (widget.isEdit) {
        await api.update(widget.driverId!, payload);
        ref.invalidate(driverDetailProvider(widget.driverId!));
        ref.invalidate(driversHubProvider);
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Driver updated')),
        );
        context.pop();
      } else {
        final id = await api.create(payload);
        ref.invalidate(driversHubProvider);
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Driver created')),
        );
        context.go('/more/drivers/$id');
      }
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(ErrorHandler.message(e))),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final df = DateFormat('dd MMM yyyy');
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: Text(widget.isEdit ? 'Edit driver' : 'Add driver'),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _loadError != null
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(_loadError!, textAlign: TextAlign.center),
                        const SizedBox(height: 12),
                        FilledButton(
                          onPressed: _loadExisting,
                          child: const Text('Retry'),
                        ),
                      ],
                    ),
                  ),
                )
              : Form(
                  key: _formKey,
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
                    children: [
                      TextFormField(
                        controller: _firstName,
                        textCapitalization: TextCapitalization.words,
                        decoration: const InputDecoration(labelText: 'First name *'),
                        validator: (v) =>
                            (v == null || v.trim().isEmpty) ? 'Required' : null,
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _lastName,
                        textCapitalization: TextCapitalization.words,
                        decoration: const InputDecoration(labelText: 'Last name *'),
                        validator: (v) =>
                            (v == null || v.trim().isEmpty) ? 'Required' : null,
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _phone,
                        keyboardType: TextInputType.phone,
                        decoration: const InputDecoration(labelText: 'Phone *'),
                        validator: (v) =>
                            (v == null || v.trim().isEmpty) ? 'Required' : null,
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _email,
                        keyboardType: TextInputType.emailAddress,
                        decoration: const InputDecoration(labelText: 'Email *'),
                        validator: (v) {
                          final t = v?.trim() ?? '';
                          if (t.isEmpty) return 'Required';
                          if (!t.contains('@')) return 'Enter a valid email';
                          return null;
                        },
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _license,
                        decoration:
                            const InputDecoration(labelText: 'License number *'),
                        validator: (v) =>
                            (v == null || v.trim().isEmpty) ? 'Required' : null,
                      ),
                      const SizedBox(height: 12),
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        title: const Text('License expiry *'),
                        subtitle: Text(
                          _licenseExpiry != null
                              ? df.format(_licenseExpiry!.toLocal())
                              : 'Select date',
                        ),
                        trailing: const Icon(Icons.calendar_today_outlined),
                        onTap: () => _pickDate(
                          current: _licenseExpiry,
                          firstDate: DateTime.now(),
                          onPicked: (d) => setState(() => _licenseExpiry = d),
                        ),
                      ),
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        title: const Text('Date of birth *'),
                        subtitle: Text(
                          _dateOfBirth != null
                              ? df.format(_dateOfBirth!.toLocal())
                              : 'Select date',
                        ),
                        trailing: const Icon(Icons.cake_outlined),
                        onTap: () => _pickDate(
                          current: _dateOfBirth,
                          firstDate: DateTime(1950),
                          lastDate: DateTime.now()
                              .subtract(const Duration(days: 365 * 18)),
                          onPicked: (d) => setState(() => _dateOfBirth = d),
                        ),
                      ),
                      const SizedBox(height: 8),
                      TextFormField(
                        controller: _cnic,
                        decoration: const InputDecoration(labelText: 'CNIC'),
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _address,
                        maxLines: 2,
                        decoration: const InputDecoration(labelText: 'Address'),
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _emergencyName,
                        decoration: const InputDecoration(
                          labelText: 'Emergency contact name',
                        ),
                      ),
                      const SizedBox(height: 12),
                      TextFormField(
                        controller: _emergencyPhone,
                        keyboardType: TextInputType.phone,
                        decoration: const InputDecoration(
                          labelText: 'Emergency contact phone',
                        ),
                      ),
                      if (widget.isEdit) ...[
                        const SizedBox(height: 8),
                        SwitchListTile(
                          contentPadding: EdgeInsets.zero,
                          title: const Text('Active'),
                          subtitle: Text('Current status: $_status'),
                          value: _isActive,
                          onChanged: (v) => setState(() => _isActive = v),
                        ),
                      ],
                      const SizedBox(height: 24),
                      FilledButton(
                        onPressed: _saving ? null : _save,
                        child: _saving
                            ? const SizedBox(
                                width: 22,
                                height: 22,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : Text(widget.isEdit ? 'Save changes' : 'Create driver'),
                      ),
                    ],
                  ),
                ),
    );
  }
}
