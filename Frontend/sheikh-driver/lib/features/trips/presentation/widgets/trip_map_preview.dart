import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import '../../../../core/api/dio_client.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../navigation/utils/gps_geo.dart';
import '../../data/trip_route_loader.dart';
import '../../domain/trip_driving_route.dart';
import '../../domain/trip_model.dart';

/// Fixed-height route preview on Trip Details only.
///
/// Android and iOS render tiles with the native Maps SDK key
/// (`com.google.android.geo.API_KEY` / `GMSApiKey`) from
/// `android/maps.properties` and `ios/Flutter/Maps.xcconfig`.
///
/// The road path comes from one `GET /gps/maps/planned-route` call when this
/// card is shown (memory + 6h local cache). It is not refreshed on GPS ticks.
/// Do not mount this card on the dashboard or any polling surface.
/// If the route call fails, the straight pickup→drop line stays.
class TripMapPreviewCard extends StatefulWidget {
  const TripMapPreviewCard({
    super.key,
    required this.trip,
    this.onTap,
    this.height = 200,
  });

  final Trip trip;
  final VoidCallback? onTap;
  final double height;

  @override
  State<TripMapPreviewCard> createState() => _TripMapPreviewCardState();
}

class _TripMapPreviewCardState extends State<TripMapPreviewCard> {
  GoogleMapController? _map;
  bool _fitted = false;
  TripDrivingRoute? _route;
  String? _routeKey;
  bool _routeStarted = false;

  bool get _canShowMap => tripMapPreviewShowsMap(widget.trip);

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _loadRouteOnce();
  }

  @override
  void didUpdateWidget(covariant TripMapPreviewCard oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.trip.pickupLatitude != widget.trip.pickupLatitude ||
        oldWidget.trip.pickupLongitude != widget.trip.pickupLongitude ||
        oldWidget.trip.dropLatitude != widget.trip.dropLatitude ||
        oldWidget.trip.dropLongitude != widget.trip.dropLongitude) {
      _fitted = false;
      _route = null;
      _routeKey = null;
      _routeStarted = false;
      _loadRouteOnce();
      _fit();
    }
  }

  void _loadRouteOnce() {
    final trip = widget.trip;
    if (!trip.hasPickupCoords || !trip.hasDropCoords || _routeStarted) return;
    final key = tripRouteCacheKey(
      trip.pickupLatitude!,
      trip.pickupLongitude!,
      trip.dropLatitude!,
      trip.dropLongitude!,
    );
    if (_routeKey == key && _route != null) return;
    _routeKey = key;
    final cached = TripRouteLoader.memoryHit(key);
    if (cached != null) {
      _route = cached;
      _routeStarted = true;
      _fitted = false;
      return;
    }
    _routeStarted = true;
    final dio = ProviderScope.containerOf(context).read(dioProvider);
    TripRouteLoader(dio)
        .load(
      originLat: trip.pickupLatitude!,
      originLng: trip.pickupLongitude!,
      destLat: trip.dropLatitude!,
      destLng: trip.dropLongitude!,
    )
        .then((route) {
      if (!mounted || route == null || _routeKey != key) return;
      setState(() {
        _route = route;
        _fitted = false;
      });
      _fit();
    });
  }

  Set<Marker> _markers() {
    final trip = widget.trip;
    final markers = <Marker>{};
    if (trip.hasPickupCoords) {
      markers.add(
        Marker(
          markerId: const MarkerId('pickup'),
          position: LatLng(trip.pickupLatitude!, trip.pickupLongitude!),
          icon: BitmapDescriptor.defaultMarkerWithHue(
            BitmapDescriptor.hueGreen,
          ),
          infoWindow: InfoWindow(
            title: 'Pickup',
            snippet: trip.pickupAddress ?? trip.routeName,
          ),
        ),
      );
    }
    if (trip.hasDropCoords) {
      markers.add(
        Marker(
          markerId: const MarkerId('dropoff'),
          position: LatLng(trip.dropLatitude!, trip.dropLongitude!),
          icon: BitmapDescriptor.defaultMarkerWithHue(
            BitmapDescriptor.hueRed,
          ),
          infoWindow: InfoWindow(
            title: 'Drop-off',
            snippet: trip.dropoffAddress ?? '',
          ),
        ),
      );
    }
    return markers;
  }

  List<LatLng> _linePoints() {
    final road = _route?.points;
    if (road != null && road.length >= 2) {
      return [
        for (final p in road) LatLng(p.latitude, p.longitude),
      ];
    }
    final trip = widget.trip;
    if (!trip.hasPickupCoords || !trip.hasDropCoords) return const [];
    return [
      LatLng(trip.pickupLatitude!, trip.pickupLongitude!),
      LatLng(trip.dropLatitude!, trip.dropLongitude!),
    ];
  }

  Set<Polyline> _polylines() {
    final points = _linePoints();
    if (points.length < 2) return {};
    return {
      Polyline(
        polylineId: const PolylineId('route'),
        points: points,
        color: const Color(GpsStatusColors.route),
        width: 5,
        geodesic: false,
      ),
    };
  }

  Future<void> _fit() async {
    final map = _map;
    if (map == null || _fitted) return;
    final pts = _linePoints().isNotEmpty
        ? _linePoints()
        : tripMapPreviewPoints(widget.trip);
    if (pts.isEmpty) return;
    try {
      if (pts.length == 1 || _boundsAreDegenerate(pts)) {
        await map.animateCamera(CameraUpdate.newLatLngZoom(pts.first, 13.5));
      } else {
        var minLat = pts.first.latitude;
        var maxLat = pts.first.latitude;
        var minLng = pts.first.longitude;
        var maxLng = pts.first.longitude;
        for (final p in pts.skip(1)) {
          minLat = minLat < p.latitude ? minLat : p.latitude;
          maxLat = maxLat > p.latitude ? maxLat : p.latitude;
          minLng = minLng < p.longitude ? minLng : p.longitude;
          maxLng = maxLng > p.longitude ? maxLng : p.longitude;
        }
        await map.animateCamera(
          CameraUpdate.newLatLngBounds(
            LatLngBounds(
              southwest: LatLng(minLat, minLng),
              northeast: LatLng(maxLat, maxLng),
            ),
            48,
          ),
        );
      }
      _fitted = true;
    } catch (_) {
      _fitted = false;
    }
  }

  bool _boundsAreDegenerate(List<LatLng> pts) {
    var minLat = pts.first.latitude;
    var maxLat = pts.first.latitude;
    var minLng = pts.first.longitude;
    var maxLng = pts.first.longitude;
    for (final p in pts.skip(1)) {
      minLat = minLat < p.latitude ? minLat : p.latitude;
      maxLat = maxLat > p.latitude ? maxLat : p.latitude;
      minLng = minLng < p.longitude ? minLng : p.longitude;
      maxLng = maxLng > p.longitude ? maxLng : p.longitude;
    }
    return (maxLat - minLat).abs() < 0.0001 && (maxLng - minLng).abs() < 0.0001;
  }

  @override
  Widget build(BuildContext context) {
    final trip = widget.trip;
    final eta = _route?.summary ?? tripMapPreviewEtaLabel(trip);
    // GoogleMap must not sit under Material/InkWell — that compositing path
    // paints a blank white platform view inside a ListView on Android.
    return DecoratedBox(
      decoration: BoxDecoration(
        color: context.sg.card,
        borderRadius: BorderRadius.circular(AppRadii.lg),
        border: Border.all(color: AppColors.primary.withValues(alpha: 0.45)),
        boxShadow: AppShadows.card,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SizedBox(
            key: const Key('trip-map-preview'),
            height: widget.height,
            child: _canShowMap
                ? _MapCanvas(
                    trip: trip,
                    markers: _markers(),
                    polylines: _polylines(),
                    onMapCreated: (c) {
                      _map = c;
                      _fit();
                    },
                  )
                : _MapUnavailablePlaceholder(
                    canNavigate: trip.canNavigate,
                    onTap: widget.onTap,
                  ),
          ),
          _RouteCaption(
            pickup: trip.pickupAddress,
            dropoff: trip.dropoffAddress,
            eta: eta,
            onTap: widget.onTap,
          ),
        ],
      ),
    );
  }
}

class _MapCanvas extends StatelessWidget {
  const _MapCanvas({
    required this.trip,
    required this.markers,
    required this.polylines,
    required this.onMapCreated,
  });

  final Trip trip;
  final Set<Marker> markers;
  final Set<Polyline> polylines;
  final void Function(GoogleMapController) onMapCreated;

  @override
  Widget build(BuildContext context) {
    return Stack(
      fit: StackFit.expand,
      children: [
        const ColoredBox(color: Color(0xFFE7F3EF)),
        GoogleMap(
          initialCameraPosition: tripMapPreviewCamera(trip),
          markers: markers,
          polylines: polylines,
          myLocationButtonEnabled: false,
          myLocationEnabled: false,
          zoomControlsEnabled: false,
          mapToolbarEnabled: false,
          compassEnabled: false,
          liteModeEnabled: false,
          onMapCreated: onMapCreated,
        ),
      ],
    );
  }
}

class _RouteCaption extends StatelessWidget {
  const _RouteCaption({
    required this.pickup,
    required this.dropoff,
    required this.eta,
    this.onTap,
  });

  final String? pickup;
  final String? dropoff;
  final String? eta;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final from = (pickup ?? '').trim();
    final to = (dropoff ?? '').trim();
    final route = from.isEmpty && to.isEmpty
        ? 'Route'
        : '${from.isEmpty ? 'Pickup' : from} → ${to.isEmpty ? 'Drop-off' : to}';
    return GestureDetector(
      onTap: onTap,
      behavior: HitTestBehavior.opaque,
      child: Container(
        color: const Color(0xFFE7F3EF),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        child: Row(
          children: [
            const Icon(Icons.route_rounded, size: 16, color: AppColors.primary),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                route,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: p.textPrimary,
                  fontWeight: FontWeight.w700,
                  fontSize: 13,
                ),
              ),
            ),
            if (eta != null) ...[
              const SizedBox(width: 8),
              Text(
                eta!,
                style: const TextStyle(
                  color: AppColors.primaryDark,
                  fontWeight: FontWeight.w700,
                  fontSize: 12,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _MapUnavailablePlaceholder extends StatelessWidget {
  const _MapUnavailablePlaceholder({
    required this.canNavigate,
    this.onTap,
  });

  final bool canNavigate;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final subtitle = canNavigate
        ? 'Navigate still works from the address.'
        : 'This trip has no pickup or drop-off coordinates.';
    return GestureDetector(
      onTap: onTap,
      child: ColoredBox(
        key: const Key('trip-map-placeholder'),
        color: const Color(0xFFE7F3EF),
        child: Center(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(
                  Icons.map_outlined,
                  size: 40,
                  color: AppColors.primary,
                ),
                const SizedBox(height: 8),
                const Text(
                  'Map needs a location',
                  style: TextStyle(
                    color: AppColors.primaryDark,
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  subtitle,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: Color(0xFF3D5C54),
                    fontSize: 13,
                    height: 1.3,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Pickup / drop points already on the trip (no invented coordinates).
List<LatLng> tripMapPreviewPoints(Trip trip) {
  final pts = <LatLng>[];
  if (trip.hasPickupCoords) {
    pts.add(LatLng(trip.pickupLatitude!, trip.pickupLongitude!));
  }
  if (trip.hasDropCoords) {
    pts.add(LatLng(trip.dropLatitude!, trip.dropLongitude!));
  }
  return pts;
}

/// True when the preview should build a [GoogleMap].
///
/// Independent of `--dart-define=GOOGLE_MAPS_KEY`. The native SDK key is what
/// paints tiles. Without coordinates there is nothing to mark.
bool tripMapPreviewShowsMap(Trip trip) => tripMapPreviewPoints(trip).isNotEmpty;

/// Scheduled drop-off label only. Null when the API did not send a time.
String? tripMapPreviewEtaLabel(Trip trip) {
  if (trip.dropoffTime == null) return null;
  final label = trip.dropoffDateTimeLabel.trim();
  if (label.isEmpty || label == 'ETA pending') return null;
  return label;
}

/// Initial camera before bounds fit.
CameraPosition tripMapPreviewCamera(Trip trip) {
  final pts = tripMapPreviewPoints(trip);
  if (pts.isEmpty) {
    return const CameraPosition(
      target: LatLng(defaultMapLat, defaultMapLng),
      zoom: 5.5,
    );
  }
  if (pts.length == 1) {
    return CameraPosition(target: pts.first, zoom: 13.5);
  }
  final mid = LatLng(
    (pts.first.latitude + pts.last.latitude) / 2,
    (pts.first.longitude + pts.last.longitude) / 2,
  );
  return CameraPosition(target: mid, zoom: 7.5);
}
