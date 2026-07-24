import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/config/app_config.dart';
import '../../../core/constants/app_theme.dart';
import '../data/legal_documents.dart';

enum LegalDocumentKind { privacy, terms }

class LegalDocumentScreen extends StatelessWidget {
  const LegalDocumentScreen({super.key, required this.kind});

  final LegalDocumentKind kind;

  @override
  Widget build(BuildContext context) {
    final isPrivacy = kind == LegalDocumentKind.privacy;
    final title = isPrivacy ? 'Privacy Policy' : 'Terms of Service';
    final body = isPrivacy ? LegalDocuments.privacyBody : LegalDocuments.termsBody;
    final url = isPrivacy ? AppConfig.privacyPolicyUrl : AppConfig.termsOfServiceUrl;

    return Scaffold(
      appBar: AppBar(
        title: Text(title),
        actions: [
          if (url.isNotEmpty)
            IconButton(
              tooltip: 'Open in browser',
              icon: const Icon(Icons.open_in_new),
              onPressed: () => _open(url),
            ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 32),
        children: [
          const Text(
            'Effective ${LegalDocuments.effectiveDate}',
            style: TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 12),
          Text(
            body,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 14,
              height: 1.45,
            ),
          ),
          if (url.isNotEmpty) ...[
            const SizedBox(height: 24),
            OutlinedButton.icon(
              onPressed: () => _open(url),
              icon: const Icon(Icons.public),
              label: const Text('View hosted version'),
            ),
          ],
        ],
      ),
    );
  }

  Future<void> _open(String url) async {
    final uri = Uri.tryParse(url);
    if (uri == null) return;
    await launchUrl(uri, mode: LaunchMode.externalApplication);
  }
}
