# SheikhGo Tracking — Store Release Checklist

## Pre-flight (engineering)

- [ ] `flutter test` green on CI
- [ ] `flutter analyze` clean enough for release
- [ ] Version bumped in `pubspec.yaml` (`x.y.z+build`)
- [ ] `store/RELEASE_NOTES.md` updated for this build
- [ ] Prod API: `ENV=prod` — default is Azure Caddy `https://20-174-1-230.sslip.io/api` (`AppConfig.defaultProductionApiBaseUrl` / `scripts/build_prod.sh`; same as ERP). Confirm `curl -sS https://20-174-1-230.sslip.io/health` → `Healthy` before store upload.
- [ ] Cert pins set: `CERT_PIN_1` / `CERT_PIN_2` (recommended for store; without pins HTTPS still works)
- [ ] Maps: `android/maps.properties` (or `GOOGLE_MAPS_API_KEY` env) present before `./scripts/build_prod.sh` — blank map otherwise
- [ ] `GOOGLE_MAPS_KEY` / Maps key restricted to app package / bundle id + release SHA-1
- [ ] Firebase: `google-services.json` + `GoogleService-Info.plist` for **prod** project (see `android/app/GOOGLE_SERVICES.md`) — missing file disables FCM but app still launches
- [ ] Crashlytics enabled in release (`!kDebugMode`); `env` custom key set on login
- [ ] Privacy Policy + Terms hosted on **public HTTPS** URLs
- [ ] In-app Settings → Legal opens correct hosted URLs (`PRIVACY_URL` / `TERMS_URL`)
- [ ] Remove debug logging of tokens / PII
- [ ] Background location justifications match store forms
- [ ] **Emulator QA:** use debug (`./scripts/run_android_emu.sh`). Prod release AABs block emulators (`BLOCK_EMULATORS`).

## Android (Play Store)

- [ ] Create upload keystore; fill `android/key.properties` (never commit)
- [ ] Confirm release `signingConfig` uses release keystore
- [ ] Build App Bundle:

```bash
cd Frontend/sheikh-driver
# Preferred (injects Maps key from android/maps.properties when present):
./scripts/build_prod.sh android
# Or:
flutter build appbundle --release \
  --dart-define=ENV=prod \
  --dart-define=API_BASE_URL=https://20-174-1-230.sslip.io/api \
  --dart-define=TENANT_SLUG=default \
  --dart-define=GOOGLE_MAPS_KEY=YOUR_KEY \
  --dart-define=CERT_PIN_1=YOUR_SHA256_HEX \
  --dart-define=PRIVACY_URL=https://example.com/driver/privacy \
  --dart-define=TERMS_URL=https://example.com/driver/terms
```

- [ ] Sideload APK smoke-test (no Mac / local API): `./scripts/run_prod_device.sh`
- [ ] Upload to Play Console → Internal testing → then Closed → Production
- [ ] Complete Data safety form (see `STORE_LISTING.md`)
- [ ] Declare foreground service + background location use cases
- [ ] Target API level meets Play requirements
- [ ] App signing by Google Play enabled

## iOS (TestFlight / App Store)

- [ ] Apple Developer team + App ID `com.sheikhgo.fleet`
- [ ] Push capability + APNs key in Firebase
- [ ] Privacy Nutrition Labels match `STORE_LISTING.md`
- [ ] Location purpose strings accurate in `Info.plist`
- [ ] `PrivacyInfo.xcprivacy` present under `ios/Runner/`
- [ ] Archive in Xcode or:

```bash
./scripts/build_prod.sh ios
# Or:
flutter build ipa --release \
  --dart-define=ENV=prod \
  --dart-define=API_BASE_URL=https://20-174-1-230.sslip.io/api \
  --dart-define=TENANT_SLUG=default \
  --dart-define=CERT_PIN_1=YOUR_SHA256_HEX \
  --dart-define=PRIVACY_URL=https://example.com/driver/privacy \
  --dart-define=TERMS_URL=https://example.com/driver/terms
```

- [ ] USB release smoke-test (no TestFlight): `./scripts/run_ios_prod_device.sh`
- [ ] Upload to TestFlight; smoke-test on physical device
- [ ] Submit for App Review with demo driver credentials in ASC notes

## Store assets

- [ ] Screenshots per `SCREENSHOTS.md`
- [ ] Feature graphic (Play)
- [ ] Listing copy from `STORE_LISTING.md`
- [ ] Support + privacy URLs live

## Post-release

- [ ] Monitor Crashlytics for 48h (filter by `env=PROD`)
- [ ] Verify Analytics events in DebugView then production
- [ ] Confirm FCM on prod builds
- [ ] Tag git `driver-v1.0.0` (optional)
