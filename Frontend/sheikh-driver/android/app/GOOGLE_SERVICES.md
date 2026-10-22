# FCM / `google-services.json` setup (SheikhGo Tracking)

**Status:** Push cannot obtain an FCM token until this file exists. Do **not** invent fake Firebase keys — Gradle/runtime will break.

`google-services.json` is **gitignored** and is not in this repo. Place your real Firebase Android config locally (or from your private secrets store).

## Why push fails

1. `android/app/google-services.json` is missing → `Firebase.initializeApp()` fails at startup.
2. Package / applicationId must be **`com.sheikhgo.fleet`** (must match the Firebase Android app).
3. Emulator without **Google Play** services → FCM `getToken()` fails (`SERVICE_NOT_AVAILABLE`), even when Firebase init succeeded.

## Steps for Ali (Firebase Console)

1. Open [Firebase Console](https://console.firebase.google.com/) → select or create the **SheikhGo / Sheikh Travel** Android project.
2. Add Android app (or open existing) with package name **`com.sheikhgo.fleet`**.
3. Download **`google-services.json`**.
4. Place it at:
   ```text
   Frontend/sheikh-driver/android/app/google-services.json
   ```
5. Rebuild (clean recommended):
   ```bash
   cd Frontend/sheikh-driver
   flutter clean && flutter pub get
   flutter run -d <device>
   ```
6. Settings → enable **Push notifications** → allow OS permission when prompted.
7. Confirm logcat shows an FCM token and API `POST .../ai/device-tokens` succeeds.

## Emulator requirements (FCM)

Use an AVD that includes **Google Play** (Play Store icon in the AVD Manager), for example:

- Pixel 6 / Pixel 7 — system image **Google Play** (API 34+)
- Not: “Google APIs” only, AOSP, or images without Play Services

Physical devices with Play Store also work.

After placing `google-services.json` and using a Play-capable AVD:

- Login should **not** show a scary push popup when config is still missing (hint stays in Settings).
- With config + Play Services present, login registers the token silently; Settings toggle retries permission/token/API registration.

## Optional: Firebase CLI

```bash
npm i -g firebase-tools
firebase login
firebase apps:sdkconfig android <APP_ID> > android/app/google-services.json
```

(`gcloud` / `firebase` must be authenticated on this machine first.)

## After the file is present

- `build.gradle.kts` auto-applies the `com.google.gms.google-services` plugin when the JSON exists.
- Do **not** commit secrets if policy forbids it; keep the file local or in a private secrets store.
