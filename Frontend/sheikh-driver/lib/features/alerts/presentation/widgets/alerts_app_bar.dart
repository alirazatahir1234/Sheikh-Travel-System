import 'package:flutter/material.dart';
import '../../../../core/constants/app_theme.dart';

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
      child: TextField(
        controller: controller,
        onChanged: onChanged,
        textInputAction: TextInputAction.search,
        decoration: InputDecoration(
          hintText: 'Search vehicle plate, driver, DTC code…',
          prefixIcon: const Icon(Icons.search_rounded, color: AppColors.textMuted),
          filled: true,
          fillColor: Colors.white,
          contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(AppRadii.md),
            borderSide: const BorderSide(color: AppColors.border),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(AppRadii.md),
            borderSide: const BorderSide(color: AppColors.border),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(AppRadii.md),
            borderSide: const BorderSide(color: AppColors.primary, width: 1.4),
          ),
        ),
      ),
    );
  }
}
