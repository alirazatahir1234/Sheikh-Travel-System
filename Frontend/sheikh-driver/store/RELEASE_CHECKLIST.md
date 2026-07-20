# SheikhGo Driver — Store Release Checklist

## Pre-flight (engineering)

- [ ] `flutter test` green on CI
- [ ] `flutter analyze` clean enough for release
- [ ] Version bumped in `pubspec.yaml` (`x.y.z+build`)
- [ ] `store/RELEASE_NOTES.md` updated for this build
- [ ] Prod API URL + tenant via `--dart-define`
- [ ] Cert pins set: `CERT_PIN_1` / `CERT_PIN_2` (prod/UAT release)
- [ ] `ENV=prod` for store builds
- [ ] `GOOGLE_MAPS_API_KEY` restricted to app package / bundle id
- [ ] Firebase: `google-services.json` + `GoogleService-Info.plist` for **prod** project
- [ ] Crashlytics enabled in release (`!kDebugMode`)
- [ ] Privacy Policy + Terms hosted on **public HTTPS** URLs
- [ ] In-app Settings → Legal opens correct hosted URLs (`PRIVACY_URL` / `TERMS_URL`)
- [ ] Remove debug logging of tokens / PII
- [ ] Background location justifications match store forms

## Android (Play Store)

- [ ] Create upload keystore; fill `android/key.properties` (never commit)
- [ ] Confirm release `signingConfig` uses release keystore
- [ ] Build App Bundle:

```bash
cd Frontend/sheikh-driver
flutter build appbundle --release \
  --dart-define=ENV=prod \
  --dart-define=API_BASE_URL=https://api.example.com/api \
  --dart-define=TENANT_SLUG=default \
  --dart-define=CERT_PIN_1=YOUR_SHA256_HEX \
  --dart-define=PRIVACY_URL=https://example.com/driver/privacy \
  --dart-define=TERMS_URL=https://example.com/driver/terms
```

- [ ] Upload to Play Console → Internal testing → then Closed → Production
- [ ] Complete Data safety form (see `STORE_LISTING.md`)
- [ ] Declare foreground service + background location use cases
- [ ] Target API level meets Play requirements
- [ ] App signing by Google Play enabled

## iOS (TestFlight / App Store)

- [ ] Apple Developer team + App ID `com.sheikhgo.driver`
- [ ] Push capability + APNs key in Firebase
- [ ] Privacy Nutrition Labels match `STORE_LISTING.md`
- [ ] Location purpose strings accurate in `Info.plist`
- [ ] `PrivacyInfo.xcprivacy` present under `ios/Runner/`
- [ ] Archive in Xcode or:

```bash
flutter build ipa --release \
  --dart-define=ENV=prod \
  --dart-define=API_BASE_URL=https://api.example.com/api \
  --dart-define=TENANT_SLUG=default \
  --dart-define=CERT_PIN_1=YOUR_SHA256_HEX \
  --dart-define=PRIVACY_URL=https://example.com/driver/privacy \
  --dart-define=TERMS_URL=https://example.com/driver/terms
```

- [ ] Upload to TestFlight; smoke-test on physical device
- [ ] Submit for App Review with demo driver credentials in ASC notes

## Store assets

- [ ] Screenshots per `SCREENSHOTS.md`
- [ ] Feature graphic (Play)
- [ ] Listing copy from `STORE_LISTING.md`
- [ ] Support + privacy URLs live

## Post-release

- [ ] Monitor Crashlytics for 48h
- [ ] Verify Analytics events in DebugView then production
- [ ] Confirm FCM on prod builds
- [ ] Tag git `driver-v1.0.0` (optional)
