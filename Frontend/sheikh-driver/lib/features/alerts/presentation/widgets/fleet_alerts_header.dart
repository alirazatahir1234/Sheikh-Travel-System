import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'fleet_alerts_colors.dart';

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
      padding: const EdgeInsets.fromLTRB(8, 4, 8, 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (canPop)
                IconButton(
                  tooltip: 'Back',
                  onPressed: () => context.pop(),
                  icon: const Icon(Icons.arrow_back_ios_new_rounded, size: 18),
                  color: FleetAlertsColors.navy,
                )
              else
                const SizedBox(width: 12),
              Expanded(
                child: Padding(
                  padding: EdgeInsets.only(
                    left: canPop ? 0 : 4,
                    top: 10,
                    right: 4,
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Fleet Alerts',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                          fontSize: 26,
                          fontWeight: FontWeight.w800,
                          color: FleetAlertsColors.navy,
                          height: 1.1,
                          letterSpacing: -0.4,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '$companyName • $vehicleCount vehicles',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 13,
                          color: FleetAlertsColors.secondary,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
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
                        color: FleetAlertsColors.navy,
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
                                color: FleetAlertsColors.critical,
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
        ],
      ),
    );
  }
}
