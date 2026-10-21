import 'package:flutter/material.dart';
import '../../../../shared/widgets/sg_keyboard_fields.dart';

class AlertsAppBar extends StatelessWidget implements PreferredSizeWidget {
  const AlertsAppBar({
    super.key,
    required this.activeFilterCount,
    required this.refreshing,
    required this.onFilter,
    required this.onRefresh,
  });

  final int activeFilterCount;
  final bool refreshing;
  final VoidCallback onFilter;
  final VoidCallback onRefresh;

  @override
  Size get preferredSize => const Size.fromHeight(kToolbarHeight);

  @override
  Widget build(BuildContext context) {
    return AppBar(
      title: const Text('Alerts'),
      actions: [
        IconButton(
          tooltip: 'Filters',
          onPressed: onFilter,
          icon: Badge(
            isLabelVisible: activeFilterCount > 0,
            label: Text('$activeFilterCount'),
            child: const Icon(Icons.tune_rounded),
          ),
        ),
        IconButton(
          tooltip: 'Refresh',
          onPressed: refreshing ? null : onRefresh,
          icon: AnimatedRotation(
            turns: refreshing ? 1 : 0,
            duration: const Duration(milliseconds: 700),
            child: const Icon(Icons.refresh_rounded),
          ),
        ),
      ],
    );
  }
}

class AlertsSearchBar extends StatelessWidget {
  const AlertsSearchBar({
    super.key,
    required this.controller,
    required this.onChanged,
  });

  final TextEditingController controller;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      child: SgSearchField(
        controller: controller,
        hintText: 'Search vehicle plate, driver, DTC code…',
        onChanged: onChanged,
      ),
    );
  }
}
