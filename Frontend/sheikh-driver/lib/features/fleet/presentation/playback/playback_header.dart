import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';

/// Compact map-first vehicle header (back + identity + date + actions).
class PlaybackHeader extends StatelessWidget {
  const PlaybackHeader({
    super.key,
    required this.onBack,
    required this.vehicleName,
    this.plateNumber,
    this.driverName,
    this.dateLabel,
    this.onDateTap,
    this.onMore,
    this.moreButton,
  });

  final VoidCallback onBack;
  final String vehicleName;
  final String? plateNumber;
  final String? driverName;
  final String? dateLabel;
  final VoidCallback? onDateTap;
  final VoidCallback? onMore;
  final Widget? moreButton;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final plate = (plateNumber ?? '').trim();
    final driver = (driverName ?? '').trim();
    final subtitle = [
      if (plate.isNotEmpty) plate,
      if (driver.isNotEmpty) driver,
    ].join(' · ');

    return Material(
      color: p.card.withValues(alpha: 0.96),
      elevation: 2,
      shadowColor: Colors.black26,
      borderRadius: BorderRadius.circular(AppRadii.lg),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(4, 4, 6, 4),
        child: Row(
          children: [
            IconButton(
              tooltip: 'Back',
              onPressed: onBack,
              icon: const Icon(Icons.arrow_back_rounded),
              visualDensity: VisualDensity.compact,
              constraints: const BoxConstraints(minWidth: 44, minHeight: 44),
            ),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    vehicleName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 14,
                      color: p.textPrimary,
                    ),
                  ),
                  if (subtitle.isNotEmpty)
                    Text(
                      subtitle,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: p.textSecondary,
                      ),
                    ),
                ],
              ),
            ),
            if (dateLabel != null && dateLabel!.isNotEmpty) ...[
              const SizedBox(width: 4),
              Material(
                color: p.chipBg,
                borderRadius: BorderRadius.circular(AppRadii.sm),
                child: InkWell(
                  onTap: onDateTap,
                  borderRadius: BorderRadius.circular(AppRadii.sm),
                  child: Padding(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.calendar_today_rounded,
                          size: 14,
                          color: p.textSecondary,
                        ),
                        const SizedBox(width: 6),
                        ConstrainedBox(
                          constraints: const BoxConstraints(maxWidth: 110),
                          child: Text(
                            dateLabel!,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                              color: p.textPrimary,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ],
            if (moreButton != null)
              moreButton!
            else if (onMore != null)
              IconButton(
                tooltip: 'More',
                onPressed: onMore,
                icon: const Icon(Icons.more_vert_rounded),
                visualDensity: VisualDensity.compact,
                constraints: const BoxConstraints(minWidth: 44, minHeight: 44),
              ),
          ],
        ),
      ),
    );
  }
}
