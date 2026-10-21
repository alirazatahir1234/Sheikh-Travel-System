import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/constants/app_theme.dart';
import '../../../features/drivers/data/drivers_api.dart';
import '../../../features/drivers/domain/driver_models.dart';
import '../../../features/fleet/data/fleet_api.dart';
import '../../../features/fleet/domain/fleet_models.dart';
import '../../../shared/widgets/sg_ui.dart';

Future<int?> pickDriverId(BuildContext context, WidgetRef ref) async {
  final drivers = await ref.read(driversApiProvider).list();
  if (!context.mounted) return null;
  return showModalBottomSheet<int>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    backgroundColor: context.sg.card,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadii.xl)),
    ),
    builder: (ctx) => _AssignPickerSheet<DriverListItem>(
      title: 'Assign driver',
      subtitle: 'Select a driver for this booking',
      items: drivers,
      searchHint: 'Search name or phone…',
      matches: (d, q) =>
          d.fullName.toLowerCase().contains(q) || d.phone.contains(q),
      itemBuilder: (context, d) {
        final name = d.fullName.trim().isEmpty ? 'Driver' : d.fullName.trim();
        return ListTile(
          contentPadding:
              const EdgeInsets.symmetric(horizontal: 20, vertical: 2),
          leading: SgAvatar(label: name, size: 42),
          title: Text(
            name,
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
          subtitle: Text(
            '${d.phone} · ${d.status}',
            style: TextStyle(
              fontSize: 12,
              color: context.sg.textSecondary,
            ),
          ),
          trailing: StatusBadge(d.status),
          onTap: () => Navigator.pop(ctx, d.id),
        );
      },
      emptyIcon: Icons.groups_outlined,
      emptyTitle: 'No drivers available',
    ),
  );
}

Future<int?> pickVehicleId(BuildContext context, WidgetRef ref) async {
  final vehicles = await ref.read(fleetApiProvider).getVehicles();
  if (!context.mounted) return null;
  return showModalBottomSheet<int>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    backgroundColor: context.sg.card,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(AppRadii.xl)),
    ),
    builder: (ctx) => _AssignPickerSheet<VehicleListItem>(
      title: 'Assign vehicle',
      subtitle: 'Select a vehicle for this driver',
      items: vehicles,
      searchHint: 'Search name or plate…',
      matches: (v, q) =>
          v.name.toLowerCase().contains(q) ||
          v.registrationNumber.toLowerCase().contains(q),
      itemBuilder: (context, v) {
        return ListTile(
          contentPadding:
              const EdgeInsets.symmetric(horizontal: 20, vertical: 2),
          leading: const SgIconWell(
            icon: Icons.local_shipping_outlined,
            size: 42,
          ),
          title: Text(
            v.name,
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
          subtitle: Text(
            '${v.registrationNumber} · ${v.status}',
            style: TextStyle(
              fontSize: 12,
              color: context.sg.textSecondary,
            ),
          ),
          trailing: StatusBadge(v.status),
          onTap: () => Navigator.pop(ctx, v.id),
        );
      },
      emptyIcon: Icons.local_shipping_outlined,
      emptyTitle: 'No vehicles available',
    ),
  );
}

class _AssignPickerSheet<T> extends StatefulWidget {
  const _AssignPickerSheet({
    required this.title,
    required this.subtitle,
    required this.items,
    required this.searchHint,
    required this.matches,
    required this.itemBuilder,
    required this.emptyIcon,
    required this.emptyTitle,
  });

  final String title;
  final String subtitle;
  final List<T> items;
  final String searchHint;
  final bool Function(T item, String query) matches;
  final Widget Function(BuildContext context, T item) itemBuilder;
  final IconData emptyIcon;
  final String emptyTitle;

  @override
  State<_AssignPickerSheet<T>> createState() => _AssignPickerSheetState<T>();
}

class _AssignPickerSheetState<T> extends State<_AssignPickerSheet<T>> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final q = _query.trim().toLowerCase();
    final visible = q.isEmpty
        ? widget.items
        : widget.items.where((e) => widget.matches(e, q)).toList();

    return SafeArea(
      child: SizedBox(
        height: MediaQuery.sizeOf(context).height * 0.68,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    widget.title,
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          fontWeight: FontWeight.w800,
                          letterSpacing: -0.3,
                          color: p.textPrimary,
                        ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    widget.subtitle,
                    style: TextStyle(
                      fontSize: 13,
                      color: p.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
              child: TextField(
                onChanged: (v) => setState(() => _query = v),
                decoration: InputDecoration(
                  hintText: widget.searchHint,
                  prefixIcon: Icon(Icons.search_rounded, color: p.textMuted),
                  filled: true,
                  fillColor: p.surface,
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadii.md),
                    borderSide: BorderSide(color: p.border),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadii.md),
                    borderSide: BorderSide(color: p.border.withValues(alpha: 0.85)),
                  ),
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: 14,
                    vertical: 12,
                  ),
                ),
              ),
            ),
            Expanded(
              child: visible.isEmpty
                  ? SgEmptyState(
                      icon: widget.emptyIcon,
                      title: widget.emptyTitle,
                      subtitle: q.isEmpty
                          ? null
                          : 'No matches for “$_query”. Try another search.',
                    )
                  : ListView.separated(
                      itemCount: visible.length,
                      separatorBuilder: (_, __) => Divider(
                        height: 1,
                        indent: 78,
                        color: p.divider,
                      ),
                      itemBuilder: (context, i) =>
                          widget.itemBuilder(context, visible[i]),
                    ),
            ),
          ],
        ),
      ),
    );
  }
}
