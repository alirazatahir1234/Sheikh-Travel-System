import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';
import '../../domain/alert_display.dart';

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
        padding: const EdgeInsets.fromLTRB(32, 24, 32, 40),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            _Illustration(kind: kind),
            const SizedBox(height: 20),
            Text(
              copy.title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.w800,
                color: Color(0xFF0B253B),
              ),
            ),
            const SizedBox(height: 8),
            Text(
              copy.body,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 14,
                height: 1.4,
                color: AppColors.textSecondary,
              ),
            ),
            if (copy.support != null) ...[
              const SizedBox(height: 6),
              Text(
                copy.support!,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 13,
                  color: AppColors.textMuted,
                ),
              ),
            ],
            if (kind == AlertEmptyKind.filtered && onClearFilters != null) ...[
              const SizedBox(height: 18),
              OutlinedButton(
                onPressed: onClearFilters,
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColors.primary,
                  side: const BorderSide(color: AppColors.primary),
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

  ({String title, String body, String? support}) _copy() {
    switch (kind) {
      case AlertEmptyKind.allClear:
        return (
          title: 'All clear!',
          body: 'No active fleet alerts at the moment.',
          support: 'Your fleet is looking good.',
        );
      case AlertEmptyKind.resolvedEmpty:
        return (
          title: 'No resolved alerts',
          body: 'Resolved fleet alerts will appear here.',
          support: null,
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
            body:
                'There are no alerts matching your selected $name filter right now.',
            support: null,
          );
        }
        return (
          title: 'No matching alerts',
          body: 'There are no alerts matching your selected filters.',
          support: null,
        );
    }
  }
}

class _Illustration extends StatelessWidget {
  const _Illustration({required this.kind});

  final AlertEmptyKind kind;

  @override
  Widget build(BuildContext context) {
    if (kind == AlertEmptyKind.filtered) {
      return const SizedBox(
        width: 96,
        height: 96,
        child: DecoratedBox(
          decoration: BoxDecoration(
            color: Color(0xFFE8EEF2),
            shape: BoxShape.circle,
          ),
          child: Icon(
            Icons.search_off_rounded,
            size: 44,
            color: AppColors.textSecondary,
          ),
        ),
      );
    }

    final isResolved = kind == AlertEmptyKind.resolvedEmpty;
    return SizedBox(
      width: 140,
      height: 120,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Positioned(
            bottom: 8,
            left: 10,
            right: 10,
            child: Container(
              height: 18,
              decoration: BoxDecoration(
                color: const Color(0xFFD6E4EC),
                borderRadius: BorderRadius.circular(20),
              ),
            ),
          ),
          Icon(
            Icons.local_shipping_rounded,
            size: 64,
            color: AppColors.primary.withValues(alpha: 0.85),
          ),
          Positioned(
            top: 4,
            right: 18,
            child: Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(
                color: isResolved
                    ? const Color(0xFFE0F2FE)
                    : const Color(0xFFDCFCE7),
                shape: BoxShape.circle,
                border: Border.all(
                  color: isResolved
                      ? const Color(0xFF3B82F6)
                      : AppColors.success,
                  width: 2,
                ),
              ),
              child: Icon(
                isResolved ? Icons.history_rounded : Icons.verified_rounded,
                size: 20,
                color: isResolved
                    ? const Color(0xFF3B82F6)
                    : AppColors.success,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
