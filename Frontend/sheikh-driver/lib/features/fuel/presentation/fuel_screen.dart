import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../inspection/data/inspection_api.dart';
import '../../inspection/domain/inspection_models.dart';
import 'fuel_notifier.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../../../shared/widgets/sg_brand_loader.dart';

final _fuelVehiclesProvider = FutureProvider.autoDispose<List<InspectionVehicle>>(
  (ref) => ref.read(inspectionApiProvider).getVehicles(),
);

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
  final _money = NumberFormat('#,##0.00');

  double get _total {
    final liters =
        double.tryParse(_litersCtrl.text.trim().replaceAll(',', '.')) ?? 0;
    final price =
        double.tryParse(_priceCtrl.text.trim().replaceAll(',', '.')) ?? 0;
    return liters * price;
  }

  @override
  void dispose() {
    _litersCtrl.dispose();
    _priceCtrl.dispose();
    _odoCtrl.dispose();
    _stationCtrl.dispose();
    super.dispose();
  }

  Future<void> _pickImage(ImageSource source) async {
    final picked = await _picker.pickImage(
      source: source,
      imageQuality: 55,
      maxWidth: 1600,
      maxHeight: 1600,
    );
    if (picked == null) return;
    setState(() => _receiptImage = File(picked.path));
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
            if (_receiptImage != null)
              ListTile(
                leading: const Icon(Icons.delete_outline, color: AppColors.error),
                title: const Text('Remove photo'),
                onTap: () {
                  Navigator.pop(context);
                  setState(() => _receiptImage = null);
                },
              ),
          ],
        ),
      ),
    );
  }

  Future<void> _previewImage() async {
    if (_receiptImage == null) return;
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => _ReceiptPreviewPage(file: _receiptImage!),
      ),
    );
  }

  Future<void> _scanOcr() async {
    if (_receiptImage == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Add a receipt photo first')),
      );
      return;
    }
    final suggestion =
        await ref.read(fuelNotifierProvider.notifier).scan(_receiptImage!);
    if (!mounted || suggestion == null) return;
    if (!suggestion.hasAny) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Could not read amounts — enter details manually'),
        ),
      );
      return;
    }
    setState(() {
      if (suggestion.liters != null) {
        _litersCtrl.text = suggestion.liters!.toStringAsFixed(
          suggestion.liters! == suggestion.liters!.roundToDouble() ? 0 : 2,
        );
      }
      if (suggestion.pricePerLiter != null) {
        _priceCtrl.text = suggestion.pricePerLiter!.toStringAsFixed(2);
      }
      if (suggestion.station != null && suggestion.station!.isNotEmpty) {
        _stationCtrl.text = suggestion.station!;
      }
      if (suggestion.fuelType != null &&
          ['Petrol', 'Diesel', 'CNG'].contains(suggestion.fuelType)) {
        _fuelType = suggestion.fuelType!;
      }
    });
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          'OCR filled fields (${suggestion.confidence}% confidence) — review before submit',
        ),
        backgroundColor: AppColors.success,
      ),
    );
  }

  void _resetForm() {
    _litersCtrl.clear();
    _priceCtrl.clear();
    _odoCtrl.clear();
    _stationCtrl.clear();
    setState(() {
      _fuelType = 'Petrol';
      _receiptImage = null;
    });
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    if (_vehicleId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Select a vehicle')),
      );
      return;
    }
    if (_receiptImage == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Receipt photo is required')),
      );
      return;
    }

    await ref.read(fuelNotifierProvider.notifier).submit(
          vehicleId: _vehicleId!,
          liters: double.parse(_litersCtrl.text.trim().replaceAll(',', '.')),
          pricePerLiter:
              double.parse(_priceCtrl.text.trim().replaceAll(',', '.')),
          odometerReading: double.parse(_odoCtrl.text.trim()),
          station: _stationCtrl.text.trim(),
          fuelType: _fuelType,
          receipt: _receiptImage,
        );
  }

  @override
  Widget build(BuildContext context) {
    final vehiclesAsync = ref.watch(_fuelVehiclesProvider);
    final historyAsync = ref.watch(fuelHistoryProvider);
    final fuel = ref.watch(fuelNotifierProvider);

    ref.listen<FuelSubmitState>(fuelNotifierProvider, (_, next) {
      if (next.success && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(next.queuedOffline
                ? 'Receipt saved offline — will upload when online'
                : 'Fuel receipt uploaded'),
            backgroundColor:
                next.queuedOffline ? AppColors.warning : AppColors.success,
          ),
        );
        _resetForm();
      }
      if (next.error != null && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(next.error!), backgroundColor: AppColors.error),
        );
      }
    });

    return Scaffold(
      backgroundColor: context.sg.surface,
      appBar: AppBar(
        title: const Text('Fuel Receipt'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () {
              ref.invalidate(_fuelVehiclesProvider);
              ref.invalidate(fuelHistoryProvider);
            },
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(_fuelVehiclesProvider);
          ref.invalidate(fuelHistoryProvider);
        },
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _ReceiptPhotoWidget(
                    image: _receiptImage,
                    onTap: _showImagePicker,
                    onPreview: _receiptImage != null ? _previewImage : null,
                    onRemove: () => setState(() => _receiptImage = null),
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: fuel.scanning || _receiptImage == null
                              ? null
                              : _scanOcr,
                          icon: fuel.scanning
                              ? const SizedBox(
                                  width: 16,
                                  height: 16,
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                )
                              : const Icon(Icons.document_scanner_outlined),
                          label: Text(fuel.scanning ? 'Scanning…' : 'Scan receipt (OCR)'),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  vehiclesAsync.when(
                    loading: () => const LinearProgressIndicator(),
                    error: (e, _) => Text('$e',
                        style: const TextStyle(color: AppColors.error)),
                    data: (vehicles) {
                      if (vehicles.isEmpty) {
                        return Text(
                          'No vehicles available. Assign a vehicle to a trip first.',
                          style: TextStyle(color: context.sg.textSecondary),
                        );
                      }
                      _vehicleId ??= vehicles.first.id;
                      return DropdownMenu<int>(
                        initialSelection: _vehicleId,
                        label: const Text('Vehicle'),
                        width: double.infinity,
                        dropdownMenuEntries: vehicles
                            .map(
                              (v) => DropdownMenuEntry(
                                value: v.id,
                                label: v.plate == null || v.plate!.isEmpty
                                    ? v.name
                                    : '${v.name} · ${v.plate}',
                              ),
                            )
                            .toList(),
                        onSelected: (id) => setState(() => _vehicleId = id),
                      );
                    },
                  ),
                  const SizedBox(height: 16),
                  const Text('Fuel Type',
                      style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    children: ['Petrol', 'Diesel', 'CNG'].map((type) {
                      final selected = _fuelType == type;
                      return FilterChip(
                        label: Text(type),
                        selected: selected,
                        onSelected: (_) => setState(() => _fuelType = type),
                        selectedColor: AppColors.primary.withValues(alpha: 0.15),
                        checkmarkColor: AppColors.primary,
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: 16),
                  // Tap-to-enter amount sheet avoids the floating keyboard side
                  // toolbar (mic / emoji strip) that covers the left of these fields.
                  SgSheetAmountFormField(
                    controller: _litersCtrl,
                    label: 'Liters',
                    unit: 'L',
                    helperExample: 'e.g. 35',
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) return 'Required';
                      final n = double.tryParse(v.trim().replaceAll(',', '.'));
                      if (n == null || n <= 0) return 'Enter liters';
                      return null;
                    },
                  ),
                  const SizedBox(height: 12),
                  SgSheetAmountFormField(
                    controller: _priceCtrl,
                    label: 'Price / Liter',
                    unit: 'PKR',
                    helperExample: 'e.g. 280',
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) return 'Required';
                      final n = double.tryParse(v.trim().replaceAll(',', '.'));
                      if (n == null || n < 0) return 'Invalid price';
                      return null;
                    },
                  ),
                  const SizedBox(height: 12),
                  ListenableBuilder(
                    listenable: Listenable.merge([_litersCtrl, _priceCtrl]),
                    builder: (context, _) {
                      return Container(
                        width: double.infinity,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 14,
                          vertical: 14,
                        ),
                        decoration: BoxDecoration(
                          color: AppColors.primary.withValues(alpha: 0.08),
                          borderRadius: BorderRadius.circular(AppRadii.md),
                          border: Border.all(
                            color: AppColors.primary.withValues(alpha: 0.28),
                          ),
                        ),
                        child: Row(
                          children: [
                            Text(
                              'Total Cost',
                              style: TextStyle(
                                color: context.sg.textSecondary,
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                            const Spacer(),
                            Text(
                              'PKR ${_money.format(_total)}',
                              style: const TextStyle(
                                fontWeight: FontWeight.w800,
                                fontSize: 18,
                                letterSpacing: -0.3,
                                color: AppColors.primaryDark,
                              ),
                            ),
                          ],
                        ),
                      );
                    },
                  ),
                  const SizedBox(height: 12),
                  SgSheetAmountFormField(
                    controller: _odoCtrl,
                    label: 'Odometer Reading',
                    unit: 'km',
                    helperExample: 'e.g. 45210',
                    allowDecimal: false,
                    validator: (v) {
                      if (v == null || v.trim().isEmpty) return 'Required';
                      if (int.tryParse(v.trim()) == null) return 'Invalid';
                      return null;
                    },
                  ),
                  const SizedBox(height: 12),
                  SgSheetTextFormField(
                    controller: _stationCtrl,
                    label: 'Station Name',
                    helperExample: 'e.g. PSO Shell Main',
                    validator: (v) =>
                        (v == null || v.trim().isEmpty) ? 'Required' : null,
                  ),
                  const SizedBox(height: 24),
                  SizedBox(
                    width: double.infinity,
                    height: 50,
                    child: FilledButton(
                      onPressed: fuel.loading ? null : _submit,
                      child: fuel.loading
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                  strokeWidth: 2, color: Colors.white),
                            )
                          : const Text('Upload Receipt',
                              style: TextStyle(
                                  fontSize: 16, fontWeight: FontWeight.w600)),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 28),
            Text(
              'Recent receipts',
              style: TextStyle(
                fontWeight: FontWeight.w800,
                fontSize: 15.5,
                letterSpacing: -0.25,
                color: context.sg.textPrimary,
              ),
            ),
            const SizedBox(height: 12),
            historyAsync.when(
              loading: () => const Padding(
                padding: EdgeInsets.all(24),
                child: Center(child: SgBrandLoader()),
              ),
              error: (e, _) => Text('$e'),
              data: (logs) {
                if (logs.isEmpty) {
                  return Padding(
                    padding: const EdgeInsets.symmetric(vertical: 16),
                    child: Text(
                      'No fuel receipts yet.',
                      style: TextStyle(
                        color: context.sg.textSecondary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  );
                }
                return Column(
                  children: logs.map((log) => _HistoryTile(log: log)).toList(),
                );
              },
            ),
            const SizedBox(height: 24),
          ],
        ),
      ),
    );
  }
}

class _HistoryTile extends StatelessWidget {
  const _HistoryTile({required this.log});
  final dynamic log;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final dateFmt = DateFormat('dd MMM yyyy · HH:mm');
    final money = NumberFormat('#,##0.00');
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        color: p.card,
        borderRadius: BorderRadius.circular(AppRadii.lg),
        border: Border.all(color: p.border.withValues(alpha: 0.72)),
        boxShadow: AppShadows.card,
      ),
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
        leading: log.receiptUrl != null && (log.receiptUrl as String).isNotEmpty
            ? ClipRRect(
                borderRadius: BorderRadius.circular(AppRadii.sm),
                child: Image.network(
                  log.receiptUrl as String,
                  width: 48,
                  height: 48,
                  fit: BoxFit.cover,
                  errorBuilder: (_, __, ___) => Container(
                    width: 48,
                    height: 48,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: AppColors.warning.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(AppRadii.sm),
                    ),
                    child: const Icon(
                      Icons.local_gas_station_rounded,
                      color: AppColors.warning,
                    ),
                  ),
                ),
              )
            : Container(
                width: 48,
                height: 48,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: AppColors.warning.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(AppRadii.sm),
                ),
                child: const Icon(
                  Icons.local_gas_station_rounded,
                  color: AppColors.warning,
                ),
              ),
        title: Text(
          '${log.liters} L · ${log.fuelType ?? 'Fuel'}',
          style: TextStyle(
            fontWeight: FontWeight.w800,
            letterSpacing: -0.15,
            color: p.textPrimary,
          ),
        ),
        subtitle: Text(
          [
            if (log.station != null && (log.station as String).isNotEmpty)
              log.station,
            dateFmt.format(log.fuelDate as DateTime),
          ].join(' · '),
          style: TextStyle(fontSize: 12, color: p.textMuted),
        ),
        trailing: Text(
          'PKR ${money.format(log.totalCost)}',
          style: const TextStyle(
            fontWeight: FontWeight.w800,
            color: AppColors.primaryDark,
          ),
        ),
        onTap: log.receiptUrl != null && (log.receiptUrl as String).isNotEmpty
            ? () => Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) =>
                        _NetworkPreviewPage(url: log.receiptUrl as String),
                  ),
                )
            : null,
      ),
    );
  }
}

class _ReceiptPhotoWidget extends StatelessWidget {
  const _ReceiptPhotoWidget({
    required this.onTap,
    required this.onRemove,
    this.onPreview,
    this.image,
  });

  final VoidCallback onTap;
  final VoidCallback onRemove;
  final VoidCallback? onPreview;
  final File? image;

  @override
  Widget build(BuildContext context) {
    if (image != null) {
      return Stack(
        children: [
          GestureDetector(
            onTap: onPreview,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(12),
              child: Image.file(
                image!,
                width: double.infinity,
                height: 200,
                fit: BoxFit.cover,
              ),
            ),
          ),
          Positioned(
            top: 8,
            right: 8,
            child: Row(
              children: [
                if (onPreview != null)
                  _RoundIcon(icon: Icons.zoom_in, onTap: onPreview!),
                const SizedBox(width: 6),
                _RoundIcon(icon: Icons.edit, onTap: onTap),
                const SizedBox(width: 6),
                _RoundIcon(icon: Icons.close, onTap: onRemove),
              ],
            ),
          ),
          const Positioned(
            left: 10,
            bottom: 10,
            child: DecoratedBox(
              decoration: BoxDecoration(
                color: Colors.black54,
                borderRadius: BorderRadius.all(Radius.circular(6)),
              ),
              child: Padding(
                padding: EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                child: Text(
                  'Tap to preview · compressed',
                  style: TextStyle(color: Colors.white, fontSize: 11),
                ),
              ),
            ),
          ),
        ],
      );
    }

    return GestureDetector(
      onTap: onTap,
      child: Container(
        height: 140,
        width: double.infinity,
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: 0.04),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: AppColors.primary.withValues(alpha: 0.25),
          ),
        ),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.camera_alt_outlined,
                size: 36, color: AppColors.primary.withValues(alpha: 0.6)),
            const SizedBox(height: 8),
            Text(
              'Add receipt photo (required)',
              style: TextStyle(color: context.sg.textSecondary, fontSize: 13),
            ),
            const SizedBox(height: 4),
            Text(
              'Camera or gallery · auto-compressed',
              style: TextStyle(
                color: context.sg.textSecondary.withValues(alpha: 0.8),
                fontSize: 11,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _RoundIcon extends StatelessWidget {
  const _RoundIcon({required this.icon, required this.onTap});
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        decoration: const BoxDecoration(
          color: Colors.black54,
          shape: BoxShape.circle,
        ),
        padding: const EdgeInsets.all(6),
        child: Icon(icon, color: Colors.white, size: 18),
      ),
    );
  }
}

class _ReceiptPreviewPage extends StatelessWidget {
  const _ReceiptPreviewPage({required this.file});
  final File file;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: const Text('Receipt preview'),
      ),
      body: Center(
        child: InteractiveViewer(
          child: Image.file(file),
        ),
      ),
    );
  }
}

class _NetworkPreviewPage extends StatelessWidget {
  const _NetworkPreviewPage({required this.url});
  final String url;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: const Text('Receipt'),
      ),
      body: Center(
        child: InteractiveViewer(
          child: Image.network(url),
        ),
      ),
    );
  }
}
