# SheikhGo Driver — Privacy Policy

**Effective date:** 19 July 2026  
**App:** SheikhGo Fleet (`com.sheikhgo.fleet`)  
**Operator:** SheikhGo / fleet tenant administrators using the SheikhGo platform

This policy describes how the SheikhGo Driver mobile application collects, uses, and shares information when drivers use the app for fleet operations.

## 1. Who this applies to

SheikhGo Driver is a **workforce / fleet operations** app for authorized drivers of organizations that use SheikhGo. It is not a consumer rideshare marketplace app for the general public.

## 2. Data we collect

| Category | Examples | Purpose |
|----------|----------|---------|
| Account | Phone number, driver id, tenant/branch affiliation | Authentication and access control |
| Trip & operations | Assigned trips, status changes, attendance, fuel receipts, inspections, documents metadata | Dispatch and compliance |
| Location | GPS coordinates while on duty / during active trip tracking | Live tracking, navigation, ETA, safety |
| Device | Device id, OS, model, app version, installer, integrity signals | Security, push delivery, support |
| Media | Photos of fuel receipts, inspection images, document scans | Operational records |
| Diagnostics | Crash reports, performance events (via Firebase Crashlytics / Analytics) | Stability and product improvement |
| Notifications | Push tokens, notification read state | Operational alerts |

## 3. How we use data

- Authenticate drivers and enforce tenant isolation  
- Assign and track trips; share location with the driver’s organization (dispatch / ERP)  
- Record attendance, fuel, inspections, and compliance documents  
- Send push notifications about trips, payments, and safety (including SOS)  
- Detect compromised or tampered devices in production builds  
- Diagnose crashes and improve reliability  

## 4. Legal bases / employer context

Processing is performed on behalf of the **fleet operator (tenant)** that employs or contracts the driver. Drivers should also refer to their employer’s workplace privacy notices where applicable.

## 5. Sharing

We do **not** sell personal data. Data may be shared with:

- The driver’s organization via the SheikhGo ERP / API  
- Infrastructure providers (hosting, maps, push, crash reporting) under contract  
- Authorities when required by law or to respond to emergencies (e.g. SOS)

## 6. Location

Background location is used **only** to support active trip / duty tracking for fleet operations. Drivers can stop background GPS from Settings when not needed. Denying location permission limits navigation and live tracking features.

## 7. Retention

Operational records are retained according to the tenant’s configuration and applicable law. Crash diagnostics are retained per Firebase product defaults unless configured otherwise.

## 8. Security

- TLS for API traffic; optional certificate pinning in production  
- Tokens stored in platform secure storage  
- Device integrity checks (root / jailbreak / emulator / tamper) in production releases  

## 9. Children’s privacy

The app is not directed at children under 16.

## 10. Your choices

- Request access or correction via your fleet administrator  
- Sign out to clear the local session  
- Disable optional permissions (camera, notifications) in system settings (some features will stop working)  
- Contact: **privacy@sheikhgo.local** (replace with your production address before store submission)

## 11. Changes

We may update this policy. Material changes will be reflected by updating the effective date and in-app copy. Continued use after an update constitutes acceptance where permitted by law.

## 12. Contact

**Privacy:** privacy@sheikhgo.local  
**Support:** support@sheikhgo.local  

> Replace placeholder emails and legal entity name with your registered company details before Play Store / App Store submission.
