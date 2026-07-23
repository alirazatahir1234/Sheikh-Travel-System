import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/compliance_api.dart';
import '../domain/compliance_models.dart';

class ComplianceHubState {
  const ComplianceHubState({
    this.summary = ComplianceSummary.empty,
    this.documents = const [],
    this.statusFilter,
    this.search = '',
  });

  final ComplianceSummary summary;
  final List<ComplianceDocument> documents;
  final String? statusFilter;
  final String search;

  List<ComplianceDocument> get visible {
    var list = documents;
    if (statusFilter != null) {
      final f = statusFilter!.toLowerCase();
      list = list.where((d) => d.status.toLowerCase().contains(f)).toList();
    }
    final q = search.trim().toLowerCase();
    if (q.isNotEmpty) {
      list = list
          .where((d) =>
              d.documentType.toLowerCase().contains(q) ||
              (d.entityName?.toLowerCase().contains(q) ?? false) ||
              (d.documentNumber?.toLowerCase().contains(q) ?? false))
          .toList();
    }
    return list;
  }

  ComplianceHubState copyWith({
    ComplianceSummary? summary,
    List<ComplianceDocument>? documents,
    String? statusFilter,
    bool clearStatus = false,
    String? search,
  }) {
    return ComplianceHubState(
      summary: summary ?? this.summary,
      documents: documents ?? this.documents,
      statusFilter: clearStatus ? null : (statusFilter ?? this.statusFilter),
      search: search ?? this.search,
    );
  }
}

final complianceHubProvider =
    AsyncNotifierProvider<ComplianceHubNotifier, ComplianceHubState>(
  ComplianceHubNotifier.new,
);

class ComplianceHubNotifier extends AsyncNotifier<ComplianceHubState> {
  @override
  Future<ComplianceHubState> build() => _load();

  Future<ComplianceHubState> _load() async {
    final api = ref.read(complianceApiProvider);
    final prev = state.valueOrNull;
    ComplianceSummary summary = ComplianceSummary.empty;
    List<ComplianceDocument> docs = const [];
    await Future.wait([
      () async {
        try {
          summary = await api.summary();
        } catch (_) {}
      }(),
      () async {
        try {
          docs = await api.list();
        } catch (_) {}
      }(),
    ]);
    return ComplianceHubState(
      summary: summary,
      documents: docs,
      statusFilter: prev?.statusFilter,
      search: prev?.search ?? '',
    );
  }

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(_load);
  }

  void setSearch(String q) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    state = AsyncData(cur.copyWith(search: q));
  }

  void setStatusFilter(String? status) {
    final cur = state.valueOrNull;
    if (cur == null) return;
    if (status == null || cur.statusFilter == status) {
      state = AsyncData(cur.copyWith(clearStatus: true));
    } else {
      state = AsyncData(cur.copyWith(statusFilter: status));
    }
  }
}

class ComplianceListScreen extends ConsumerWidget {
  const ComplianceListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(complianceHubProvider);
    final df = DateFormat('dd MMM yyyy');

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Documents'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            onPressed: () =>
                ref.read(complianceHubProvider.notifier).refresh(),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$e'),
              FilledButton(
                onPressed: () =>
                    ref.read(complianceHubProvider.notifier).refresh(),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (state) {
          final visible = state.visible;
          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () =>
                ref.read(complianceHubProvider.notifier).refresh(),
            child: CustomScrollView(
              slivers: [
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
                    child: Row(
                      children: [
                        _Stat('Expired', '${state.summary.expired}',
                            warn: state.summary.expired > 0),
                        _Stat('7 days', '${state.summary.expiring7Days}'),
                        _Stat('15 days', '${state.summary.expiring15Days}'),
                        _Stat('30 days', '${state.summary.expiring30Days}'),
                      ],
                    ),
                  ),
                ),
                SliverToBoxAdapter(
                  child: SizedBox(
                    height: 44,
                    child: ListView(
                      scrollDirection: Axis.horizontal,
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      children: [
                        for (final s in const [
                          null,
                          'Expired',
                          'Expiring',
                          'Valid',
                        ])
                          Padding(
                            padding: const EdgeInsets.only(right: 8),
                            child: FilterChip(
                              label: Text(s ?? 'All'),
                              selected: state.statusFilter == s,
                              onSelected: (_) => ref
                                  .read(complianceHubProvider.notifier)
                                  .setStatusFilter(s),
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 12),
                    child: TextField(
                      onChanged: (v) => ref
                          .read(complianceHubProvider.notifier)
                          .setSearch(v),
                      decoration: InputDecoration(
                        hintText: 'Search entity, type, number…',
                        prefixIcon: const Icon(Icons.search_rounded),
                        filled: true,
                        fillColor: Colors.white,
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(AppRadii.md),
                          borderSide: BorderSide.none,
                        ),
                      ),
                    ),
                  ),
                ),
                if (visible.isEmpty)
                  const SliverFillRemaining(
                    hasScrollBody: false,
                    child: Center(child: Text('No documents')),
                  )
                else
                  SliverPadding(
                    padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
                    sliver: SliverList.builder(
                      itemCount: visible.length,
                      itemBuilder: (_, i) {
                        final d = visible[i];
                        return SgCard(
                          margin: const EdgeInsets.only(bottom: 10),
                          onTap: d.fileUrl == null || d.fileUrl!.isEmpty
                              ? null
                              : () => _openFile(context, d.fileUrl!),
                          child: Row(
                            children: [
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      d.documentType,
                                      style: const TextStyle(
                                        fontWeight: FontWeight.w800,
                                      ),
                                    ),
                                    const SizedBox(height: 2),
                                    Text(
                                      '${d.entityType}${d.entityName != null ? ' · ${d.entityName}' : ''}',
                                      style: const TextStyle(
                                        fontSize: 13,
                                        color: AppColors.textSecondary,
                                      ),
                                    ),
                                    if (d.expiryDate != null)
                                      Text(
                                        'Expires ${df.format(d.expiryDate!.toLocal())}',
                                        style: const TextStyle(
                                          fontSize: 12,
                                          color: AppColors.textMuted,
                                        ),
                                      ),
                                  ],
                                ),
                              ),
                              StatusBadge(
                                d.status,
                                color: d.isExpired
                                    ? AppColors.error
                                    : d.isExpiring
                                        ? AppColors.warning
                                        : AppColors.success,
                              ),
                            ],
                          ),
                        );
                      },
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }

  Future<void> _openFile(BuildContext context, String url) async {
    final uri = Uri.tryParse(url);
    if (uri == null) return;
    final ok = await launchUrl(uri, mode: LaunchMode.externalApplication);
    if (!ok && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Could not open document')),
      );
    }
  }
}

class _Stat extends StatelessWidget {
  const _Stat(this.label, this.value, {this.warn = false});
  final String label;
  final String value;
  final bool warn;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        children: [
          Text(
            value,
            style: TextStyle(
              fontWeight: FontWeight.w800,
              fontSize: 16,
              color: warn ? AppColors.error : AppColors.textPrimary,
            ),
          ),
          Text(
            label,
            style: const TextStyle(fontSize: 11, color: AppColors.textMuted),
          ),
        ],
      ),
    );
  }
}
