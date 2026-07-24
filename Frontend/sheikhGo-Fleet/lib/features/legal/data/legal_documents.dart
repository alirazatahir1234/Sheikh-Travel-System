/// In-app legal copy (kept short). Full store docs live under `store/`.
class LegalDocuments {
  static const effectiveDate = '19 July 2026';

  static const privacyBody = '''
SheikhGo Fleet is the mobile app for authorized fleet drivers and operations staff.

We collect account details (phone, driver id), trip and attendance records, GPS location while on duty or on active trips, device integrity signals, photos for fuel/inspections/documents, push tokens, and crash diagnostics.

Data is used to authenticate you, run trips and navigation, share live location with your organization, record compliance activity, send operational alerts, and improve stability. We do not sell your data.

Background location supports trip tracking for dispatch. You can stop background GPS from Settings. Crash reports may include a driver identifier for support.

For access or correction requests, contact your fleet administrator or privacy@sheikhgo.local (replace before production). Full policy: store/PRIVACY_POLICY.md or the hosted PRIVACY_URL.
''';

  static const termsBody = '''
By using SheikhGo Fleet you agree to these terms and the Privacy Policy.

You must be an authorized driver of a SheikhGo customer organization. Use the app only for assigned fleet duties. Do not spoof location, tamper with the app, or misuse SOS.

Trip updates and GPS are shared with your organization. Offline actions sync when you are back online; resolve conflicts when prompted.

The service is provided as-is. Your organization or we may suspend access if credentials are revoked or security checks fail.

Contact legal@sheikhgo.local / support@sheikhgo.local (replace before production). Full terms: store/TERMS_OF_SERVICE.md or the hosted TERMS_URL.
''';
}
