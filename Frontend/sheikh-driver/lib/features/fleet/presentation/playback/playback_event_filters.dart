import 'package:flutter/material.dart';

import '../../../../core/constants/app_theme.dart';
import 'playback_helpers.dart';

/// Horizontal event filter chips — scrollable so labels never clip.
class PlaybackEventFilters extends StatelessWidget {
  const PlaybackEventFilters({
    super.key,
    required this.selected,
    required this.onSelected,
    this.filters = const [
      TripEventFilter.all,
      TripEventFilter.stops,
      TripEventFilter.overspeed,
      TripEventFilter.ignition,
      TripEventFilter.fuel,
      TripEventFilter.sos,
    ],
  });

  final TripEventFilter selected;
  final ValueChanged<TripEventFilter> onSelected;
  final List<TripEventFilter> filters;

  static String labelFor(TripEventFilter f) => switch (f) {
        TripEventFilter.all => 'All',
        TripEventFilter.stops => 'Stops',
        TripEventFilter.overspeed => 'Overspeed',
        TripEventFilter.fuel => 'Fuel',
        TripEventFilter.sos => 'SOS',
        TripEventFilter.ignition => 'Ignition',
        TripEventFilter.geofence => 'Geofence',
      };

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return SizedBox(
      height: 36,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        physics: const BouncingScrollPhysics(),
        itemCount: filters.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (_, i) {
          final f = filters[i];
          final isSelected = selected == f;
          return Semantics(
            button: true,
            selected: isSelected,
            label: 'Filter ${labelFor(f)}',
            child: Material(
              color: isSelected
                  ? AppColors.primary.withValues(alpha: 0.14)
                  : p.chipBg,
              borderRadius: BorderRadius.circular(AppRadii.pill),
              child: InkWell(
                onTap: () => onSelected(f),
                borderRadius: BorderRadius.circular(AppRadii.pill),
                child: Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(AppRadii.pill),
                    border: Border.all(
                      color: isSelected ? AppColors.primary : p.border,
                      width: isSelected ? 1.4 : 1,
                    ),
                  ),
                  child: Text(
                    labelFor(f),
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: isSelected ? AppColors.primary : p.textPrimary,
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
