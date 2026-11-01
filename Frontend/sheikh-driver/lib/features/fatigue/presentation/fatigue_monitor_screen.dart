import 'dart:async';
import 'package:camera/camera.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:google_mlkit_face_detection/google_mlkit_face_detection.dart';
import '../domain/fatigue_ear.dart';
import 'fatigue_response_screen.dart';

/// On-drive prototype: front camera + ML Kit contours → EAR → ingest API.
class FatigueMonitorScreen extends ConsumerStatefulWidget {
  const FatigueMonitorScreen({super.key});

  @override
  ConsumerState<FatigueMonitorScreen> createState() =>
      _FatigueMonitorScreenState();
}

class _FatigueMonitorScreenState extends ConsumerState<FatigueMonitorScreen> {
  CameraController? _camera;
  FaceDetector? _detector;
  bool _running = false;
  int _drowsyFrames = 0;
  DateTime _lastSignal = DateTime.fromMillisecondsSinceEpoch(0);
  String _status = 'Initializing…';

  @override
  void initState() {
    super.initState();
    if (kIsWeb) {
      _status = 'Camera prototype runs on iOS/Android builds.';
    } else {
      _initCamera();
    }
  }

  Future<void> _initCamera() async {
    try {
      final cameras = await availableCameras();
      final front = cameras.firstWhere(
        (c) => c.lensDirection == CameraLensDirection.front,
        orElse: () => cameras.first,
      );
      _camera =
          CameraController(front, ResolutionPreset.medium, enableAudio: false);
      await _camera!.initialize();
      _detector = FaceDetector(
        options: FaceDetectorOptions(
          enableContours: true,
          performanceMode: FaceDetectorMode.fast,
        ),
      );
      setState(() => _status = 'Monitoring (prototype)');
      unawaited(_loop());
    } catch (e) {
      setState(() => _status = 'Camera unavailable: $e');
    }
  }

  Future<void> _loop() async {
    if (_running) return;
    _running = true;
    while (mounted && _camera != null && _camera!.value.isInitialized) {
      try {
        await _processFrame();
      } catch (_) {}
      await Future<void>.delayed(const Duration(milliseconds: 350));
    }
    _running = false;
  }

  Future<void> _processFrame() async {
    final file = await _camera!.takePicture();
    final input = InputImage.fromFilePath(file.path);
    final faces = await _detector!.processImage(input);
    if (faces.isEmpty) {
      _drowsyFrames = 0;
      return;
    }
    final face = faces.first;
    final left = face.contours[FaceContourType.leftEye]?.points ?? [];
    final right = face.contours[FaceContourType.rightEye]?.points ?? [];
    if (left.length < 6 || right.length < 6) return;

    Offset map(dynamic p) => Offset(p.x.toDouble(), p.y.toDouble());
    final ear = combineEar(
      computeEar(left.map(map).toList()),
      computeEar(right.map(map).toList()),
    );
    if (isDrowsyEar(ear)) {
      _drowsyFrames++;
    } else {
      _drowsyFrames = 0;
    }
    if (_drowsyFrames >= 6 &&
        DateTime.now().difference(_lastSignal) > const Duration(seconds: 20)) {
      _lastSignal = DateTime.now();
      _drowsyFrames = 0;
      Position? pos;
      try {
        pos = await Geolocator.getCurrentPosition();
      } catch (_) {}
      await ref.read(fatigueApiProvider).submitCameraSignal(
            signalType: 'drowsy_eyes',
            confidence: clampConfidenceFromEar(ear),
            earScore: ear,
            expressionHint: 'low_ear_mobile',
            latitude: pos?.latitude,
            longitude: pos?.longitude,
          );
      if (mounted) {
        await Navigator.of(context).push<bool>(
          MaterialPageRoute(builder: (_) => const FatigueResponseScreen()),
        );
      }
    }
    if (mounted) {
      setState(() => _status = 'EAR ${ear.toStringAsFixed(3)} (prototype)');
    }
  }

  @override
  void dispose() {
    _detector?.close();
    _camera?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final preview = _camera != null && _camera!.value.isInitialized
        ? CameraPreview(_camera!)
        : const SizedBox.shrink();
    return Scaffold(
      appBar: AppBar(title: const Text('Fatigue monitor')),
      body: Column(
        children: [
          Expanded(child: preview),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Text(_status, textAlign: TextAlign.center),
          ),
        ],
      ),
    );
  }
}
