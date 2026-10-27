/// Speech recognition locale (independent of app UI language).
enum VoiceSpeechLocale {
  en,
  ar,
  de;

  String get code => name;

  String get speechTag => switch (this) {
        VoiceSpeechLocale.en => 'en_US',
        VoiceSpeechLocale.ar => 'ar_SA',
        VoiceSpeechLocale.de => 'de_DE',
      };

  String get label => switch (this) {
        VoiceSpeechLocale.en => 'English',
        VoiceSpeechLocale.ar => 'العربية',
        VoiceSpeechLocale.de => 'Deutsch',
      };

  static VoiceSpeechLocale fromCode(String? raw) {
    final c = (raw ?? 'en').trim().toLowerCase();
    return VoiceSpeechLocale.values.firstWhere(
      (e) => e.code == c,
      orElse: () => VoiceSpeechLocale.en,
    );
  }
}
