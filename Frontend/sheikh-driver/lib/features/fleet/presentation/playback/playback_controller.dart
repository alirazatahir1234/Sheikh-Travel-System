import 'dart:developer' as developer;

import 'package:flutter/scheduler.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../domain/fleet_models.dart';
import 'playback_helpers.dart';

class PlaybackController {
  PlaybackController({required TickerProvider vsync}) {
    _ticker = vsync.createTicker(_onTick);
  }

  late final Ticker _ticker;
  List<HistoryReplayPoint> _points = const [];
  bool _playing = false;
  double _speed = 1;
  int _index = 0;
  DateTime? _virtualTime;
  Duration? _lastElapsed;
  LatLng? _displayPosition;
  double? _displayHeading;

  bool get playing => _playing;
  double get speed => _speed;
  int get index => _index;
  DateTime? get virtualTime => _virtualTime;
  LatLng? get displayPosition => _displayPosition;
  double? get displayHeading => _displayHeading;
  List<HistoryReplayPoint> get points => _points;

  void Function()? onChanged;
  void Function()? onFinished;

  void setPoints(List<HistoryReplayPoint> points) {
    _points = points;
    _index = 0;
    _virtualTime = points.isEmpty ? null : points.first.timestamp;
    _displayPosition = points.isEmpty
        ? null
        : LatLng(points.first.latitude, points.first.longitude);
    _displayHeading = points.isEmpty ? null : points.first.heading;
    _emitChanged();
  }

  void setSpeed(double speed) {
    _speed = speed;
    developer.log('playback_speed_change', name: 'playback', error: '$speed');
    _emitChanged();
  }

  void togglePlayPause() {
    if (_playing) {
      pause();
      return;
    }
    play();
  }

  /// Starts the ticker. Returns false when there are fewer than 2 points.
  bool play() {
    if (_points.length < 2) {
      developer.log(
        'playback_start_rejected',
        name: 'playback',
        error: 'need_at_least_2_points',
      );
      return false;
    }
    if (_index >= _points.length - 1) {
      seekToIndex(0);
    }
    _playing = true;
    _lastElapsed = null;
    _ticker.start();
    developer.log('playback_start', name: 'playback');
    _emitChanged();
    return true;
  }

  void pause() {
    _playing = false;
    _ticker.stop();
    _lastElapsed = null;
    developer.log('playback_pause', name: 'playback');
    _snapToNearestPoint();
    _emitChanged();
  }

  void stop() {
    _playing = false;
    _ticker.stop();
    _lastElapsed = null;
    developer.log('playback_stop', name: 'playback');
    seekToIndex(0);
  }

  void seekToIndex(int index) {
    if (_points.isEmpty) return;
    _index = index.clamp(0, _points.length - 1);
    final p = _points[_index];
    _virtualTime = p.timestamp;
    _displayPosition = LatLng(p.latitude, p.longitude);
    _displayHeading = p.heading;
    _emitChanged();
  }

  void seekToTimestamp(DateTime time) {
    if (_points.isEmpty) return;
    _virtualTime = time;
    _applyVirtualTimeState();
    _emitChanged();
  }

  void dispose() {
    _ticker.dispose();
  }

  void _onTick(Duration elapsed) {
    if (!_playing || _points.length < 2) return;
    final prevElapsed = _lastElapsed;
    _lastElapsed = elapsed;
    if (prevElapsed == null) return;

    final delta = elapsed - prevElapsed;
    if (delta <= Duration.zero) return;
    final baseTime = _virtualTime ?? _points[_index].timestamp;
    final advanced = baseTime.add(
      Duration(
        microseconds: (delta.inMicroseconds * _speed).round(),
      ),
    );
    _virtualTime = advanced;
    _applyVirtualTimeState();
    _emitChanged();
  }

  void _applyVirtualTimeState() {
    if (_points.isEmpty || _virtualTime == null) return;
    final first = _points.first.timestamp;
    final last = _points.last.timestamp;
    if (_virtualTime!.isBefore(first)) {
      _virtualTime = first;
      _index = 0;
      _displayPosition = LatLng(_points.first.latitude, _points.first.longitude);
      _displayHeading = _points.first.heading;
      return;
    }
    if (_virtualTime!.isAtSameMomentAs(last) || _virtualTime!.isAfter(last)) {
      _virtualTime = last;
      _index = _points.length - 1;
      _displayPosition = LatLng(_points.last.latitude, _points.last.longitude);
      _displayHeading = _points.last.heading;
      _playing = false;
      _ticker.stop();
      _lastElapsed = null;
      onFinished?.call();
      return;
    }

    final right = lowerBoundPlaybackIndex(_points, _virtualTime!);
    final left = (right - 1).clamp(0, _points.length - 1);
    final from = _points[left];
    final to = _points[right];
    final totalUs = to.timestamp.difference(from.timestamp).inMicroseconds;
    final partUs = _virtualTime!.difference(from.timestamp).inMicroseconds;
    final t = totalUs <= 0 ? 0.0 : (partUs / totalUs).clamp(0.0, 1.0);

    final lat = from.latitude + (to.latitude - from.latitude) * t;
    final lng = from.longitude + (to.longitude - from.longitude) * t;
    final heading = _lerpHeading(from.heading ?? 0, to.heading ?? from.heading ?? 0, t);

    _index = left;
    _displayPosition = LatLng(lat, lng);
    _displayHeading = heading;
  }

  double _lerpHeading(double from, double to, double t) {
    var delta = to - from;
    while (delta > 180) {
      delta -= 360;
    }
    while (delta < -180) {
      delta += 360;
    }
    return from + delta * t;
  }

  void _snapToNearestPoint() {
    if (_points.isEmpty || _virtualTime == null) return;
    _index = indexForTimestamp(_points, _virtualTime!);
    final p = _points[_index];
    _virtualTime = p.timestamp;
    _displayPosition = LatLng(p.latitude, p.longitude);
    _displayHeading = p.heading;
  }

  void _emitChanged() {
    onChanged?.call();
  }
}
