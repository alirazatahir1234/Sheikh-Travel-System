import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../fleet/domain/fleet_models.dart';
import '../../fleet/presentation/fleet_hub_notifier.dart';
import '../../fleet/presentation/widgets/geofence_distance.dart';
import '../data/geofence_api.dart';
import '../domain/geofence_models.dart';

enum _Shape { circle, polygon, rectangle }

/// Create / edit a zone on the map, with vehicle assignment and Entry/Exit
/// alerts saved in the same request so a new zone alerts immediately.
class GeofenceEditorScreen extends ConsumerStatefulWidget {
  const GeofenceEditorScreen({super.key, this.existing});

  final GpsGeofenceItem? existing;

  @override
  ConsumerState<GeofenceEditorScreen> createState() =>
      _GeofenceEditorScreenState();
}

class _GeofenceEditorScreenState extends ConsumerState<GeofenceEditorScreen> {
  static const _categories = [
    'General',
    'Parking',
    'Depot',
    'Customer site',
    'Restricted',
  ];

  final _form = GlobalKey<FormState>();
  late final TextEditingController _name;
  late final TextEditingController _description;
  late String _category;
  late bool _isActive;
  late bool _alertOnEnter;
  late bool _alertOnExit;
  late _Shape _shape;
  LatLng? _center;
  double _radius = 300;
  List<LatLng> _vertices = [];
  LatLng? _rectStart;
  Set<int> _vehicleIds = {};
  bool _loadingAssignments = false;
  bool _saving = false;

  bool get _isEdit => widget.existing != null;

  @override
  void initState() {
    super.initState();
    final g = widget.existing;
    _name = TextEditingController(text: g?.name ?? '');
    _description = TextEditingController(text: g?.description ?? '');
    _category = (g?.category ?? '').trim().isEmpty ? 'General' : g!.category!;
    _isActive = g?.isActive ?? true;
    _alertOnEnter = g == null || g.alertOnEnter;
    _alertOnExit = g == null || g.alertOnExit;
    if (g == null || g.isCircle) {
      _shape = _Shape.circle;
      if (g != null) {
        _center = LatLng(g.centerLat, g.centerLng);
        if (g.radiusMeters > 0) _radius = g.radiusMeters;
      }
    } else {
      final ring = parseGeofenceRing(g.geoJson);
      _vertices = [for (final p in ring) LatLng(p.lat, p.lng)];
      _shape = g.areaType.toLowerCase().contains('rect') && ring.length == 4
          ? _Shape.rectangle
          : _Shape.polygon;
    }
    if (g != null) _loadAssignments(g.id);
  }

  Future<void> _loadAssignments(int id) async {
    setState(() => _loadingAssignments = true);
    try {
      final rows = await ref.read(geofenceApiProvider).assignments(id);
      if (!mounted) return;
      setState(() => _vehicleIds = {
            for (final a in rows)
              if (a.vehicleId != null) a.vehicleId!,
          });
    } catch (_) {
      // Leave selection empty; saving then sends the user's explicit choice.
    } finally {
      if (mounted) setState(() => _loadingAssignments = false);
    }
  }

  @override
  void dispose() {
    _name.dispose();
    _description.dispose();
    super.dispose();
  }

  LatLng get _initialTarget {
    if (_center != null) return _center!;
    if (_vertices.isNotEmpty) return _vertices.first;
    final hub = ref.read(fleetHubProvider).valueOrNull;
    final v = hub?.locations.where((l) => l.hasMapCoords).firstOrNull;
    if (v != null) return LatLng(v.latitude!, v.longitude!);
    return const LatLng(31.5204, 74.3587);
  }

  void _setShape(_Shape s) {
    setState(() {
      _shape = s;
      _rectStart = null;
      if (s == _Shape.rectangle && _vertices.length != 4) _vertices = [];
    });
  }

  void _onMapTap(LatLng p) {
    setState(() {
      switch (_shape) {
        case _Shape.circle:
          _center = p;
        case _Shape.polygon:
          _vertices = [..._vertices, p];
        case _Shape.rectangle:
          if (_rectStart == null) {
            _rectStart = p;
            _vertices = [];
          } else {
            final a = _rectStart!;
            _vertices = [
              LatLng(a.latitude, a.longitude),
              LatLng(a.latitude, p.longitude),
              LatLng(p.latitude, p.longitude),
              LatLng(p.latitude, a.longitude),
            ];
            _rectStart = null;
          }
      }
    });
  }

  void _moveVertex(int i, LatLng p) {
    setState(() {
      if (_shape == _Shape.rectangle && _vertices.length == 4) {
        // Keep the rectangle axis-aligned: drag a corner, opposite stays fixed.
        final o = _vertices[(i + 2) % 4];
        _vertices = [
          LatLng(o.latitude, o.longitude),
          LatLng(o.latitude, p.longitude),
          LatLng(p.latitude, p.longitude),
          LatLng(p.latitude, o.longitude),
        ];
      } else {
        _vertices = [..._vertices]..[i] = p;
      }
    });
  }

  List<({double lat, double lng})> get _ring =>
      [for (final v in _vertices) (lat: v.latitude, lng: v.longitude)];

  String get _hint => switch (_shape) {
        _Shape.circle => _center == null
            ? 'Tap the map to place the centre.'
            : 'Drag the pin to move · slider sets radius.',
        _Shape.polygon => _vertices.length < 3
            ? 'Tap to add points (${_vertices.length}/3 minimum).'
            : 'Shape closed · ${_vertices.length} points. Drag points to adjust.',
        _Shape.rectangle => _rectStart != null
            ? 'Tap the opposite corner.'
            : (_vertices.isEmpty
                ? 'Tap one corner, then the opposite corner.'
                : 'Drag a corner to resize.'),
      };

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    final geometryError = validateGeofenceGeometry(
      circle: _shape == _Shape.circle,
      radiusMeters: _radius,
      hasCenter: _center != null,
      vertices: _ring,
    );
    if (geometryError != null) {
      _snack(geometryError);
      return;
    }
    final description = _description.text.trim();
    final circle = _shape == _Shape.circle;
    final draft = GeofenceDraft(
      name: _name.text.trim(),
      category: _category,
      areaType: _shape.name,
      centerLat: circle ? _center!.latitude : 0,
      centerLng: circle ? _center!.longitude : 0,
      radiusMeters: circle ? _radius : 0,
      geoJson: circle ? null : geofenceRingToGeoJson(_ring),
      description: description.isEmpty ? null : description,
      color: widget.existing?.color,
      isActive: _isActive,
      alertOnEnter: _alertOnEnter,
      alertOnExit: _alertOnExit,
      vehicleIds: _vehicleIds.toList(),
    );

    setState(() => _saving = true);
    try {
      final api = ref.read(geofenceApiProvider);
      if (_isEdit) {
        await api.update(widget.existing!.id, draft);
      } else {
        await api.create(draft);
      }
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) _snack(ErrorHandler.message(e));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _delete() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete zone?'),
        content: Text(
          '"${widget.existing!.name}" and its assignments and alert rules '
          'will be removed. Past alerts are kept.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            style: TextButton.styleFrom(foregroundColor: AppColors.error),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    setState(() => _saving = true);
    try {
      await ref.read(geofenceApiProvider).delete(widget.existing!.id);
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) _snack(ErrorHandler.message(e));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  void _snack(String message) => ScaffoldMessenger.of(context)
      .showSnackBar(SnackBar(content: Text(message)));

  Future<void> _pickVehicles(List<FleetVehicleLocation> vehicles) async {
    final picked = await showModalBottomSheet<Set<int>>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      backgroundColor: context.sg.card,
      builder: (_) => _VehiclePicker(vehicles: vehicles, selected: _vehicleIds),
    );
    if (picked != null) setState(() => _vehicleIds = picked);
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final categories = {..._categories, _category}.toList();
    final vehicles =
        ref.watch(fleetHubProvider).valueOrNull?.locations ?? const [];
    final shapeClosed = _vertices.length >= 3;

    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(
        title: Text(_isEdit ? 'Edit zone' : 'New zone'),
        actions: [
          if (_isEdit)
            IconButton(
              tooltip: 'Delete zone',
              onPressed: _saving ? null : _delete,
              icon: const Icon(Icons.delete_outline_rounded),
            ),
          TextButton(
            onPressed: _saving ? null : _save,
            child: _saving
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Save'),
          ),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            flex: 5,
            child: Stack(
              children: [
                GoogleMap(
                  initialCameraPosition:
                      CameraPosition(target: _initialTarget, zoom: 14),
                  onTap: _onMapTap,
                  myLocationButtonEnabled: false,
                  mapToolbarEnabled: false,
                  circles: {
                    if (_shape == _Shape.circle && _center != null)
                      Circle(
                        circleId: const CircleId('draft'),
                        center: _center!,
                        radius: _radius,
                        fillColor: AppColors.primary.withValues(alpha: 0.16),
                        strokeColor: AppColors.primary,
                        strokeWidth: 2,
                      ),
                  },
                  polygons: {
                    if (_shape != _Shape.circle && shapeClosed)
                      Polygon(
                        polygonId: const PolygonId('draft'),
                        points: _vertices,
                        fillColor: AppColors.primary.withValues(alpha: 0.16),
                        strokeColor: AppColors.primary,
                        strokeWidth: 2,
                      ),
                  },
                  polylines: {
                    if (_shape == _Shape.polygon && _vertices.length == 2)
                      Polyline(
                        polylineId: const PolylineId('draft-edge'),
                        points: _vertices,
                        color: AppColors.primary,
                        width: 2,
                      ),
                  },
                  markers: {
                    if (_shape == _Shape.circle && _center != null)
                      Marker(
                        markerId: const MarkerId('c'),
                        position: _center!,
                        draggable: true,
                        onDragEnd: (p) => setState(() => _center = p),
                      ),
                    if (_shape == _Shape.rectangle && _rectStart != null)
                      Marker(
                        markerId: const MarkerId('r0'),
                        position: _rectStart!,
                      ),
                    if (_shape != _Shape.circle)
                      for (var i = 0; i < _vertices.length; i++)
                        Marker(
                          markerId: MarkerId('v$i'),
                          position: _vertices[i],
                          draggable: true,
                          alpha: 0.85,
                          onDragEnd: (p) => _moveVertex(i, p),
                        ),
                  },
                ),
                Positioned(
                  left: 12,
                  right: 12,
                  top: 12,
                  child: Material(
                    color: p.card,
                    elevation: 2,
                    borderRadius: BorderRadius.circular(12),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 8,
                      ),
                      child: Text(
                        _hint,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(color: p.textSecondary, fontSize: 13),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            flex: 6,
            child: Form(
              key: _form,
              child: ListView(
                padding: EdgeInsets.fromLTRB(
                  16,
                  12,
                  16,
                  16 + MediaQuery.paddingOf(context).bottom,
                ),
                children: [
                  SegmentedButton<_Shape>(
                    showSelectedIcon: false,
                    segments: const [
                      ButtonSegment(
                        value: _Shape.circle,
                        label: Text('Circle'),
                        icon: Icon(Icons.radio_button_unchecked),
                      ),
                      ButtonSegment(
                        value: _Shape.polygon,
                        label: Text('Polygon'),
                        icon: Icon(Icons.pentagon_outlined),
                      ),
                      ButtonSegment(
                        value: _Shape.rectangle,
                        label: Text('Rect'),
                        icon: Icon(Icons.crop_square_rounded),
                      ),
                    ],
                    selected: {_shape},
                    onSelectionChanged: (s) => _setShape(s.first),
                  ),
                  const SizedBox(height: 12),
                  if (_shape == _Shape.circle) ...[
                    Text(
                      'Radius: ${_radius.toStringAsFixed(0)} m',
                      style: TextStyle(color: p.textSecondary),
                    ),
                    Slider(
                      value: _radius.clamp(
                        geofenceMinRadiusMeters,
                        5000,
                      ),
                      min: geofenceMinRadiusMeters,
                      max: 5000,
                      divisions: 99,
                      label: '${_radius.toStringAsFixed(0)} m',
                      onChanged: (v) => setState(() => _radius = v),
                    ),
                  ] else
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        if (_shape == _Shape.polygon)
                          OutlinedButton.icon(
                            onPressed: _vertices.isEmpty
                                ? null
                                : () => setState(() => _vertices =
                                    _vertices.sublist(0, _vertices.length - 1)),
                            icon: const Icon(Icons.undo_rounded),
                            label: const Text('Undo point'),
                          ),
                        OutlinedButton.icon(
                          onPressed: _vertices.isEmpty && _rectStart == null
                              ? null
                              : () => setState(() {
                                    _vertices = [];
                                    _rectStart = null;
                                  }),
                          icon: const Icon(Icons.delete_outline_rounded),
                          label: const Text('Clear shape'),
                        ),
                      ],
                    ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _name,
                    maxLength: 100,
                    decoration: const InputDecoration(
                      labelText: 'Zone name',
                      border: OutlineInputBorder(),
                    ),
                    validator: (v) => (v ?? '').trim().isEmpty
                        ? 'Zone name is required.'
                        : null,
                  ),
                  const SizedBox(height: 8),
                  DropdownButtonFormField<String>(
                    initialValue: _category,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'Category',
                      border: OutlineInputBorder(),
                    ),
                    items: [
                      for (final c in categories)
                        DropdownMenuItem(value: c, child: Text(c)),
                    ],
                    onChanged: (v) => setState(() => _category = v ?? _category),
                  ),
                  const SizedBox(height: 12),
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.directions_car_outlined),
                    title: const Text('Vehicles to watch'),
                    subtitle: Text(
                      _loadingAssignments
                          ? 'Loading…'
                          : _vehicleIds.isEmpty
                              ? 'None selected — zone will not be monitored.'
                              : '${_vehicleIds.length} selected',
                      style: TextStyle(
                        color: _vehicleIds.isEmpty && !_loadingAssignments
                            ? AppColors.warning
                            : p.textSecondary,
                      ),
                    ),
                    trailing: const Icon(Icons.chevron_right_rounded),
                    onTap: _loadingAssignments
                        ? null
                        : () => _pickVehicles(vehicles),
                  ),
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Alert on enter'),
                    value: _alertOnEnter,
                    onChanged: (v) => setState(() => _alertOnEnter = v),
                  ),
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Alert on exit'),
                    value: _alertOnExit,
                    onChanged: (v) => setState(() => _alertOnExit = v),
                  ),
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Active'),
                    subtitle: Text(
                      'Inactive zones are not monitored.',
                      style: TextStyle(color: p.textSecondary),
                    ),
                    value: _isActive,
                    onChanged: (v) => setState(() => _isActive = v),
                  ),
                  const SizedBox(height: 8),
                  TextFormField(
                    controller: _description,
                    maxLength: 500,
                    maxLines: 2,
                    decoration: const InputDecoration(
                      labelText: 'Description (optional)',
                      border: OutlineInputBorder(),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _VehiclePicker extends StatefulWidget {
  const _VehiclePicker({required this.vehicles, required this.selected});

  final List<FleetVehicleLocation> vehicles;
  final Set<int> selected;

  @override
  State<_VehiclePicker> createState() => _VehiclePickerState();
}

class _VehiclePickerState extends State<_VehiclePicker> {
  late final Set<int> _selected = {...widget.selected};
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final q = _query.toLowerCase();
    final rows = widget.vehicles
        .where((v) =>
            q.isEmpty ||
            v.vehicleName.toLowerCase().contains(q) ||
            v.registrationNumber.toLowerCase().contains(q))
        .toList();

    return DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.75,
      maxChildSize: 0.95,
      builder: (ctx, scroll) => Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    'Vehicles (${_selected.length})',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: p.textPrimary,
                    ),
                  ),
                ),
                TextButton(
                  onPressed: () => setState(() => rows.length == _selected.length
                      ? _selected.clear()
                      : _selected.addAll(rows.map((v) => v.vehicleId))),
                  child: Text(
                    rows.length == _selected.length ? 'Clear' : 'Select all',
                  ),
                ),
                FilledButton(
                  onPressed: () => Navigator.of(context).pop(_selected),
                  child: const Text('Done'),
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: TextField(
              decoration: const InputDecoration(
                isDense: true,
                hintText: 'Search vehicles',
                prefixIcon: Icon(Icons.search_rounded),
                border: OutlineInputBorder(),
              ),
              onChanged: (v) => setState(() => _query = v),
            ),
          ),
          Expanded(
            child: rows.isEmpty
                ? Center(
                    child: Text(
                      'No vehicles found.',
                      style: TextStyle(color: p.textMuted),
                    ),
                  )
                : ListView.builder(
                    controller: scroll,
                    itemCount: rows.length,
                    itemBuilder: (_, i) {
                      final v = rows[i];
                      return CheckboxListTile(
                        value: _selected.contains(v.vehicleId),
                        title: Text(
                          v.vehicleName,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                        subtitle: Text(
                          v.registrationNumber,
                          style: TextStyle(color: p.textSecondary),
                        ),
                        onChanged: (on) => setState(() => on == true
                            ? _selected.add(v.vehicleId)
                            : _selected.remove(v.vehicleId)),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }
}
