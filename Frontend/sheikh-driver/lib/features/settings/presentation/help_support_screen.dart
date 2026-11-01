import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/config/app_config.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/offline/connectivity_provider.dart';
import '../../../features/auth/data/auth_repository.dart';
import '../domain/settings_access.dart';

class HelpSupportScreen extends ConsumerWidget {
  const HelpSupportScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(fleetSessionProvider);
    final online = ref.watch(isOnlineProvider);
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bodyBg = isDark ? const Color(0xFF0E141B) : const Color(0xFFF2F6F8);
    final canLocation = SettingsAccess.canSeeLocationTracking(session);
    final canSync = SettingsAccess.canSeeSyncStatus(session);
    final email = AppConfig.supportEmail.trim();
    final phone = AppConfig.supportPhone.trim();
    final hasEmail = email.isNotEmpty;
    final hasPhone = phone.isNotEmpty;

    return Scaffold(
      backgroundColor: bodyBg,
      body: Column(
        children: [
          _HelpHeader(onBack: () => Navigator.of(context).maybePop()),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
              children: [
                if (!online)
                  const Padding(
                    padding: EdgeInsets.only(bottom: 12),
                    child: _OfflineBanner(
                      message:
                          'You are offline. Email and phone still open on your device; in-app sync help is available locally.',
                    ),
                  ),
                _HelpCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const _HelpCardTitle(
                        icon: Icons.headset_mic_outlined,
                        title: 'Get help',
                      ),
                      const SizedBox(height: 6),
                      Text(
                        'Contact SheikhGo support or your fleet administrator for account, fleet, or tracking issues.',
                        style: TextStyle(
                          color: context.sg.textSecondary,
                          fontSize: 13,
                          height: 1.4,
                        ),
                      ),
                      const SizedBox(height: 12),
                      if (hasEmail)
                        _ActionRow(
                          icon: Icons.email_outlined,
                          title: 'Contact Support',
                          subtitle: email,
                          onTap: () => _launchEmail(
                            context,
                            to: email,
                            subject: 'SheikhGo Support',
                            body: _supportBody(session?.displayName),
                          ),
                        )
                      else
                        const _EmptyHint(
                          'Support email is not configured for this build.',
                        ),
                      if (hasPhone)
                        _ActionRow(
                          icon: Icons.phone_outlined,
                          title: 'Call Support',
                          subtitle: phone,
                          onTap: () => _launchTel(context, phone),
                        ),
                      _ActionRow(
                        icon: Icons.bug_report_outlined,
                        title: 'Report a Problem',
                        subtitle: hasEmail
                            ? 'Send details to support'
                            : 'Email unavailable — ask your administrator',
                        onTap: hasEmail
                            ? () => _launchEmail(
                                  context,
                                  to: email,
                                  subject: 'SheikhGo Problem Report',
                                  body: _problemBody(session?.displayName),
                                )
                            : null,
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 14),
                _HelpCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const _HelpCardTitle(
                        icon: Icons.explore_outlined,
                        title: 'Guides',
                      ),
                      const SizedBox(height: 8),
                      _ActionRow(
                        icon: Icons.location_on_outlined,
                        title: 'GPS & Tracking Help',
                        subtitle: canLocation
                            ? 'Permissions and tracking status'
                            : 'How location is used for trips',
                        onTap: () {
                          if (canLocation) {
                            context.push('/settings/location');
                          } else {
                            _showInfoSheet(
                              context,
                              title: 'GPS & Tracking',
                              body:
                                  'Location is used while on duty or on active trips for live tracking, navigation, ETA, and safety. Background GPS can be managed from Settings when your role allows. Denying location permission limits navigation and live tracking.',
                            );
                          }
                        },
                      ),
                      _ActionRow(
                        icon: Icons.sync_outlined,
                        title: 'Offline Sync Help',
                        subtitle: canSync
                            ? 'View queue, retry failed items'
                            : 'How offline actions sync',
                        onTap: () {
                          if (canSync) {
                            context.push('/offline-queue');
                          } else {
                            _showInfoSheet(
                              context,
                              title: 'Offline Synchronization',
                              body:
                                  'Actions queued offline (attendance, fuel, trips, inspections, documents) sync when connectivity returns. Open Sync & Connectivity from Settings when available to review pending or failed items.',
                            );
                          }
                        },
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 14),
                _HelpCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const _HelpCardTitle(
                        icon: Icons.help_outline_rounded,
                        title: 'FAQs',
                      ),
                      const SizedBox(height: 4),
                      ..._faqs.map(
                        (f) => _FaqTile(question: f.$1, answer: f.$2),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 14),
                _HelpCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const _HelpCardTitle(
                        icon: Icons.info_outline_rounded,
                        title: 'Legal',
                      ),
                      _ActionRow(
                        icon: Icons.privacy_tip_outlined,
                        title: 'Privacy Policy',
                        onTap: () => context.push('/legal/privacy'),
                        dense: true,
                      ),
                      _ActionRow(
                        icon: Icons.description_outlined,
                        title: 'Terms of Service',
                        onTap: () => context.push('/legal/terms'),
                        dense: true,
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static const _faqs = <(String, String)>[
    (
      'Why is my location shared?',
      'GPS points are shared with your organization for operations and safety while you are on duty or on an active trip. You can review tracking status under Location & GPS in Settings.',
    ),
    (
      'What happens when I am offline?',
      'Attendance, fuel, trips, inspections, and documents can be queued offline and sync when you reconnect. Failed items appear under Offline Sync with retry options.',
    ),
    (
      'How do I turn notifications on or off?',
      'Use Quick Settings → Notifications (or Alerts) on the Settings screen. System permission may still be required on your device.',
    ),
    (
      'Who do I contact for account access?',
      'Contact your fleet administrator for credentials, corrections, or access requests. Use Contact Support for product or app issues when email is configured.',
    ),
  ];

  static String _supportBody(String? name) => 'Hello SheikhGo Support,\n\n'
      'User: ${name ?? '—'}\n'
      'App: SheikhGo Tracking\n\n'
      'Please describe your question below:\n\n';

  static String _problemBody(String? name) => 'Hello SheikhGo Support,\n\n'
      'I am reporting a problem.\n\n'
      'User: ${name ?? '—'}\n'
      'App: SheikhGo Tracking\n'
      'Steps to reproduce:\n'
      '1.\n'
      '2.\n\n'
      'What I expected:\n\n'
      'What happened:\n\n';

  static Future<void> _launchEmail(
    BuildContext context, {
    required String to,
    required String subject,
    required String body,
  }) async {
    final query = [
      'subject=${Uri.encodeComponent(subject)}',
      'body=${Uri.encodeComponent(body)}',
    ].join('&');
    final uri = Uri.parse('mailto:$to?$query');
    final ok = await launchUrl(uri);
    if (!ok && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Could not open email app for $to'),
          backgroundColor: AppColors.error,
        ),
      );
    }
  }

  static Future<void> _launchTel(BuildContext context, String phone) async {
    final uri = Uri(
      scheme: 'tel',
      path: phone.replaceAll(RegExp(r'\s+'), ''),
    );
    final ok = await canLaunchUrl(uri) && await launchUrl(uri);
    if (!ok && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Could not start a call to $phone'),
          backgroundColor: AppColors.error,
        ),
      );
    }
  }

  static void _showInfoSheet(
    BuildContext context, {
    required String title,
    required String body,
  }) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(24, 0, 24, 28),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                    color: ctx.sg.textPrimary,
                  ),
                ),
                const SizedBox(height: 10),
                Text(
                  body,
                  style: TextStyle(
                    color: ctx.sg.textSecondary,
                    height: 1.45,
                    fontSize: 14,
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _HelpHeader extends StatelessWidget {
  const _HelpHeader({required this.onBack});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    final top = MediaQuery.paddingOf(context).top;
    return Container(
      width: double.infinity,
      color: AppColors.navy,
      padding: EdgeInsets.fromLTRB(8, top + 6, 16, 18),
      child: Row(
        children: [
          IconButton(
            onPressed: onBack,
            icon: const Icon(Icons.arrow_back_ios_new_rounded, size: 20),
            color: Colors.white,
          ),
          const Expanded(
            child: Text(
              'Help & Support',
              textAlign: TextAlign.center,
              style: TextStyle(
                color: Colors.white,
                fontSize: 18,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          const Icon(Icons.support_agent_outlined, color: AppColors.emerald),
        ],
      ),
    );
  }
}

class _HelpCard extends StatelessWidget {
  const _HelpCard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        color: context.sg.card,
        borderRadius: BorderRadius.circular(AppRadii.lg),
        boxShadow: isDark ? null : AppShadows.card,
        border: isDark
            ? Border.all(color: context.sg.border.withValues(alpha: 0.55))
            : null,
      ),
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
      child: child,
    );
  }
}

class _HelpCardTitle extends StatelessWidget {
  const _HelpCardTitle({required this.icon, required this.title});

  final IconData icon;
  final String title;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, color: AppColors.primary, size: 20),
        const SizedBox(width: 8),
        Text(
          title,
          style: TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.w700,
            color: context.sg.textPrimary,
          ),
        ),
      ],
    );
  }
}

class _ActionRow extends StatelessWidget {
  const _ActionRow({
    required this.icon,
    required this.title,
    this.subtitle,
    this.onTap,
    this.dense = false,
  });

  final IconData icon;
  final String title;
  final String? subtitle;
  final VoidCallback? onTap;
  final bool dense;

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Padding(
        padding: EdgeInsets.symmetric(vertical: dense ? 10 : 12),
        child: Row(
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.10),
                borderRadius: BorderRadius.circular(11),
                border: Border.all(
                  color: AppColors.primary.withValues(alpha: 0.22),
                ),
              ),
              child: Icon(
                icon,
                color: enabled ? AppColors.primary : context.sg.textMuted,
                size: 20,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                      color: enabled
                          ? context.sg.textPrimary
                          : context.sg.textMuted,
                    ),
                  ),
                  if (subtitle != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      subtitle!,
                      style: TextStyle(
                        fontSize: 12,
                        color: context.sg.textSecondary,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            Icon(
              Icons.chevron_right_rounded,
              color: context.sg.textMuted,
              size: 22,
            ),
          ],
        ),
      ),
    );
  }
}

class _FaqTile extends StatelessWidget {
  const _FaqTile({required this.question, required this.answer});

  final String question;
  final String answer;

  @override
  Widget build(BuildContext context) {
    return Theme(
      data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
      child: ExpansionTile(
        tilePadding: EdgeInsets.zero,
        childrenPadding: const EdgeInsets.only(bottom: 10),
        title: Text(
          question,
          style: TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w600,
            color: context.sg.textPrimary,
          ),
        ),
        children: [
          Align(
            alignment: Alignment.centerLeft,
            child: Text(
              answer,
              style: TextStyle(
                fontSize: 13,
                height: 1.4,
                color: context.sg.textSecondary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _OfflineBanner extends StatelessWidget {
  const _OfflineBanner({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.warning.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.warning.withValues(alpha: 0.35)),
      ),
      child: Row(
        children: [
          const Icon(Icons.cloud_off_outlined, color: AppColors.warning),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: TextStyle(
                fontSize: 12,
                height: 1.35,
                color: context.sg.textPrimary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _EmptyHint extends StatelessWidget {
  const _EmptyHint(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Text(
        text,
        style: TextStyle(fontSize: 13, color: context.sg.textMuted),
      ),
    );
  }
}
