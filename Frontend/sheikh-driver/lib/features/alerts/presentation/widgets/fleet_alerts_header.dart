import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/constants/app_theme.dart';
import '../../domain/alert_display.dart';
import '../alerts_notifier.dart';

class FleetAlertsHeader extends StatelessWidget {
  const FleetAlertsHeader({
    super.key,
    required this.companyName,
    required this.vehicleCount,
    required this.unreadNotifications,
    required this.onBell,
  });

  final String companyName;
  final int vehicleCount;
  final int unreadNotifications;
  final VoidCallback onBell;

  @override
  Widget build(BuildContext context) {
    final canPop = GoRouter.of(context).canPop();
    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 4, 8, 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              if (canPop)
                IconButton(
                  tooltip: 'Back',
                  onPressed: () => context.pop(),
                  icon: const Icon(Icons.arrow_back_ios_new_rounded, size: 18),
                  color: AppColors.navy,
                )
              else
                const SizedBox(width: 12),
              const Spacer(),
              IconButton(
                tooltip: 'Notifications',
                onPressed: onBell,
                // Stack + Opacity — Material Badge mid-frame toggles dirty
                // ParentData and can blank the alerts body (see AppShell).
                icon: SizedBox(
                  width: 28,
                  height: 28,
                  child: Stack(
                    clipBehavior: Clip.none,
                    alignment: Alignment.center,
                    children: [
                      const Icon(
                        Icons.notifications_none_rounded,
                        color: AppColors.navy,
                      ),
                      Positioned(
                        right: 0,
                        top: 0,
                        child: IgnorePointer(
                          child: Opacity(
                            opacity: unreadNotifications > 0 ? 1 : 0,
                            child: Container(
                              width: 8,
                              height: 8,
                              decoration: const BoxDecoration(
                                color: AppColors.error,
                                shape: BoxShape.circle,
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                RichText(
                  text: const TextSpan(
                    style: TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.w800,
                      letterSpacing: -0.4,
                    ),
                    children: [
                      TextSpan(
                        text: 'Sheikh',
                        style: TextStyle(color: AppColors.navy),
                      ),
                      TextSpan(
                        text: 'Go',
                        style: TextStyle(color: AppColors.primary),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 2),
                const Text(
                  'Fleet Alerts',
                  style: TextStyle(
                    fontSize: 26,
                    fontWeight: FontWeight.w800,
                    color: AppColors.navy,
                    height: 1.1,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  '$companyName • $vehicleCount vehicles',
                  style: const TextStyle(
                    fontSize: 13,
                    color: AppColors.textSecondary,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class FleetAlertKpiRow extends StatelessWidget {
  const FleetAlertKpiRow({
    super.key,
    required this.summary,
    required this.selected,
    required this.unacknowledgedSelected,
    required this.onSelect,
    required this.onUnacknowledged,
  });

  final AlertsFeedSummary summary;
  final FleetAlertChip selected;
  final bool unacknowledgedSelected;
  final ValueChanged<FleetAlertChip> onSelect;
  final VoidCallback onUnacknowledged;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 92,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        children: [
          _KpiCard(
            label: 'Critical',
            count: summary.critical,
            icon: Icons.warning_amber_rounded,
            bg: const Color(0xFFFEE2E2),
            fg: AppColors.error,
            selected: selected == FleetAlertChip.critical,
            onTap: () => onSelect(FleetAlertChip.critical),
          ),
          _KpiCard(
            label: 'Warning',
            count: summary.warning,
            icon: Icons.error_outline_rounded,
            bg: const Color(0xFFFFEDD5),
            fg: AppColors.warning,
            selected: selected == FleetAlertChip.warning,
            onTap: () => onSelect(FleetAlertChip.warning),
          ),
          _KpiCard(
            label: 'Info',
            count: summary.info,
            icon: Icons.info_outline_rounded,
            bg: const Color(0xFFDBEAFE),
            fg: AppColors.info,
            selected: selected == FleetAlertChip.info,
            onTap: () => onSelect(FleetAlertChip.info),
          ),
          _KpiCard(
            label: 'Unacknowledged',
            count: summary.unacknowledged,
            icon: Icons.notifications_active_outlined,
            bg: const Color(0xFFCCFBF1),
            fg: AppColors.primary,
            selected: unacknowledgedSelected,
            onTap: onUnacknowledged,
            emphasizeTap: true,
          ),
        ],
      ),
    );
  }
}

class _KpiCard extends StatelessWidget {
  const _KpiCard({
    required this.label,
    required this.count,
    required this.icon,
    required this.bg,
    required this.fg,
    required this.selected,
    required this.onTap,
    this.emphasizeTap = false,
  });

  final String label;
  final int count;
  final IconData icon;
  final Color bg;
  final Color fg;
  final bool selected;
  final VoidCallback onTap;
  final bool emphasizeTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(right: 10),
      child: Material(
        color: bg,
        borderRadius: BorderRadius.circular(14),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(14),
          child: Container(
            width: 118,
            height: 92,
            padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(14),
              border: selected
                  ? Border.all(color: fg, width: 1.5)
                  : Border.all(color: Colors.transparent),
            ),
            // Fixed column (no Spacer) — Spacer overflowed the 86px strip by
            // 8px and contributed to a blank body after layout errors.
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(icon, size: 16, color: fg),
                    const Spacer(),
                    if (emphasizeTap)
                      Icon(Icons.filter_list_rounded, size: 14, color: fg),
                  ],
                ),
                const SizedBox(height: 8),
                Text(
                  '$count',
                  style: TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                    color: fg,
                    height: 1,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w600,
                    color: fg.withValues(alpha: 0.9),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class FleetAlertFilterChips extends StatelessWidget {
  const FleetAlertFilterChips({
    super.key,
    required this.selected,
    required this.summary,
    required this.onSelected,
  });

  final FleetAlertChip selected;
  final AlertsFeedSummary summary;
  final ValueChanged<FleetAlertChip> onSelected;

  @override
  Widget build(BuildContext context) {
    String countFor(FleetAlertChip c) => switch (c) {
          FleetAlertChip.all => '${summary.total}',
          FleetAlertChip.critical => '${summary.critical}',
          FleetAlertChip.warning => '${summary.warning}',
          FleetAlertChip.info => '${summary.info}',
          FleetAlertChip.acknowledged => '',
          FleetAlertChip.resolved => '',
        };

    return SizedBox(
      height: 40,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        children: [
          for (final chip in FleetAlertChip.values)
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: ChoiceChip(
                label: Text(
                  countFor(chip).isEmpty
                      ? chip.label
                      : '${chip.label} ${countFor(chip)}',
                ),
                selected: selected == chip,
                onSelected: (_) => onSelected(chip),
                selectedColor: AppColors.primary,
                labelStyle: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  color: selected == chip ? Colors.white : AppColors.navy,
                ),
                backgroundColor: Colors.white,
                side: BorderSide(
                  color: selected == chip
                      ? AppColors.primary
                      : const Color(0xFFD5DEE7),
                ),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(20),
                ),
                showCheckmark: false,
                visualDensity: VisualDensity.compact,
                materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
              ),
            ),
        ],
      ),
    );
  }
}

class FleetAlertDateSortRow extends StatelessWidget {
  const FleetAlertDateSortRow({
    super.key,
    required this.datePreset,
    required this.sort,
    required this.onDate,
    required this.onSort,
  });

  final String datePreset;
  final FleetAlertSort sort;
  final VoidCallback onDate;
  final VoidCallback onSort;

  String get _dateLabel {
    final now = DateTime.now();
    final d = '${now.day} ${_month(now.month)} ${now.year}';
    return switch (datePreset) {
      'today' => 'Today • $d',
      'last7' => 'Last 7 days',
      'last30' => 'Last 30 days',
      'all' => 'All time',
      _ => datePreset,
    };
  }

  String _month(int m) => const [
        'Jan',
        'Feb',
        'Mar',
        'Apr',
        'May',
        'Jun',
        'Jul',
        'Aug',
        'Sep',
        'Oct',
        'Nov',
        'Dec'
      ][m - 1];

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: Row(
        children: [
          Expanded(
            child: OutlinedButton.icon(
              onPressed: onDate,
              icon: const Icon(Icons.calendar_today_outlined, size: 16),
              label: Text(
                _dateLabel,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
              ),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.navy,
                side: const BorderSide(color: Color(0xFFD5DEE7)),
                backgroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
              ),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: OutlinedButton.icon(
              onPressed: onSort,
              icon: const Icon(Icons.sort_rounded, size: 16),
              label: Text(
                sort == FleetAlertSort.newest ? 'Newest first' : 'Oldest first',
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
              ),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.navy,
                side: const BorderSide(color: Color(0xFFD5DEE7)),
                backgroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class FleetAlertsBulkBar extends StatelessWidget {
  const FleetAlertsBulkBar({
    super.key,
    required this.selectedCount,
    required this.canAcknowledge,
    required this.canAssign,
    required this.onToggleSelectMode,
    required this.onAcknowledge,
    required this.onAssign,
    required this.selectMode,
  });

  final int selectedCount;
  final bool canAcknowledge;
  final bool canAssign;
  final VoidCallback onToggleSelectMode;
  final VoidCallback? onAcknowledge;
  final VoidCallback? onAssign;
  final bool selectMode;

  @override
  Widget build(BuildContext context) {
    return Material(
      elevation: 8,
      color: Colors.white,
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 10),
          child: Row(
            children: [
              InkWell(
                onTap: onToggleSelectMode,
                borderRadius: BorderRadius.circular(8),
                child: Row(
                  children: [
                    Icon(
                      selectMode
                          ? Icons.check_box_rounded
                          : Icons.check_box_outline_blank_rounded,
                      color: AppColors.primary,
                      size: 22,
                    ),
                    const SizedBox(width: 6),
                    Text(
                      selectMode && selectedCount > 0
                          ? '$selectedCount selected'
                          : 'Select alerts',
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 13,
                        color: AppColors.navy,
                      ),
                    ),
                  ],
                ),
              ),
              const Spacer(),
              if (canAcknowledge)
                OutlinedButton.icon(
                  onPressed: onAcknowledge,
                  icon: const Icon(Icons.check_rounded, size: 18),
                  label: const Text('Acknowledge'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.primary,
                    side: const BorderSide(color: AppColors.primary),
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                  ),
                ),
              if (canAssign) ...[
                const SizedBox(width: 8),
                FilledButton.icon(
                  onPressed: onAssign,
                  icon: const Icon(Icons.person_add_alt_1_rounded, size: 18),
                  label: const Text('Assign'),
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
