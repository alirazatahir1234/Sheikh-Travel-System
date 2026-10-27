import 'dart:math' as math;
import 'package:flutter/material.dart';

/// Eye aspect ratio from six 2D points (p0–p5 clockwise around the eye).
double computeEar(List<Offset> points) {
  if (points.length < 6) return 1.0;
  double dist(Offset a, Offset b) => (a - b).distance;
  final vertical =
      (dist(points[1], points[5]) + dist(points[2], points[4])) / 2.0;
  final horizontal = dist(points[0], points[3]);
  return vertical / (horizontal + 1e-6);
}

bool isDrowsyEar(double ear, {double threshold = 0.21}) => ear < threshold;

double combineEar(double left, double right) => (left + right) / 2.0;

double clampConfidenceFromEar(double ear, {double threshold = 0.21}) {
  final deficit = (threshold - ear).clamp(0.0, threshold);
  return (0.5 + deficit / threshold * 0.5).clamp(0.0, 1.0);
}

/// Rough yawning proxy: mouth height vs width from landmarks.
double mouthOpenRatio(List<Offset> mouthContour) {
  if (mouthContour.length < 4) return 0;
  final xs = mouthContour.map((p) => p.dx);
  final ys = mouthContour.map((p) => p.dy);
  final width = (xs.reduce(math.max) - xs.reduce(math.min)).abs();
  final height = (ys.reduce(math.max) - ys.reduce(math.min)).abs();
  return height / (width + 1e-6);
}
