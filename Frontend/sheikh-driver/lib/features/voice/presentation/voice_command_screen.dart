import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:speech_to_text/speech_to_text.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/offline/offline_models.dart';
import '../../../core/offline/offline_outbox.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/voice_command_api.dart';
import '../domain/voice_speech_locale.dart';
import 'voice_speech_prefs.dart';

class VoiceCommandScreen extends ConsumerStatefulWidget {
  const VoiceCommandScreen({super.key});

  @override
  ConsumerState<VoiceCommandScreen> createState() => _VoiceCommandScreenState();
}

class _VoiceCommandScreenState extends ConsumerState<VoiceCommandScreen> {
  final _speech = SpeechToText();
  final _manual = TextEditingController();
  VoiceSpeechLocale _locale = VoiceSpeechLocale.en;
  String? _intent; // null = auto
  bool _listening = false;
  bool _initializing = true;
  bool _available = false;
  String _partial = '';
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    _bootstrap();
  }

  Future<void> _bootstrap() async {
    final saved = await VoiceSpeechPrefs.loadLocale();
    _available = await _speech.initialize(
      onStatus: (s) {
        if (s == 'done' || s == 'notListening') {
          if (mounted) setState(() => _listening = false);
        }
      },
      onError: (_) {
        if (mounted) setState(() => _listening = false);
      },
    );
    if (mounted) {
      setState(() {
        _locale = saved;
        _initializing = false;
      });
    }
  }

  @override
  void dispose() {
    _manual.dispose();
    _speech.stop();
    super.dispose();
  }

  Future<void> _toggleListen() async {
    if (!_available) {
      _snack('Speech recognition is not available on this device.');
      return;
    }
    if (_listening) {
      await _speech.stop();
      setState(() => _listening = false);
      return;
    }

    setState(() {
      _listening = true;
      _partial = '';
    });

    await _speech.listen(
      listenOptions: SpeechListenOptions(
        localeId: _locale.speechTag,
        listenMode: ListenMode.confirmation,
      ),
      onResult: (result) {
        setState(() {
          _partial = result.recognizedWords;
          if (result.finalResult) {
            _manual.text = result.recognizedWords;
            _listening = false;
          }
        });
      },
    );
  }

  Future<void> _submit() async {
    final text = _manual.text.trim();
    if (text.isEmpty) {
      _snack('Say or type a message first.');
      return;
    }
    setState(() => _submitting = true);
    try {
      Position? pos;
      try {
        pos = await Geolocator.getLastKnownPosition();
      } catch (_) {}

      final clientOpId = OfflineOutbox.newId();
      final api = ref.read(voiceCommandApiProvider);
      final result = await api.submit(
        transcript: text,
        language: _locale,
        intent: _intent,
        clientOpId: clientOpId,
        latitude: pos?.latitude,
        longitude: pos?.longitude,
      );

      if (!mounted) return;
      if (result.queuedOffline) {
        _snack('Saved offline — will sync when connected.');
      } else {
        _snack(
          result.resolvedIntent.toLowerCase() == 'sos'
              ? 'SOS sent to dispatch.'
              : 'Sent (${result.resolvedIntent}).',
        );
        _manual.clear();
        setState(() => _partial = '');
      }
    } on OfflineQueuedException catch (e) {
      _snack(e.message);
    } catch (e) {
      _snack('$e');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  void _snack(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(msg), duration: const Duration(seconds: 3)),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('Voice commands'),
      ),
      body: _initializing
          ? const Center(child: SgBrandLoader(message: 'Preparing microphone…'))
          : ListView(
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 32),
              children: [
                Text(
                  'Speak in Arabic, English, or German to send a dispatch message or alert. '
                  'Uses on-device speech recognition when available.',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: context.sg.textSecondary,
                      ),
                ),
                const SizedBox(height: 20),
                Text('Recognition language',
                    style: Theme.of(context).textTheme.titleSmall),
                const SizedBox(height: 8),
                SegmentedButton<VoiceSpeechLocale>(
                  segments: VoiceSpeechLocale.values
                      .map(
                        (l) => ButtonSegment(
                          value: l,
                          label: Text(l.label,
                              style: const TextStyle(fontSize: 12)),
                        ),
                      )
                      .toList(),
                  selected: {_locale},
                  onSelectionChanged: (s) async {
                    final next = s.first;
                    setState(() => _locale = next);
                    await VoiceSpeechPrefs.saveLocale(next);
                  },
                ),
                const SizedBox(height: 16),
                Text('Send as', style: Theme.of(context).textTheme.titleSmall),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  children: [
                    ChoiceChip(
                      label: const Text('Auto'),
                      selected: _intent == null,
                      onSelected: (_) => setState(() => _intent = null),
                    ),
                    ChoiceChip(
                      label: const Text('Message'),
                      selected: _intent == 'message',
                      onSelected: (_) => setState(() => _intent = 'message'),
                    ),
                    ChoiceChip(
                      label: const Text('Alert'),
                      selected: _intent == 'alert',
                      onSelected: (_) => setState(() => _intent = 'alert'),
                    ),
                    ChoiceChip(
                      label: const Text('SOS'),
                      selected: _intent == 'sos',
                      onSelected: (_) => setState(() => _intent = 'sos'),
                    ),
                  ],
                ),
                const SizedBox(height: 24),
                Center(
                  child: Column(
                    children: [
                      Material(
                        color: _listening
                            ? AppColors.error.withValues(alpha: 0.15)
                            : AppColors.primary.withValues(alpha: 0.12),
                        shape: const CircleBorder(),
                        child: InkWell(
                          customBorder: const CircleBorder(),
                          onTap: _submitting ? null : _toggleListen,
                          child: SizedBox(
                            width: 96,
                            height: 96,
                            child: Icon(
                              _listening ? Icons.mic : Icons.mic_none_rounded,
                              size: 44,
                              color: _listening
                                  ? AppColors.error
                                  : AppColors.primary,
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        _listening ? 'Listening…' : 'Tap to speak',
                        style: TextStyle(
                          color: context.sg.textSecondary,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      if (_partial.isNotEmpty)
                        Padding(
                          padding: const EdgeInsets.only(top: 8),
                          child: Text(
                            _partial,
                            textAlign: TextAlign.center,
                            style: const TextStyle(fontStyle: FontStyle.italic),
                          ),
                        ),
                    ],
                  ),
                ),
                const SizedBox(height: 20),
                TextField(
                  controller: _manual,
                  maxLines: 4,
                  decoration: const InputDecoration(
                    labelText: 'Transcript',
                    hintText: 'Edit before sending',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 16),
                FilledButton.icon(
                  onPressed: _submitting ? null : _submit,
                  icon: _submitting
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.send_rounded),
                  label: Text(_submitting ? 'Sending…' : 'Send to dispatch'),
                ),
              ],
            ),
    );
  }
}
