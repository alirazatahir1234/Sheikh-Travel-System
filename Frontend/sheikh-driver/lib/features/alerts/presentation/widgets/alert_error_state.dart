import 'package:flutter/material.dart';

import '../../../../core/errors/error_handler.dart';
import 'fleet_alerts_colors.dart';

class AlertErrorState extends StatelessWidget {
  const AlertErrorState({
    super.key,
    required this.onRetry,
    this.retrying = false,
    this.error,
  });

  final VoidCallback onRetry;
  final bool retrying;
  final Object? error;

  @override
  Widget build(BuildContext context) {
    final kind = ErrorHandler.classify(error);
    final (title, subtitle) = switch (kind) {
      ApiErrorKind.unauthorized => (
          'Session Expired',
          ErrorHandler.message(error!),
        ),
      ApiErrorKind.forbidden => (
          'Access Restricted',
          ErrorHandler.message(error!),
        ),
      _ => (
          "Couldn't Load Alerts",
          "We couldn't fetch the latest alerts. Please check your "
              'internet connection and try again.',
        ),
    };

    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(32, 24, 32, 40),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const _ErrorCloud(),
            const SizedBox(height: 22),
            Text(
              title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.w800,
                color: FleetAlertsColors.navy,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              subtitle,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 14,
                height: 1.45,
                color: FleetAlertsColors.secondary,
              ),
            ),
            const SizedBox(height: 24),
            SizedBox(
              width: double.infinity,
              child: FilledButton.icon(
                onPressed: retrying ? null : onRetry,
                icon: retrying
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Icon(Icons.refresh_rounded),
                label: Text(retrying ? 'Retrying…' : 'Retry'),
                style: FilledButton.styleFrom(
                  backgroundColor: FleetAlertsColors.teal,
                  foregroundColor: Colors.white,
                  disabledBackgroundColor:
                      FleetAlertsColors.teal.withValues(alpha: 0.7),
                  padding: const EdgeInsets.symmetric(vertical: 15),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(14),
                  ),
                  textStyle: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Soft cloud with red no-wifi + notification dot (Image A error).
class _ErrorCloud extends StatelessWidget {
  const _ErrorCloud();

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 140,
      height: 110,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Positioned(
            top: 10,
            child: Container(
              width: 124,
              height: 76,
              decoration: BoxDecoration(
                color: const Color(0xFFE8EEF2),
                borderRadius: BorderRadius.circular(42),
              ),
            ),
          ),
          const Positioned(
            top: 28,
            child: Icon(
              Icons.wifi_off_rounded,
              size: 42,
              color: FleetAlertsColors.critical,
            ),
          ),
          Positioned(
            top: 18,
            right: 28,
            child: Container(
              width: 14,
              height: 14,
              decoration: BoxDecoration(
                color: FleetAlertsColors.critical,
                shape: BoxShape.circle,
                border: Border.all(color: Colors.white, width: 2),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
