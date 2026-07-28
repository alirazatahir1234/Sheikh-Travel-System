import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

Future<void> launchPhoneCall(String? phone) async {
  final raw = phone?.trim() ?? '';
  if (raw.isEmpty) return;
  final uri = Uri(scheme: 'tel', path: raw.replaceAll(RegExp(r'\s+'), ''));
  if (await canLaunchUrl(uri)) {
    await launchUrl(uri);
  }
}

Future<void> launchWhatsApp(String? phone, {String? message}) async {
  final raw = phone?.replaceAll(RegExp(r'[^\d+]'), '') ?? '';
  if (raw.isEmpty) return;
  final digits = raw.startsWith('+') ? raw.substring(1) : raw;
  final uri = Uri.parse(
    'https://wa.me/$digits${message != null ? '?text=${Uri.encodeComponent(message)}' : ''}',
  );
  if (await canLaunchUrl(uri)) {
    await launchUrl(uri, mode: LaunchMode.externalApplication);
  }
}

class VehicleCommsButtons extends StatelessWidget {
  const VehicleCommsButtons({
    super.key,
    required this.phone,
    this.emergencyPhone,
    this.vehicleLabel,
  });

  final String? phone;
  final String? emergencyPhone;
  final String? vehicleLabel;

  @override
  Widget build(BuildContext context) {
    final hasCall = (phone?.trim().isNotEmpty ?? false) ||
        (emergencyPhone?.trim().isNotEmpty ?? false);
    if (!hasCall) {
      return const SizedBox.shrink();
    }
    final msg = vehicleLabel != null ? 'Regarding $vehicleLabel' : null;
    return Row(
      children: [
        if (phone != null && phone!.trim().isNotEmpty)
          Expanded(
            child: OutlinedButton.icon(
              onPressed: () => launchPhoneCall(phone),
              icon: const Icon(Icons.phone_outlined, size: 18),
              label: const Text('Call driver'),
            ),
          ),
        if (phone != null &&
            phone!.trim().isNotEmpty &&
            emergencyPhone != null &&
            emergencyPhone!.trim().isNotEmpty)
          const SizedBox(width: 8),
        if (phone != null && phone!.trim().isNotEmpty)
          Expanded(
            child: OutlinedButton.icon(
              onPressed: () => launchWhatsApp(phone, message: msg),
              icon: const Icon(Icons.chat_outlined, size: 18),
              label: const Text('WhatsApp'),
            ),
          ),
        if (emergencyPhone != null && emergencyPhone!.trim().isNotEmpty) ...[
          const SizedBox(width: 8),
          Expanded(
            child: OutlinedButton.icon(
              onPressed: () => launchPhoneCall(emergencyPhone),
              icon: const Icon(Icons.emergency_outlined, size: 18),
              label: const Text('Emergency'),
            ),
          ),
        ],
      ],
    );
  }
}
