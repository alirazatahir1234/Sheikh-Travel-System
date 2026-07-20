import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../data/inspection_api.dart';
import '../domain/inspection_models.dart';

final _templateProvider = FutureProvider.autoDispose<InspectionTemplate>(
  (ref) => ref.read(inspectionApiProvider).getTemplate(),
);

final _vehiclesProvider = FutureProvider.autoDispose<List<InspectionVehicle>>(
  (ref) => ref.read(inspectionApiProvider).getVehicles(),
);

final _historyProvider = FutureProvider.autoDispose<List<InspectionSummary>>(
  (ref) => ref.read(inspectionApiProvider).getHistory(),
);

class InspectionScreen extends ConsumerStatefulWidget {
  const InspectionScreen({super.key});

  @override
  ConsumerState<InspectionScreen> createState() => _InspectionScreenState();
}

class _InspectionScreenState extends ConsumerState<InspectionScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 2, vsync: this);
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Vehicle Inspection'),
        bottom: TabBar(
          controller: _tabs,
          labelColor: AppColors.primary,
          unselectedLabelColor: AppColors.textSecondary,
          indicatorColor: AppColors.primary,
          tabs: const [
            Tab(text: 'New check'),
            Tab(text: 'History'),
          ],
        ),
      ),
      body: TabBarView(
        controller: _tabs,
        children: [
          _NewInspectionTab(
            onSubmitted: () {
              ref.invalidate(_historyProvider);
              _tabs.animateTo(1);
            },
          ),
          const _HistoryTab(),
        ],
      ),
    );
  }
}

class _NewInspectionTab extends ConsumerStatefulWidget {
  const _NewInspectionTab({required this.onSubmitted});
  final VoidCallback onSubmitted;

  @override
  ConsumerState<_NewInspectionTab> createState() => _NewInspectionTabState();
}

class _NewInspectionTabState extends ConsumerState<_NewInspectionTab> {
  final _odometerCtrl = TextEditingController();
  final _commentsCtrl = TextEditingController();
  final _sigKey = GlobalKey();
  final List<_StrokePoint?> _strokes = [];
  final List<File> _photos = [];
  final Map<String, InspectionResultItem> _results = {};
  int? _vehicleId;
  bool _busy = false;

  @override
  void dispose() {
    _odometerCtrl.dispose();
    _commentsCtrl.dispose();
    super.dispose();
  }

  void _ensureResults(InspectionTemplate template) {
    for (final item in template.items) {
      _results.putIfAbsent(
        item.key,
        () => InspectionResultItem(key: item.key, status: 'Pass'),
      );
    }
  }

  Future<void> _pickPhoto(ImageSource source) async {
    final picker = ImagePicker();
    final x = await picker.pickImage(
      source: source,
      imageQuality: 70,
      maxWidth: 1600,
    );
    if (x == null) return;
    setState(() => _photos.add(File(x.path)));
  }

  Future<File?> _exportSignature() async {
    if (_strokes.whereType<_StrokePoint>().isEmpty) return null;
    final boundary =
        _sigKey.currentContext?.findRenderObject() as RenderRepaintBoundary?;
    if (boundary == null) return null;
    final image = await boundary.toImage(pixelRatio: 2);
    final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
    if (bytes == null) return null;
    final dir = Directory.systemTemp;
    final file = File(
        '${dir.path}/sig_${DateTime.now().millisecondsSinceEpoch}.png');
    await file.writeAsBytes(bytes.buffer.asUint8List());
    return file;
  }

  Future<void> _submit(InspectionTemplate template) async {
    if (_vehicleId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Select a vehicle')),
      );
      return;
    }
    for (final item in template.items.where((i) => i.required)) {
      if (!_results.containsKey(item.key)) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Complete: ${item.label}')),
        );
        return;
      }
    }

    setState(() => _busy = true);
    try {
      final sig = await _exportSignature();
      final id = await ref.read(inspectionApiProvider).submit(
            vehicleId: _vehicleId!,
            templateId: template.id,
            results: _results.values.toList(),
            odometer: double.tryParse(_odometerCtrl.text.trim()),
            comments: _commentsCtrl.text.trim().isEmpty
                ? null
                : _commentsCtrl.text.trim(),
            photos: _photos,
            signature: sig,
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(id == 0
              ? 'Inspection saved offline — will sync when online'
              : 'Inspection submitted'),
          backgroundColor: id == 0 ? AppColors.warning : AppColors.success,
        ),
      );
      setState(() {
        _photos.clear();
        _strokes.clear();
        _commentsCtrl.clear();
        _odometerCtrl.clear();
        for (final r in _results.values) {
          r.status = 'Pass';
          r.comment = null;
        }
      });
      widget.onSubmitted();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('$e'), backgroundColor: AppColors.error),
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final templateAsync = ref.watch(_templateProvider);
    final vehiclesAsync = ref.watch(_vehiclesProvider);

    return templateAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(child: Text('$e')),
      data: (template) {
        _ensureResults(template);
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text(template.name,
                style: const TextStyle(
                    fontWeight: FontWeight.w700, fontSize: 16)),
            if (template.description != null) ...[
              const SizedBox(height: 4),
              Text(template.description!,
                  style: const TextStyle(
                      color: AppColors.textSecondary, fontSize: 13)),
            ],
            const SizedBox(height: 16),
            vehiclesAsync.when(
              loading: () => const LinearProgressIndicator(),
              error: (e, _) => Text('$e'),
              data: (vehicles) {
                if (vehicles.isEmpty) {
                  return const Text(
                    'No vehicles available. Assign a vehicle to a trip first.',
                    style: TextStyle(color: AppColors.error),
                  );
                }
                _vehicleId ??= vehicles.first.id;
                return DropdownMenu<int>(
                  width: double.infinity,
                  initialSelection: _vehicleId,
                  label: const Text('Vehicle'),
                  dropdownMenuEntries: vehicles
                      .map((v) => DropdownMenuEntry(value: v.id, label: v.label))
                      .toList(),
                  onSelected: (v) {
                    if (v != null) setState(() => _vehicleId = v);
                  },
                );
              },
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _odometerCtrl,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(
                labelText: 'Odometer reading',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 16),
            const Text('Checklist',
                style: TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 8),
            ...template.items.map((item) {
              final result = _results[item.key]!;
              return Card(
                margin: const EdgeInsets.only(bottom: 8),
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        item.required ? '${item.label} *' : item.label,
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                      const SizedBox(height: 8),
                      SegmentedButton<String>(
                        segments: const [
                          ButtonSegment(value: 'Pass', label: Text('Pass')),
                          ButtonSegment(
                              value: 'Warning', label: Text('Warn')),
                          ButtonSegment(value: 'Fail', label: Text('Fail')),
                        ],
                        selected: {result.status},
                        onSelectionChanged: (s) =>
                            setState(() => result.status = s.first),
                      ),
                    ],
                  ),
                ),
              );
            }),
            const SizedBox(height: 8),
            const Text('Photos',
                style: TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                ..._photos.map((f) => ClipRRect(
                      borderRadius: BorderRadius.circular(8),
                      child: Image.file(f,
                          width: 72, height: 72, fit: BoxFit.cover),
                    )),
                OutlinedButton.icon(
                  onPressed: () => _pickPhoto(ImageSource.camera),
                  icon: const Icon(Icons.camera_alt),
                  label: const Text('Camera'),
                ),
                OutlinedButton.icon(
                  onPressed: () => _pickPhoto(ImageSource.gallery),
                  icon: const Icon(Icons.photo_library),
                  label: const Text('Gallery'),
                ),
              ],
            ),
            const SizedBox(height: 16),
            const Text('Signature',
                style: TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 8),
            Container(
              height: 140,
              decoration: BoxDecoration(
                border: Border.all(color: AppColors.divider),
                borderRadius: BorderRadius.circular(12),
                color: Colors.white,
              ),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: RepaintBoundary(
                  key: _sigKey,
                  child: GestureDetector(
                    onPanStart: (d) => setState(
                        () => _strokes.add(_StrokePoint(d.localPosition))),
                    onPanUpdate: (d) => setState(
                        () => _strokes.add(_StrokePoint(d.localPosition))),
                    onPanEnd: (_) => setState(() => _strokes.add(null)),
                    child: CustomPaint(
                      painter: _SignaturePainter(_strokes),
                      child: const SizedBox.expand(),
                    ),
                  ),
                ),
              ),
            ),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton(
                onPressed: () => setState(() => _strokes.clear()),
                child: const Text('Clear signature'),
              ),
            ),
            TextField(
              controller: _commentsCtrl,
              maxLines: 2,
              decoration: const InputDecoration(
                labelText: 'Comments',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 20),
            FilledButton.icon(
              onPressed: _busy ? null : () => _submit(template),
              icon: _busy
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.fact_check),
              label: const Text('Submit Inspection'),
              style: FilledButton.styleFrom(
                  minimumSize: const Size.fromHeight(48)),
            ),
            const SizedBox(height: 24),
          ],
        );
      },
    );
  }
}

class _HistoryTab extends ConsumerWidget {
  const _HistoryTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(_historyProvider);
    return async.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('$e'),
            FilledButton(
              onPressed: () => ref.invalidate(_historyProvider),
              child: const Text('Retry'),
            ),
          ],
        ),
      ),
      data: (items) {
        if (items.isEmpty) {
          return const Center(child: Text('No inspections yet'));
        }
        final fmt = DateFormat('dd MMM yyyy, HH:mm');
        return RefreshIndicator(
          onRefresh: () async => ref.invalidate(_historyProvider),
          child: ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: items.length,
            separatorBuilder: (_, __) => const SizedBox(height: 8),
            itemBuilder: (_, i) {
              final item = items[i];
              final color = switch (item.result) {
                'Fail' => AppColors.error,
                'Warning' => AppColors.warning,
                _ => AppColors.success,
              };
              return Card(
                child: ListTile(
                  leading: CircleAvatar(
                    backgroundColor: color.withValues(alpha: 0.15),
                    child: Icon(Icons.fact_check, color: color),
                  ),
                  title: Text(item.vehicleName ?? 'Vehicle #${item.vehicleId}'),
                  subtitle: Text(
                    '${fmt.format(item.inspectionDate.toLocal())}'
                    '${item.photoCount > 0 ? ' · ${item.photoCount} photos' : ''}'
                    '${item.hasSignature ? ' · signed' : ''}',
                  ),
                  trailing: Text(
                    item.result,
                    style: TextStyle(
                        color: color, fontWeight: FontWeight.w700),
                  ),
                ),
              );
            },
          ),
        );
      },
    );
  }
}

class _StrokePoint {
  _StrokePoint(this.offset);
  final Offset offset;
}

class _SignaturePainter extends CustomPainter {
  _SignaturePainter(this.points);
  final List<_StrokePoint?> points;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = Colors.black87
      ..strokeWidth = 2.5
      ..strokeCap = StrokeCap.round
      ..style = PaintingStyle.stroke;
    for (var i = 0; i < points.length - 1; i++) {
      final a = points[i];
      final b = points[i + 1];
      if (a != null && b != null) {
        canvas.drawLine(a.offset, b.offset, paint);
      }
    }
  }

  @override
  bool shouldRepaint(covariant _SignaturePainter oldDelegate) => true;
}
