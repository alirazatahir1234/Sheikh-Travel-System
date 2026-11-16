# sheikh-driver — Flutter app rules

Flutter · Dart · Riverpod · GoRouter · Dio · Hive · SignalR · Workmanager
The full mobile app, not a driver-only client.

## Build and test
- `flutter analyze` and `flutter test` must pass before you tell me you are done.

## API base URL
- From build flavour / remote config, NEVER a hardcoded IP. See SG-09 in the audit.

## Offline first
- Mobile networks drop. Queue writes in Hive, reconcile on reconnect, and show the
  user what is pending rather than failing silently.

## Freshness
- Telemetry shown in the app carries the same Live / Stale / Offline states as the ERP.
  A cached speed is never shown as current. See SG-04.

## Tenant and PII
- The app shows one tenant's data. Never send a tenant id the user did not authenticate
  into — tenant comes from the token.
- Never log or cache CNIC or exact customer coordinates to disk.

## Always
- Plan mode for anything touching more than three files.
- If a change needs an API change, say so — Backend/ is in this same repo and branch.
