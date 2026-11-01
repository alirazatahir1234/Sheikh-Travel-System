import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../../core/constants/app_theme.dart';

/// Digits suitable for `https://wa.me/<digits>` / `whatsapp://send?phone=`.
///
/// Handles common SheikhGo PK local numbers (`03XXXXXXXXX` → `923XXXXXXXXX`)
/// and strips formatting while preserving an explicit country code.
String? whatsAppDigits(String? phone) {
  final raw = phone?.trim() ?? '';
  if (raw.isEmpty) return null;

  var digits = raw.replaceAll(RegExp(r'[^\d+]'), '');
  if (digits.startsWith('+')) {
    digits = digits.substring(1);
  }
  if (digits.startsWith('00')) {
    digits = digits.substring(2);
  }
  // Pakistan local mobile: 03XX-XXXXXXX (11 digits incl. leading 0).
  if (digits.length == 11 && digits.startsWith('0')) {
    digits = '92${digits.substring(1)}';
  }
  if (digits.length < 10) return null;
  return digits;
}

/// `tel:` URI for the device dialer. Keeps `+` when present; strips spaces/dashes.
Uri? telUri(String? phone) {
  final raw = phone?.trim() ?? '';
  if (raw.isEmpty) return null;
  final cleaned = raw.replaceAll(RegExp(r'[^\d+]'), '');
  if (cleaned.isEmpty) return null;
  return Uri.parse('tel:$cleaned');
}

Future<void> launchPhoneCall(String? phone) async {
  final uri = telUri(phone);
  if (uri == null) return;
  // Do not gate on canLaunchUrl — it returns false for tel: on many Android builds.
  await launchUrl(uri);
}

Future<void> launchWhatsApp(String? phone, {String? message}) async {
  final digits = whatsAppDigits(phone);
  if (digits == null) return;
  final query = message != null && message.trim().isNotEmpty
      ? '?text=${Uri.encodeComponent(message.trim())}'
      : '';
  final https = Uri.parse('https://wa.me/$digits$query');
  try {
    final ok = await launchUrl(https, mode: LaunchMode.externalApplication);
    if (ok) return;
  } catch (_) {
    // Fall through to native scheme.
  }
  final native = Uri.parse(
    'whatsapp://send?phone=$digits'
    '${message != null && message.trim().isNotEmpty ? '&text=${Uri.encodeComponent(message.trim())}' : ''}',
  );
  await launchUrl(native, mode: LaunchMode.externalApplication);
}

/// Compact Call / WhatsApp controls wired to the assigned driver's phone.
class VehicleCommsButtons extends StatelessWidget {
  const VehicleCommsButtons({
    super.key,
    required this.phone,
    this.emergencyPhone,
    this.vehicleLabel,
    this.compact = true,
  });

  final String? phone;
  final String? emergencyPhone;
  final String? vehicleLabel;

  /// When true (default), renders small icon buttons balanced with detail panels.
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final driverPhone = phone?.trim() ?? '';
    final canCall = telUri(driverPhone) != null;
    final canWhatsApp = whatsAppDigits(driverPhone) != null;
    final emergency = emergencyPhone?.trim() ?? '';
    final hasEmergency = telUri(emergency) != null;

    if (!canCall && !canWhatsApp && !hasEmergency) {
      return const SizedBox.shrink();
    }

    final msg = vehicleLabel != null ? 'Regarding $vehicleLabel' : null;

    if (compact) {
      return Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (canCall)
            _CommsIconButton(
              tooltip: 'Call driver',
              icon: Icons.call_outlined,
              color: AppColors.primary,
              onPressed: () => launchPhoneCall(driverPhone),
            ),
          if (canWhatsApp)
            _CommsIconButton(
              tooltip: 'WhatsApp driver',
              icon: Icons.chat_outlined,
              color: const Color(0xFF25D366),
              onPressed: () => launchWhatsApp(driverPhone, message: msg),
            ),
          if (hasEmergency)
            _CommsIconButton(
              tooltip: 'Emergency contact',
              icon: Icons.emergency_outlined,
              color: AppColors.error,
              onPressed: () => launchPhoneCall(emergency),
            ),
        ],
      );
    }

    return Row(
      children: [
        if (canCall)
          Expanded(
            child: OutlinedButton.icon(
              onPressed: () => launchPhoneCall(driverPhone),
              icon: const Icon(Icons.call_outlined, size: 16),
              label: const Text('Call'),
              style: OutlinedButton.styleFrom(
                visualDensity: VisualDensity.compact,
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              ),
            ),
          ),
        if (canWhatsApp) ...[
          if (canCall) const SizedBox(width: 8),
          Expanded(
            child: OutlinedButton.icon(
              onPressed: () => launchWhatsApp(driverPhone, message: msg),
              icon: const Icon(Icons.chat_outlined, size: 16),
              label: const Text('WhatsApp'),
              style: OutlinedButton.styleFrom(
                visualDensity: VisualDensity.compact,
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              ),
            ),
          ),
        ],
        if (hasEmergency) ...[
          const SizedBox(width: 8),
          Expanded(
            child: OutlinedButton.icon(
              onPressed: () => launchPhoneCall(emergency),
              icon: const Icon(Icons.emergency_outlined, size: 16),
              label: const Text('Emergency'),
              style: OutlinedButton.styleFrom(
                visualDensity: VisualDensity.compact,
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              ),
            ),
          ),
        ],
      ],
    );
  }
}

class _CommsIconButton extends StatelessWidget {
  const _CommsIconButton({
    required this.tooltip,
    required this.icon,
    required this.color,
    required this.onPressed,
  });

  final String tooltip;
  final IconData icon;
  final Color color;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return IconButton(
      tooltip: tooltip,
      onPressed: onPressed,
      visualDensity: VisualDensity.compact,
      padding: EdgeInsets.zero,
      constraints: const BoxConstraints(minWidth: 28, minHeight: 28),
      iconSize: 16,
      icon: Icon(icon, size: 16, color: color),
    );
  }
}
