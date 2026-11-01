/// In-app legal copy (kept readable on-device). Full store docs live under `store/`.
/// Wording mirrors approved `store/PRIVACY_POLICY.md` and `store/TERMS_OF_SERVICE.md`.
class LegalSection {
  const LegalSection({
    required this.title,
    required this.paragraphs,
    this.bullets = const [],
    this.infoTitle,
    this.infoBody,
  });

  final String title;
  final List<String> paragraphs;
  final List<String> bullets;
  final String? infoTitle;
  final String? infoBody;
}

class LegalDocuments {
  static const effectiveDate = '19 July 2026';
  static const privacyVersion = '1.0';
  static const termsVersion = '1.0';

  static const List<LegalSection> privacySections = [
    LegalSection(
      title: 'About SheikhGo Tracking',
      paragraphs: [
        'SheikhGo Tracking is a workforce / fleet operations app for authorized drivers of organizations that use SheikhGo. It is not a consumer rideshare marketplace app for the general public.',
        'This policy describes how the SheikhGo Tracking mobile application collects, uses, and shares information when drivers use the app for fleet operations.',
      ],
    ),
    LegalSection(
      title: 'Information We Collect',
      paragraphs: [
        'We collect the following categories of information when you use the app:',
      ],
      bullets: [
        'Account — phone number, driver id, tenant/branch affiliation for authentication and access control',
        'Trip & operations — assigned trips, status changes, attendance, fuel receipts, inspections, documents metadata for dispatch and compliance',
        'Location — GPS coordinates while on duty / during active trip tracking for live tracking, navigation, ETA, and safety',
        'Device — device id, OS, model, app version, installer, integrity signals for security, push delivery, and support',
        'Media — photos of fuel receipts, inspection images, document scans for operational records',
        'Diagnostics — crash reports and performance events (via Firebase Crashlytics / Analytics) for stability and product improvement',
        'Notifications — push tokens and notification read state for operational alerts',
      ],
    ),
    LegalSection(
      title: 'How We Use Your Data',
      paragraphs: [
        'Data is used to operate fleet workflows for your organization. We do not sell personal data.',
      ],
      bullets: [
        'Authenticate drivers and enforce tenant isolation',
        'Assign and track trips; share location with the driver’s organization (dispatch / ERP)',
        'Record attendance, fuel, inspections, and compliance documents',
        'Send push notifications about trips, payments, and safety (including SOS)',
        'Detect compromised or tampered devices in production builds',
        'Diagnose crashes and improve reliability',
      ],
      infoTitle: 'How we use your data',
      infoBody:
          'Processing is performed on behalf of the fleet operator (tenant) that employs or contracts the driver. Drivers should also refer to their employer’s workplace privacy notices where applicable.',
    ),
    LegalSection(
      title: 'GPS and Background Location',
      paragraphs: [
        'Background location is used only to support active trip / duty tracking for fleet operations. Drivers can stop background GPS from Settings when not needed. Denying location permission limits navigation and live tracking features.',
      ],
    ),
    LegalSection(
      title: 'Data Sharing',
      paragraphs: [
        'We do not sell personal data. Data may be shared with:',
      ],
      bullets: [
        'The driver’s organization via the SheikhGo ERP / API',
        'Infrastructure providers (hosting, maps, push, crash reporting) under contract',
        'Authorities when required by law or to respond to emergencies (e.g. SOS)',
      ],
    ),
    LegalSection(
      title: 'Data Security and Retention',
      paragraphs: [
        'Operational records are retained according to the tenant’s configuration and applicable law. Crash diagnostics are retained per Firebase product defaults unless configured otherwise.',
      ],
      bullets: [
        'TLS for API traffic; optional certificate pinning in production',
        'Tokens stored in platform secure storage',
        'Device integrity checks (root / jailbreak / emulator / tamper) in production releases',
      ],
    ),
    LegalSection(
      title: 'User Privacy Rights',
      paragraphs: [
        'The app is not directed at children under 16.',
      ],
      bullets: [
        'Request access or correction via your fleet administrator',
        'Sign out to clear the local session',
        'Disable optional permissions (camera, notifications) in system settings (some features will stop working)',
      ],
    ),
    LegalSection(
      title: 'Contact Information',
      paragraphs: [
        'We may update this policy. Material changes will be reflected by updating the effective date and in-app copy. Continued use after an update constitutes acceptance where permitted by law.',
        'For privacy requests, contact your fleet administrator or the privacy contact configured for your deployment.',
      ],
    ),
  ];

  static const List<LegalSection> termsSections = [
    LegalSection(
      title: 'Acceptance of Terms',
      paragraphs: [
        'By installing or using SheikhGo Tracking, you agree to these Terms and the Privacy Policy. If you do not agree, do not use the app.',
      ],
    ),
    LegalSection(
      title: 'Authorized Usage',
      paragraphs: [
        'You must be an authorized driver (or contractor) of an organization that uses the SheikhGo platform, with valid credentials issued by that organization.',
        'We grant you a limited, non-exclusive, non-transferable license to use the app solely for performing assigned fleet duties. You may not reverse engineer, redistribute, or misuse the app.',
      ],
    ),
    LegalSection(
      title: 'Account Security',
      paragraphs: [
        'You agree to use accurate credentials and keep them confidential. Your organization or we may suspend access if credentials are revoked, security checks fail, or these Terms are violated. You may stop using the app at any time by signing out and uninstalling.',
      ],
    ),
    LegalSection(
      title: 'Location Permissions',
      paragraphs: [
        'You agree to use location, camera, and other permissions only for legitimate trip / compliance tasks, and not to spoof location, tamper with the app, or circumvent security controls.',
      ],
    ),
    LegalSection(
      title: 'Trip and Attendance Responsibilities',
      paragraphs: [
        'Trip status updates and GPS points are shared with your organization for operations and safety. SOS alerts may notify dispatch and related systems. Misuse of SOS may result in disciplinary action by your employer.',
        'You agree to follow your employer’s safety and driving policies while using the app.',
      ],
    ),
    LegalSection(
      title: 'Offline Synchronization',
      paragraphs: [
        'Actions queued offline (attendance, fuel, trips, inspections, documents) sync when connectivity returns. You remain responsible for resolving conflicts flagged by the app.',
      ],
    ),
    LegalSection(
      title: 'Security and Misuse',
      paragraphs: [
        'You agree not to spoof location, tamper with the app, or circumvent security controls. SheikhGo branding, software, and related materials remain the property of their respective owners.',
      ],
    ),
    LegalSection(
      title: 'Service Availability',
      paragraphs: [
        'The service is provided “as is.” We and your organization strive for uptime but do not guarantee uninterrupted access. Navigation depends on maps providers and device GPS.',
      ],
    ),
    LegalSection(
      title: 'Liability and Limitations',
      paragraphs: [
        'To the maximum extent permitted by law, SheikhGo and its affiliates are not liable for indirect or consequential damages arising from use of the app. Nothing in these Terms excludes liability that cannot be excluded by law.',
        'Governed by the laws applicable to the operating entity of your SheikhGo deployment.',
      ],
      infoTitle: 'Service and liability',
      infoBody:
          'The service is provided as-is. Your organization or we may suspend access if credentials are revoked, security checks fail, or these Terms are violated. Liability is limited as described above to the maximum extent permitted by law.',
    ),
  ];

  /// Flat bodies retained for callers that still expect a single string.
  static String get privacyBody =>
      privacySections.map(_flattenSection).join('\n\n');

  static String get termsBody =>
      termsSections.map(_flattenSection).join('\n\n');

  static String _flattenSection(LegalSection s) {
    final buf = StringBuffer(s.title);
    for (final p in s.paragraphs) {
      buf.write('\n$p');
    }
    for (final b in s.bullets) {
      buf.write('\n• $b');
    }
    return buf.toString();
  }
}
