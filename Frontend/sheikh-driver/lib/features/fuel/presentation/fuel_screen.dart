import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../../../features/trips/presentation/trips_notifier.dart';
import 'fuel_notifier.dart';

class FuelScreen extends ConsumerStatefulWidget {
  const FuelScreen({super.key});

  @override
  ConsumerState<FuelScreen> createState() => _FuelScreenState();
}

class _FuelScreenState extends ConsumerState<FuelScreen> {
  final _formKey = GlobalKey<FormState>();
  final _litersCtrl = TextEditingController();
  final _priceCtrl = TextEditingController();
  final _odoCtrl = TextEditingController();
  final _stationCtrl = TextEditingController();
  String _fuelType = 'Petrol';
  int? _vehicleId;
  File? _receiptImage;
  final _picker = ImagePicker();

  double get _total =>
      (double.tryParse(_litersCtrl.text) ?? 0) * (double.tryParse(_priceCtrl.text) ?? 0);

  @override
  void dispose() {
    _litersCtrl.dispose();
    _priceCtrl.dispose();
    _odoCtrl.dispose();
    _stationCtrl.dispose();
    super.dispose();
  }

  Future<void> _pickImage(ImageSource source) async {
    final picked = await _picker.pickImage(source: source, imageQuality: 75, maxWidth: 1280);
    if (picked != null) setState(() => _receiptImage = File(picked.path));
  }

  void _showImagePicker() {
    showModalBottomSheet(
      context: context,
      builder: (_) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.camera_alt),
              title: const Text('Take Photo'),
              onTap: () {
                Navigator.pop(context);
                _pickImage(ImageSource.camera);
              },
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Choose from Gallery'),
              onTap: () {
                Navigator.pop(context);
                _pickImage(ImageSource.gallery);
              },
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    if (_vehicleId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('No active vehicle found')),
      );
      return;
    }

    final session = ref.read(authRepositoryProvider).session;
    await ref.read(fuelNotifierProvider.notifier).submit(
          vehicleId: _vehicleId!,
          liters: double.parse(_litersCtrl.text),
          pricePerLiter: double.parse(_priceCtrl.text),
          totalCost: _total,
          odometerReading: double.parse(_odoCtrl.text),
          station: _stationCtrl.text.trim(),
          fuelType: _fuelType,
          driverId: session?.driverId,
        );
  }

  @override
  Widget build(BuildContext context) {
    // Pre-fill vehicle from the first active/confirmed trip
    final trips = ref.watch(tripsProvider).valueOrNull ?? [];
    final activeTrip = trips.where((t) => t.isStarted || t.isConfirmed).firstOrNull;
    if (activeTrip?.vehicleId != null && _vehicleId == null) {
      _vehicleId = activeTrip!.vehicleId;
    }

    ref.listen<FuelSubmitState>(fuelNotifierProvider, (_, next) {
      if (next.success && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Fuel receipt submitted'),
            backgroundColor: AppColors.success,
          ),
        );
        Navigator.of(context).pop();
      }
      if (next.error != null && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(next.error!), backgroundColor: AppColors.error),
        );
      }
    });

    final fuel = ref.watch(fuelNotifierProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Fuel Receipt')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Receipt photo
              _ReceiptPhotoWidget(
                image: _receiptImage,
                onTap: _showImagePicker,
                onRemove: () => setState(() => _receiptImage = null),
              ),
              const SizedBox(height: 20),

              // Vehicle info
              if (activeTrip != null)
                _InfoCard(
                  icon: Icons.directions_car,
                  label: 'Vehicle',
                  value: activeTrip.vehicleName ?? 'ID: $_vehicleId',
                ),
              const SizedBox(height: 16),

              // Fuel type selector
              const Text('Fuel Type',
                  style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
              const SizedBox(height: 8),
              Row(
                children: ['Petrol', 'Diesel', 'CNG', 'Electric'].map((type) {
                  final selected = _fuelType == type;
                  return Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: FilterChip(
                      label: Text(type),
                      selected: selected,
                      onSelected: (_) => setState(() => _fuelType = type),
                      selectedColor: AppColors.primary.withValues(alpha: 0.15),
                      checkmarkColor: AppColors.primary,
                    ),
                  );
                }).toList(),
              ),
              const SizedBox(height: 16),

              Row(
                children: [
                  Expanded(
                    child: _Field(
                      controller: _litersCtrl,
                      label: 'Liters',
                      suffix: 'L',
                      keyboardType: TextInputType.number,
                      onChanged: (_) => setState(() {}),
                      validator: (v) {
                        if (v == null || v.isEmpty) return 'Required';
                        if (double.tryParse(v) == null) return 'Invalid';
                        return null;
                      },
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: _Field(
                      controller: _priceCtrl,
                      label: 'Price / Liter',
                      prefix: 'SAR',
                      keyboardType: TextInputType.number,
                      onChanged: (_) => setState(() {}),
                      validator: (v) {
                        if (v == null || v.isEmpty) return 'Required';
                        if (double.tryParse(v) == null) return 'Invalid';
                        return null;
                      },
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),

              // Total (computed)
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.06),
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: AppColors.primary.withValues(alpha: 0.2)),
                ),
                child: Row(
                  children: [
                    const Text('Total Cost',
                        style: TextStyle(color: AppColors.textSecondary, fontSize: 13)),
                    const Spacer(),
                    Text(
                      'SAR ${_total.toStringAsFixed(2)}',
                      style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 18,
                          color: AppColors.primary),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),

              _Field(
                controller: _odoCtrl,
                label: 'Odometer Reading',
                suffix: 'km',
                keyboardType: TextInputType.number,
                inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                validator: (v) {
                  if (v == null || v.isEmpty) return 'Required';
                  return null;
                },
              ),
              const SizedBox(height: 12),

              _Field(
                controller: _stationCtrl,
                label: 'Station Name',
                validator: (v) =>
                    (v == null || v.trim().isEmpty) ? 'Required' : null,
              ),
              const SizedBox(height: 28),

              SizedBox(
                width: double.infinity,
                height: 50,
                child: FilledButton(
                  onPressed: fuel.loading ? null : _submit,
                  child: fuel.loading
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                        )
                      : const Text('Submit Receipt',
                          style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
                ),
              ),
              const SizedBox(height: 16),
            ],
          ),
        ),
      ),
    );
  }
}

class _ReceiptPhotoWidget extends StatelessWidget {
  const _ReceiptPhotoWidget({required this.onTap, required this.onRemove, this.image});
  final VoidCallback onTap;
  final VoidCallback onRemove;
  final File? image;

  @override
  Widget build(BuildContext context) {
    if (image != null) {
      return Stack(
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: Image.file(image!,
                width: double.infinity, height: 180, fit: BoxFit.cover),
          ),
          Positioned(
            top: 8,
            right: 8,
            child: GestureDetector(
              onTap: onRemove,
              child: Container(
                decoration: const BoxDecoration(
                  color: Colors.black54,
                  shape: BoxShape.circle,
                ),
                padding: const EdgeInsets.all(4),
                child: const Icon(Icons.close, color: Colors.white, size: 18),
              ),
            ),
          ),
        ],
      );
    }

    return GestureDetector(
      onTap: onTap,
      child: Container(
        height: 130,
        width: double.infinity,
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: 0.04),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
              color: AppColors.primary.withValues(alpha: 0.25), style: BorderStyle.solid),
        ),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.camera_alt_outlined,
                size: 36, color: AppColors.primary.withValues(alpha: 0.6)),
            const SizedBox(height: 8),
            Text('Tap to add receipt photo (optional)',
                style: TextStyle(
                    color: AppColors.textSecondary.withValues(alpha: 0.8), fontSize: 13)),
          ],
        ),
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({required this.icon, required this.label, required this.value});
  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: AppColors.divider),
        ),
        child: Row(
          children: [
            Icon(icon, size: 18, color: AppColors.textSecondary),
            const SizedBox(width: 10),
            Text(label,
                style: const TextStyle(color: AppColors.textSecondary, fontSize: 13)),
            const SizedBox(width: 8),
            Text(value,
                style: const TextStyle(
                    fontWeight: FontWeight.w600, color: AppColors.textPrimary)),
          ],
        ),
      );
}

class _Field extends StatelessWidget {
  const _Field({
    required this.controller,
    required this.label,
    this.suffix,
    this.prefix,
    this.keyboardType,
    this.inputFormatters,
    this.validator,
    this.onChanged,
  });

  final TextEditingController controller;
  final String label;
  final String? suffix;
  final String? prefix;
  final TextInputType? keyboardType;
  final List<TextInputFormatter>? inputFormatters;
  final String? Function(String?)? validator;
  final void Function(String)? onChanged;

  @override
  Widget build(BuildContext context) => TextFormField(
        controller: controller,
        keyboardType: keyboardType,
        inputFormatters: inputFormatters,
        onChanged: onChanged,
        validator: validator,
        decoration: InputDecoration(
          labelText: label,
          suffixText: suffix,
          prefixText: prefix,
          border: const OutlineInputBorder(),
        ),
      );
}
