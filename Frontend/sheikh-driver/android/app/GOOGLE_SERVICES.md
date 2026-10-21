# FCM / `google-services.json` setup (SheikhGo Tracking)

**Status:** Push toggle stays off until this file exists. Do **not** invent fake Firebase keys — Gradle/runtime will break.

## Why the Settings toggle fails

1. `android/app/google-services.json` is missing (gitignored; not in this repo).
2. Without it, `Firebase.initializeApp()` fails at startup and no FCM token can be issued.
3. Package / applicationId: **`com.sheikhgo.fleet`**.

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
7. Confirm logcat shows an FCM token and API `POST .../device-token` succeeds.

## Optional: Firebase CLI

```bash
npm i -g firebase-tools
firebase login
firebase apps:sdkconfig android <APP_ID> > android/app/google-services.json
```

(`gcloud` on this machine had expired auth — run `gcloud auth login` / `firebase login` first.)

## After the file is present

- `build.gradle.kts` auto-applies the `com.google.gms.google-services` plugin when the JSON exists.
- Do **not** commit secrets if policy forbids it; keep the file local or in a private secrets store.
