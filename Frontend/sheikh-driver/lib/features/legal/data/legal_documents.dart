/// In-app legal copy (kept readable on-device). Full store docs live under `store/`.
class LegalDocuments {
  static const effectiveDate = '19 July 2026';

  static const privacyBody = '''
SheikhGo Tracking is the mobile app for company owners, fleet staff, and authorized drivers of organizations that use the SheikhGo platform. It is not a consumer rideshare marketplace.

We collect:
• Account details (phone, driver id, tenant/branch)
• Trip, attendance, fuel, inspection, and document records
• GPS location while on duty or on active trips (including background tracking when enabled)
• Device integrity signals, push tokens, and crash diagnostics
• Photos for fuel receipts, inspections, and compliance documents

Data is used to authenticate you, run trips and navigation, share live location with your organization, record compliance activity, send operational alerts, and improve stability. We do not sell your data.

Background location supports trip tracking for dispatch. You can stop background GPS from Settings. Crash reports may include a driver identifier for support.

Processing is performed on behalf of your fleet operator (tenant). For access or correction requests, contact your fleet administrator.

Full policy: store/PRIVACY_POLICY.md or the hosted PRIVACY_URL when configured.
''';

  static const termsBody = '''
By installing or using SheikhGo Tracking you agree to these Terms and the Privacy Policy. If you do not agree, do not use the app.

You must be an authorized driver or contractor of a SheikhGo customer organization, with credentials issued by that organization. Use the app only for assigned fleet duties.

You agree to:
• Keep credentials confidential and use accurate identity
• Use location, camera, and related permissions only for legitimate trip or compliance tasks
• Not spoof location, tamper with the app, or circumvent security controls
• Follow your employer’s safety and driving policies

Trip updates and GPS points are shared with your organization for operations and safety. SOS alerts may notify dispatch. Offline actions (attendance, fuel, trips, inspections, documents) sync when you reconnect; resolve conflicts when prompted.

The service is provided as-is. Your organization or we may suspend access if credentials are revoked, security checks fail, or these Terms are violated.

To the maximum extent permitted by law, SheikhGo and its affiliates are not liable for indirect or consequential damages arising from use of the app.

Full terms: store/TERMS_OF_SERVICE.md or the hosted TERMS_URL when configured.
''';
}
