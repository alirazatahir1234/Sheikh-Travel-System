import 'package:hive_flutter/hive_flutter.dart';
import '../domain/voice_speech_locale.dart';

const _voiceLocaleKey = 'voice_speech_locale';

abstract final class VoiceSpeechPrefs {
  static Future<VoiceSpeechLocale> loadLocale() async {
    final box = Hive.box('prefs');
    return VoiceSpeechLocale.fromCode(box.get(_voiceLocaleKey) as String?);
  }

  static Future<void> saveLocale(VoiceSpeechLocale locale) async {
    final box = Hive.box('prefs');
    await box.put(_voiceLocaleKey, locale.code);
  }
}
