import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fleet/domain/fleet_models.dart';
import 'package:sheikh_go_driver/features/fleet/presentation/playback/playback_controller.dart';
import 'package:sheikh_go_driver/features/fleet/presentation/playback/playback_helpers.dart';

HistoryReplayPoint _p(
  String iso, {
  double lat = 0,
  double lng = 0,
  double speed = 0,
  double? heading,
}) {
  return HistoryReplayPoint(
    timestamp: DateTime.parse(iso),
    latitude: lat,
    longitude: lng,
    speedKmh: speed,
    heading: heading,
  );
}

void main() {
  test('effectivePlaybackPoints prefers dense route when playback is sparse', () {
    final sparsePlayback = [
      _p('2026-07-22T10:00:00Z', lat: 24.86, lng: 67.00),
      _p('2026-07-22T10:30:00Z', lat: 24.87, lng: 67.01),
    ];
    final denseRoute = [
      for (var i = 0; i < 10; i++)
        _p(
          '2026-07-22T10:${i.toString().padLeft(2, '0')}:00Z',
          lat: 24.86 + i * 0.001,
          lng: 67.00 + i * 0.001,
        ),
    ];
    final bundle = HistoryReplayBundle(
      route: denseRoute,
      playback: sparsePlayback,
    );
    final points = effectivePlaybackPoints(bundle);
    expect(points, same(denseRoute));
    expect(points, hasLength(10));
  });

  test('effectivePlaybackPoints keeps playback when comparable density', () {
    final playback = [
      _p('2026-07-22T10:00:00Z'),
      _p('2026-07-22T10:05:00Z'),
      _p('2026-07-22T10:10:00Z'),
    ];
    final route = [
      _p('2026-07-22T10:00:00Z'),
      _p('2026-07-22T10:05:00Z'),
      _p('2026-07-22T10:10:00Z'),
      _p('2026-07-22T10:15:00Z'),
    ];
    final bundle = HistoryReplayBundle(route: route, playback: playback);
    // route.length (4) < playback.length * 2 (6) → keep playback
    expect(effectivePlaybackPoints(bundle), same(playback));
  });

  test('effectivePlaybackPoints falls back to route when playback empty', () {
    final route = [
      _p('2026-07-22T10:00:00Z'),
      _p('2026-07-22T10:05:00Z'),
    ];
    final bundle = HistoryReplayBundle(route: route, playback: const []);
    expect(effectivePlaybackPoints(bundle), same(route));
  });

  testWidgets('play returns false with fewer than 2 points', (tester) async {
    late PlaybackController controller;
    await tester.pumpWidget(
      _PlaybackHarness(
        onReady: (c) => controller = c,
      ),
    );

    controller.setPoints(const []);
    expect(controller.play(), isFalse);
    expect(controller.playing, isFalse);

    controller.setPoints([_p('2026-07-22T10:00:00Z')]);
    expect(controller.play(), isFalse);
    expect(controller.playing, isFalse);
  });

  testWidgets('play advances virtualTime and displayPosition', (tester) async {
    late PlaybackController controller;
    await tester.pumpWidget(
      _PlaybackHarness(
        onReady: (c) => controller = c,
      ),
    );

    controller.setPoints([
      _p('2026-07-22T10:00:00Z', lat: 24.0, lng: 67.0, heading: 0),
      _p('2026-07-22T10:01:00Z', lat: 24.1, lng: 67.1, heading: 90),
    ]);

    expect(controller.play(), isTrue);
    expect(controller.playing, isTrue);

    // First tick only seeds _lastElapsed; second tick advances virtual time.
    await tester.pump(const Duration(milliseconds: 16));
    await tester.pump(const Duration(milliseconds: 500));

    expect(controller.virtualTime, isNotNull);
    expect(
      controller.virtualTime!.isAfter(DateTime.parse('2026-07-22T10:00:00Z')),
      isTrue,
    );
    expect(controller.displayPosition, isNotNull);
    expect(controller.displayPosition!.latitude, greaterThan(24.0));
    expect(controller.displayPosition!.longitude, greaterThan(67.0));

    controller.pause();
    expect(controller.playing, isFalse);
  });

  testWidgets('seekToTimestamp moves marker between points', (tester) async {
    late PlaybackController controller;
    await tester.pumpWidget(
      _PlaybackHarness(
        onReady: (c) => controller = c,
      ),
    );

    controller.setPoints([
      _p('2026-07-22T10:00:00Z', lat: 0, lng: 0),
      _p('2026-07-22T10:10:00Z', lat: 10, lng: 10),
    ]);
    controller.seekToTimestamp(DateTime.parse('2026-07-22T10:05:00Z'));

    expect(controller.displayPosition!.latitude, closeTo(5, 0.001));
    expect(controller.displayPosition!.longitude, closeTo(5, 0.001));
    expect(controller.index, 0);
  });
}

class _PlaybackHarness extends StatefulWidget {
  const _PlaybackHarness({required this.onReady});

  final ValueChanged<PlaybackController> onReady;

  @override
  State<_PlaybackHarness> createState() => _PlaybackHarnessState();
}

class _PlaybackHarnessState extends State<_PlaybackHarness>
    with SingleTickerProviderStateMixin {
  late final PlaybackController _controller;

  @override
  void initState() {
    super.initState();
    _controller = PlaybackController(vsync: this);
    widget.onReady(_controller);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => const SizedBox.shrink();
}
