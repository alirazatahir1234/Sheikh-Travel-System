import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../core/constants/app_brand.dart';
import '../../core/constants/app_theme.dart';

enum SgLoaderSize { sm, md, lg }

/// Animated Nexus logo loader — matches ERP `sg-progress-loader` (arc + logo pulse).
class SgProgressLoader extends StatefulWidget {
  const SgProgressLoader({super.key, this.size = SgLoaderSize.md});

  final SgLoaderSize size;

  double get dimension => switch (size) {
        SgLoaderSize.sm => 48,
        SgLoaderSize.md => 72,
        SgLoaderSize.lg => 112,
      };

  @override
  State<SgProgressLoader> createState() => _SgProgressLoaderState();
}

class _SgProgressLoaderState extends State<SgProgressLoader>
    with TickerProviderStateMixin {
  late final AnimationController _spin;
  late final AnimationController _arcPulse;
  late final AnimationController _logoPulse;

  @override
  void initState() {
    super.initState();
    final reduceMotion =
        WidgetsBinding.instance.platformDispatcher.accessibilityFeatures.reduceMotion;

    _spin = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1150),
    );
    _arcPulse = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1150),
    );
    _logoPulse = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1850),
    );

    if (!reduceMotion) {
      _spin.repeat();
      _arcPulse.repeat(reverse: true);
      _logoPulse.repeat(reverse: true);
    } else {
      _spin.value = 0.25;
      _arcPulse.value = 0.5;
      _logoPulse.value = 1;
    }
  }

  @override
  void dispose() {
    _spin.dispose();
    _arcPulse.dispose();
    _logoPulse.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final size = widget.dimension;
    return SizedBox(
      width: size,
      height: size,
      child: AnimatedBuilder(
        animation: Listenable.merge([_spin, _arcPulse, _logoPulse]),
        builder: (context, _) {
          return CustomPaint(
            painter: _NexusLoaderPainter(
              spinTurns: _spin.value,
              arcT: _arcPulse.value,
            ),
            child: Center(
              child: Transform.scale(
                scale: 0.94 + 0.06 * _logoPulse.value,
                child: Opacity(
                  opacity: 0.88 + 0.12 * _logoPulse.value,
                  child: Image.asset(
                    AppBrand.logoAsset,
                    width: size * (56 / 120),
                    height: size * (54 / 120),
                    fit: BoxFit.contain,
                    errorBuilder: (_, __, ___) => Icon(
                      Icons.radar_rounded,
                      size: size * 0.38,
                      color: AppColors.primary,
                    ),
                  ),
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}

class _NexusLoaderPainter extends CustomPainter {
  _NexusLoaderPainter({
    required this.spinTurns,
    required this.arcT,
  });

  final double spinTurns;
  final double arcT;

  static const _teal = Color(0xFF0FAF9F);
  static const _tealLight = Color(0xFF5EEAD4);

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final radius = size.width * (46 / 120);
    final stroke = size.width * (7 / 120);

    final track = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..color = _teal.withValues(alpha: 0.18);
    canvas.drawCircle(center, radius, track);

    final innerRing = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1
      ..color = _teal.withValues(alpha: 0.12);
    canvas.drawCircle(center, size.width * (34 / 120), innerRing);

    final arcSweep = (0.24 + 0.10 * arcT) * 2 * math.pi;
    final arcOpacity = 0.92 + 0.08 * arcT;

    canvas.save();
    canvas.translate(center.dx, center.dy);
    canvas.rotate(spinTurns * 2 * math.pi - math.pi / 2);
    canvas.translate(-center.dx, -center.dy);

    final arcRect = Rect.fromCircle(center: center, radius: radius);
    final arcPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..strokeCap = StrokeCap.round
      ..shader = SweepGradient(
        colors: [
          _teal.withValues(alpha: 0.35 * arcOpacity),
          _teal.withValues(alpha: arcOpacity),
          _tealLight.withValues(alpha: arcOpacity),
        ],
        transform: GradientRotation(spinTurns * 2 * math.pi),
      ).createShader(arcRect);

    canvas.drawArc(arcRect, 0, arcSweep, false, arcPaint);
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _NexusLoaderPainter oldDelegate) {
    return oldDelegate.spinTurns != spinTurns || oldDelegate.arcT != arcT;
  }
}

/// Full-screen / inline branded loading indicator with optional caption.
class SgBrandLoader extends StatelessWidget {
  const SgBrandLoader({
    super.key,
    this.size = SgLoaderSize.md,
    this.message,
    this.padding = const EdgeInsets.symmetric(horizontal: 24, vertical: 48),
  });

  final SgLoaderSize size;
  final String? message;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final label = message ?? 'Loading ${AppBrand.productName}';

    return Semantics(
      label: label,
      child: Padding(
        padding: padding,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            SgProgressLoader(size: size),
            if (message != null) ...[
              const SizedBox(height: 14),
              Text(
                message!,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: p.textMuted,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
