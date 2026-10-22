import 'package:flutter/material.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
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
    final l10n = AppLocalizations.of(context);
    final p = context.sg;
    final isPrivacy = kind == LegalDocumentKind.privacy;
    final title = isPrivacy ? l10n.privacyPolicy : l10n.termsOfService;
    final body =
        isPrivacy ? LegalDocuments.privacyBody : LegalDocuments.termsBody;
    final url =
        isPrivacy ? AppConfig.privacyPolicyUrl : AppConfig.termsOfServiceUrl;

    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(
        title: Text(title),
        actions: [
          if (url.isNotEmpty)
            IconButton(
              tooltip: l10n.openInBrowser,
              icon: const Icon(Icons.open_in_new),
              onPressed: () => _open(url),
            ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 32),
        children: [
          Text(
            l10n.legalEffectiveDate(LegalDocuments.effectiveDate),
            style: TextStyle(
              color: p.textSecondary,
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: 12),
          Text(
            body,
            style: TextStyle(
              color: p.textPrimary,
              fontSize: 14,
              height: 1.45,
            ),
          ),
          if (url.isNotEmpty) ...[
            const SizedBox(height: 24),
            OutlinedButton.icon(
              onPressed: () => _open(url),
              icon: const Icon(Icons.public),
              label: Text(l10n.viewHostedVersion),
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
