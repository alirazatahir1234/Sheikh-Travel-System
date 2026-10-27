import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/fatigue/domain/fatigue_ear.dart';

void main() {
  test('EAR detects closed eye geometry', () {
    final open = [
      const Offset(0, 0),
      const Offset(1, -0.1),
      const Offset(2, -0.1),
      const Offset(3, 0),
      const Offset(2, 0.1),
      const Offset(1, 0.1),
    ];
    final closed = [
      const Offset(0, 0),
      const Offset(1, -0.01),
      const Offset(2, -0.01),
      const Offset(3, 0),
      const Offset(2, 0.01),
      const Offset(1, 0.01),
    ];
    expect(computeEar(open), greaterThan(computeEar(closed)));
    expect(isDrowsyEar(computeEar(closed)), isTrue);
  });
}
