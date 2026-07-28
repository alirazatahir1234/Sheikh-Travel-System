Playback marker assets directory.

Reserved for dedicated playback icons (vehicle arrow, start, finish, stop, and event markers).
Current implementation generates marker bitmaps at runtime from semantic marker definitions in:

- `lib/features/fleet/presentation/playback/playback_map_builder.dart`

This keeps marker semantics consistent while avoiding accidental reuse of branding assets.
