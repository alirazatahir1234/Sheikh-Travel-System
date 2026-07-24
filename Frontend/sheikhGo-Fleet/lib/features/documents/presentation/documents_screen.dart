import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/constants/app_theme.dart';
import '../data/documents_api.dart';
import '../domain/document_models.dart';

final documentsProvider =
    FutureProvider.autoDispose<DocumentsBundle>(
  (ref) => ref.read(documentsApiProvider).getDocuments(),
);

class DocumentsScreen extends ConsumerWidget {
  const DocumentsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(documentsProvider);

    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Documents'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.invalidate(documentsProvider),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('$e', textAlign: TextAlign.center),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () => ref.invalidate(documentsProvider),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
        data: (bundle) => RefreshIndicator(
          onRefresh: () async => ref.invalidate(documentsProvider),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _SummaryBar(bundle: bundle),
              if (bundle.cnicNumber != null && bundle.cnicNumber!.isNotEmpty) ...[
                const SizedBox(height: 12),
                Text('CNIC on file: ${bundle.cnicNumber}',
                    style: const TextStyle(color: AppColors.textSecondary)),
              ],
              const SizedBox(height: 16),
              const Text('Driver documents',
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
              const SizedBox(height: 8),
              ...bundle.documents
                  .where((d) => d.scope == 'Driver')
                  .map((d) => _DocumentCard(doc: d)),
              const SizedBox(height: 16),
              const Text('Vehicle documents',
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
              const SizedBox(height: 8),
              ...bundle.documents
                  .where((d) => d.scope == 'Vehicle')
                  .map((d) => _DocumentCard(doc: d)),
              const SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }
}

class _SummaryBar extends StatelessWidget {
  const _SummaryBar({required this.bundle});
  final DocumentsBundle bundle;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        _Chip(label: 'Missing', value: bundle.missingCount, color: AppColors.warning),
        const SizedBox(width: 8),
        _Chip(label: 'Expiring', value: bundle.expiringCount, color: AppColors.warning),
        const SizedBox(width: 8),
        _Chip(label: 'Expired', value: bundle.expiredCount, color: AppColors.error),
      ],
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.label, required this.value, required this.color});
  final String label;
  final int value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 10),
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(10),
        ),
        child: Column(
          children: [
            Text('$value',
                style: TextStyle(
                    fontWeight: FontWeight.w800, color: color, fontSize: 18)),
            Text(label,
                style: TextStyle(fontSize: 11, color: color.withValues(alpha: 0.9))),
          ],
        ),
      ),
    );
  }
}

class _DocumentCard extends ConsumerWidget {
  const _DocumentCard({required this.doc});
  final DriverDocument doc;

  Color get _statusColor => switch (doc.status) {
        'Expired' || 'Rejected' => AppColors.error,
        'Expiring' || 'Missing' || 'Pending' => AppColors.warning,
        _ => AppColors.success,
      };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final fmt = DateFormat('dd MMM yyyy');
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(doc.title,
                          style: const TextStyle(fontWeight: FontWeight.w700)),
                      if (doc.vehicleName != null)
                        Text(doc.vehicleName!,
                            style: const TextStyle(
                                fontSize: 12, color: AppColors.textSecondary)),
                    ],
                  ),
                ),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: _statusColor.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(doc.status,
                      style: TextStyle(
                          color: _statusColor,
                          fontSize: 11,
                          fontWeight: FontWeight.w700)),
                ),
              ],
            ),
            if (doc.expiryDate != null) ...[
              const SizedBox(height: 6),
              Text(
                doc.isExpired
                    ? 'Expired ${fmt.format(doc.expiryDate!.toLocal())}'
                    : doc.isExpiringSoon
                        ? 'Expires in ${doc.daysUntilExpiry} days (${fmt.format(doc.expiryDate!.toLocal())})'
                        : 'Expires ${fmt.format(doc.expiryDate!.toLocal())}',
                style: TextStyle(
                  fontSize: 12,
                  color: doc.needsAttention
                      ? AppColors.error
                      : AppColors.textSecondary,
                ),
              ),
            ],
            const SizedBox(height: 10),
            Row(
              children: [
                if (doc.hasFile)
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () => _openPreview(context, doc.previewUrl!),
                      icon: const Icon(Icons.visibility_outlined, size: 18),
                      label: const Text('Preview'),
                    ),
                  ),
                if (doc.hasFile && doc.canUpload) const SizedBox(width: 8),
                if (doc.canUpload)
                  Expanded(
                    child: FilledButton.icon(
                      onPressed: () => _upload(context, ref, doc),
                      icon: const Icon(Icons.upload_file, size: 18),
                      label: Text(doc.hasFile ? 'Replace' : 'Upload'),
                    ),
                  ),
                if (!doc.canUpload && !doc.hasFile)
                  const Text('Assign a vehicle to upload',
                      style: TextStyle(
                          fontSize: 12, color: AppColors.textSecondary)),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _openPreview(BuildContext context, String url) async {
    final uri = Uri.parse(url);
    final ok = await launchUrl(uri, mode: LaunchMode.externalApplication);
    if (!ok && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Could not open document')),
      );
    }
  }

  Future<void> _upload(
      BuildContext context, WidgetRef ref, DriverDocument doc) async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (_) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.camera_alt),
              title: const Text('Camera'),
              onTap: () => Navigator.pop(context, ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Gallery'),
              onTap: () => Navigator.pop(context, ImageSource.gallery),
            ),
          ],
        ),
      ),
    );
    if (source == null) return;

    final picker = ImagePicker();
    final x = await picker.pickImage(
      source: source,
      imageQuality: 75,
      maxWidth: 2000,
    );
    if (x == null) return;

    DateTime? expiry;
    if (context.mounted) {
      expiry = await showDatePicker(
        context: context,
        firstDate: DateTime.now().subtract(const Duration(days: 30)),
        lastDate: DateTime.now().add(const Duration(days: 365 * 10)),
        initialDate: DateTime.now().add(const Duration(days: 365)),
        helpText: 'Expiry date (optional)',
      );
    }

    if (!context.mounted) return;
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => const Center(child: CircularProgressIndicator()),
    );

    try {
      await ref.read(documentsApiProvider).upload(
            documentType: doc.documentType,
            file: File(x.path),
            expiryDate: expiry,
            vehicleId: doc.vehicleId,
          );
      ref.invalidate(documentsProvider);
      if (context.mounted) {
        Navigator.pop(context); // dialog
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Document uploaded')),
        );
      }
    } catch (e) {
      if (context.mounted) {
        Navigator.pop(context);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('$e'), backgroundColor: AppColors.error),
        );
      }
    }
  }
}
