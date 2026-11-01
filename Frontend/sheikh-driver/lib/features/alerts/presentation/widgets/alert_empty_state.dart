import 'package:flutter/material.dart';

import '../../domain/alert_display.dart';
import 'fleet_alerts_colors.dart';

enum AlertEmptyKind { allClear, filtered, resolvedEmpty }

class AlertEmptyState extends StatelessWidget {
  const AlertEmptyState({
    super.key,
    required this.kind,
    this.selectedFilters = const {},
    this.onClearFilters,
  });

  final AlertEmptyKind kind;
  final Set<AlertDisplaySeverity> selectedFilters;
  final VoidCallback? onClearFilters;

  @override
  Widget build(BuildContext context) {
    final copy = _copy();
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(32, 16, 32, 40),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            _Illustration(kind: kind),
            const SizedBox(height: 22),
            Text(
              copy.title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.w800,
                color: FleetAlertsColors.navy,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              copy.body,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 14,
                height: 1.45,
                color: FleetAlertsColors.secondary,
              ),
            ),
            if (kind == AlertEmptyKind.filtered && onClearFilters != null) ...[
              const SizedBox(height: 18),
              OutlinedButton(
                onPressed: onClearFilters,
                style: OutlinedButton.styleFrom(
                  foregroundColor: FleetAlertsColors.teal,
                  side: const BorderSide(color: FleetAlertsColors.teal),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  padding: const EdgeInsets.symmetric(
                    horizontal: 20,
                    vertical: 12,
                  ),
                ),
                child: const Text(
                  'Clear Filters',
                  style: TextStyle(fontWeight: FontWeight.w700),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  ({String title, String body}) _copy() {
    switch (kind) {
      case AlertEmptyKind.allClear:
        return (
          title: 'All Clear!',
          body: 'No active fleet alerts at the moment.',
        );
      case AlertEmptyKind.resolvedEmpty:
        return (
          title: 'No resolved alerts',
          body: 'Resolved fleet alerts will appear here.',
        );
      case AlertEmptyKind.filtered:
        if (selectedFilters.length == 1) {
          final sev = selectedFilters.first;
          final name = switch (sev) {
            AlertDisplaySeverity.critical => 'Critical',
            AlertDisplaySeverity.warning => 'Warning',
            AlertDisplaySeverity.info => 'Info',
            AlertDisplaySeverity.resolved => 'Resolved',
          };
          return (
            title: 'No $name Alerts',
            body: 'There are no active $name alerts for your fleet right now.',
          );
        }
        return (
          title: 'No matching alerts',
          body: 'There are no alerts matching your selected filters.',
        );
    }
  }
}

class _Illustration extends StatelessWidget {
  const _Illustration({required this.kind});

  final AlertEmptyKind kind;

  @override
  Widget build(BuildContext context) {
    switch (kind) {
      case AlertEmptyKind.filtered:
        return const _SearchCloud();
      case AlertEmptyKind.allClear:
        return const _ShieldClear();
      case AlertEmptyKind.resolvedEmpty:
        return const _ShieldClear(resolved: true);
    }
  }
}

/// Magnifying glass in soft cloud (Image A filtered empty).
class _SearchCloud extends StatelessWidget {
  const _SearchCloud();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 140,
      height: 120,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Positioned(
            top: 8,
            child: Container(
              width: 118,
              height: 72,
              decoration: BoxDecoration(
                color: const Color(0xFFE8EEF2),
                borderRadius: BorderRadius.circular(40),
              ),
            ),
          ),
          Positioned(
            top: 28,
            left: 28,
            child: Container(
              width: 48,
              height: 48,
              decoration: BoxDecoration(
                color: Colors.white,
                shape: BoxShape.circle,
                border: Border.all(
                  color: FleetAlertsColors.secondary.withValues(alpha: 0.45),
                  width: 3,
                ),
              ),
            ),
          ),
          Positioned(
            top: 62,
            left: 62,
            child: Transform.rotate(
              angle: 0.7,
              child: Container(
                width: 6,
                height: 28,
                decoration: BoxDecoration(
                  color: FleetAlertsColors.secondary.withValues(alpha: 0.55),
                  borderRadius: BorderRadius.circular(4),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Teal shield + check with soft truck landscape (Image A All Clear).
class _ShieldClear extends StatelessWidget {
  const _ShieldClear({this.resolved = false});

  final bool resolved;

  @override
  Widget build(BuildContext context) {
    final accent = resolved ? FleetAlertsColors.info : FleetAlertsColors.teal;
    return SizedBox(
      width: 168,
      height: 150,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Positioned(
            top: 4,
            child: Container(
              width: 130,
              height: 78,
              decoration: BoxDecoration(
                color: const Color(0xFFE8EEF2),
                borderRadius: BorderRadius.circular(44),
              ),
            ),
          ),
          Positioned(
            top: 18,
            child: Container(
              width: 64,
              height: 72,
              decoration: BoxDecoration(
                color: accent,
                borderRadius: const BorderRadius.only(
                  topLeft: Radius.circular(14),
                  topRight: Radius.circular(14),
                  bottomLeft: Radius.circular(28),
                  bottomRight: Radius.circular(28),
                ),
                boxShadow: [
                  BoxShadow(
                    color: accent.withValues(alpha: 0.28),
                    blurRadius: 12,
                    offset: const Offset(0, 4),
                  ),
                ],
              ),
              child: const Icon(
                Icons.check_rounded,
                color: Colors.white,
                size: 34,
              ),
            ),
          ),
          Positioned(
            bottom: 0,
            left: 8,
            right: 8,
            child: SizedBox(
              height: 52,
              child: CustomPaint(
                painter: _SoftLandscapePainter(
                  accent: accent.withValues(alpha: 0.75),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _SoftLandscapePainter extends CustomPainter {
  _SoftLandscapePainter({required this.accent});

  final Color accent;

  @override
  void paint(Canvas canvas, Size size) {
    final ground = Paint()..color = const Color(0xFFE8EEF2);
    final path = Path()
      ..moveTo(0, size.height * 0.55)
      ..quadraticBezierTo(
        size.width * 0.5,
        size.height * 0.15,
        size.width,
        size.height * 0.55,
      )
      ..lineTo(size.width, size.height)
      ..lineTo(0, size.height)
      ..close();
    canvas.drawPath(path, ground);

    final tree = Paint()..color = accent;
    void drawTree(double x, double h) {
      final top = size.height - h - 10;
      canvas.drawPath(
        Path()
          ..moveTo(x, top)
          ..lineTo(x + 7, top + h * 0.55)
          ..lineTo(x - 7, top + h * 0.55)
          ..close(),
        tree,
      );
      canvas.drawRect(
        Rect.fromLTWH(x - 1.5, top + h * 0.5, 3, h * 0.35),
        tree,
      );
    }

    drawTree(size.width * 0.18, 22);
    drawTree(size.width * 0.78, 18);

    final truck = Paint()..color = accent;
    final ty = size.height * 0.52;
    final tx = size.width * 0.42;
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromLTWH(tx, ty, 36, 14),
        const Radius.circular(3),
      ),
      truck,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromLTWH(tx + 28, ty + 4, 14, 10),
        const Radius.circular(2),
      ),
      truck,
    );
    canvas.drawCircle(Offset(tx + 8, ty + 16), 3.5, truck);
    canvas.drawCircle(Offset(tx + 30, ty + 16), 3.5, truck);
  }

  @override
  bool shouldRepaint(covariant _SoftLandscapePainter oldDelegate) =>
      oldDelegate.accent != accent;
}
