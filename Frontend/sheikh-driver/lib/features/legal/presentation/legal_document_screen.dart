import 'package:flutter/material.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations.dart';
import 'package:sheikh_go_driver/l10n/generated/app_localizations_en.dart';
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
    final l10n =
        Localizations.of<AppLocalizations>(context, AppLocalizations) ??
            AppLocalizationsEn();
    final isPrivacy = kind == LegalDocumentKind.privacy;
    final title = isPrivacy ? l10n.privacyPolicy : l10n.termsOfService;
    final sections = isPrivacy
        ? LegalDocuments.privacySections
        : LegalDocuments.termsSections;
    final version =
        isPrivacy ? LegalDocuments.privacyVersion : LegalDocuments.termsVersion;
    final url =
        isPrivacy ? AppConfig.privacyPolicyUrl : AppConfig.termsOfServiceUrl;
    final headerIcon =
        isPrivacy ? Icons.privacy_tip_outlined : Icons.description_outlined;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bodyBg = isDark ? const Color(0xFF0E141B) : const Color(0xFFF2F6F8);

    return Scaffold(
      backgroundColor: bodyBg,
      body: Column(
        children: [
          _LegalHeader(
            title: title,
            icon: headerIcon,
            onBack: () => Navigator.of(context).maybePop(),
            onOpenHosted: url.isNotEmpty ? () => _open(url) : null,
            openTooltip: l10n.openInBrowser,
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
              children: [
                Container(
                  width: double.infinity,
                  decoration: BoxDecoration(
                    color: context.sg.card,
                    borderRadius: BorderRadius.circular(AppRadii.lg),
                    boxShadow: isDark ? null : AppShadows.card,
                    border: isDark
                        ? Border.all(
                            color: context.sg.border.withValues(alpha: 0.55),
                          )
                        : null,
                  ),
                  padding: const EdgeInsets.fromLTRB(18, 18, 18, 20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Container(
                            width: 48,
                            height: 48,
                            decoration: BoxDecoration(
                              color: AppColors.primary.withValues(alpha: 0.12),
                              borderRadius: BorderRadius.circular(14),
                            ),
                            child: Icon(
                              headerIcon,
                              color: AppColors.primary,
                              size: 26,
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  isPrivacy
                                      ? 'Data & Privacy Overview'
                                      : 'User Agreement Overview',
                                  style: TextStyle(
                                    fontSize: 18,
                                    fontWeight: FontWeight.w800,
                                    color: context.sg.textPrimary,
                                    letterSpacing: -0.2,
                                  ),
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  l10n.legalEffectiveDate(
                                    LegalDocuments.effectiveDate,
                                  ),
                                  style: TextStyle(
                                    color: context.sg.textSecondary,
                                    fontSize: 13,
                                    fontWeight: FontWeight.w500,
                                  ),
                                ),
                                const SizedBox(height: 2),
                                Text(
                                  'Version $version',
                                  style: TextStyle(
                                    color: context.sg.textMuted,
                                    fontSize: 12,
                                    fontWeight: FontWeight.w500,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 20),
                      for (var i = 0; i < sections.length; i++) ...[
                        if (i > 0) const SizedBox(height: 22),
                        _SectionBlock(section: sections[i]),
                      ],
                    ],
                  ),
                ),
                if (url.isNotEmpty) ...[
                  const SizedBox(height: 16),
                  OutlinedButton.icon(
                    onPressed: () => _open(url),
                    icon: const Icon(Icons.public),
                    label: Text(l10n.viewHostedVersion),
                  ),
                ],
              ],
            ),
          ),
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

class _LegalHeader extends StatelessWidget {
  const _LegalHeader({
    required this.title,
    required this.icon,
    required this.onBack,
    this.onOpenHosted,
    this.openTooltip,
  });

  final String title;
  final IconData icon;
  final VoidCallback onBack;
  final VoidCallback? onOpenHosted;
  final String? openTooltip;

  @override
  Widget build(BuildContext context) {
    final top = MediaQuery.paddingOf(context).top;
    return Container(
      width: double.infinity,
      color: AppColors.navy,
      padding: EdgeInsets.fromLTRB(8, top + 6, 12, 14),
      child: Row(
        children: [
          IconButton(
            onPressed: onBack,
            icon: const Icon(Icons.arrow_back_ios_new_rounded, size: 20),
            color: Colors.white,
            tooltip: MaterialLocalizations.of(context).backButtonTooltip,
          ),
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 18,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          if (onOpenHosted != null)
            IconButton(
              tooltip: openTooltip,
              onPressed: onOpenHosted,
              icon: const Icon(Icons.open_in_new_rounded, size: 20),
              color: AppColors.emerald,
            )
          else
            Icon(icon, color: AppColors.emerald, size: 22),
        ],
      ),
    );
  }
}

class _SectionBlock extends StatelessWidget {
  const _SectionBlock({required this.section});

  final LegalSection section;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          section.title,
          style: TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.w800,
            color: p.textPrimary,
          ),
        ),
        const SizedBox(height: 8),
        for (final paragraph in section.paragraphs) ...[
          Text(
            paragraph,
            style: TextStyle(
              color: p.textPrimary,
              fontSize: 14,
              height: 1.45,
            ),
          ),
          const SizedBox(height: 8),
        ],
        if (section.bullets.isNotEmpty) ...[
          for (final bullet in section.bullets)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Padding(
                    padding: EdgeInsets.only(top: 2),
                    child: Icon(
                      Icons.check_circle_rounded,
                      size: 18,
                      color: AppColors.primary,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      bullet,
                      style: TextStyle(
                        color: p.textPrimary,
                        fontSize: 14,
                        height: 1.4,
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
        if (section.infoTitle != null && section.infoBody != null) ...[
          const SizedBox(height: 8),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(14),
              border: Border.all(
                color: AppColors.primary.withValues(alpha: 0.18),
              ),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Icon(
                  Icons.info_outline_rounded,
                  color: AppColors.primary,
                  size: 22,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        section.infoTitle!,
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 14,
                          color: p.textPrimary,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        section.infoBody!,
                        style: TextStyle(
                          fontSize: 13,
                          height: 1.4,
                          color: p.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ],
    );
  }
}
