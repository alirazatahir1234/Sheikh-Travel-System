import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:speech_to_text/speech_recognition_error.dart';
import 'package:speech_to_text/speech_to_text.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../data/ai_chat_api.dart';
import '../domain/ai_assistant_catalog.dart';
import '../domain/ai_assistant_theme.dart';
import '../domain/ai_chat_models.dart';
import 'widgets/ai_assistant_app_bar.dart';
import 'widgets/ai_chat_bubble.dart';

class AiAssistantChatScreen extends ConsumerStatefulWidget {
  const AiAssistantChatScreen({super.key, this.initialPrompt});

  /// Opens chat and sends when set (home, dashboard `/ai?q=...`, feature cards).
  final String? initialPrompt;

  @override
  ConsumerState<AiAssistantChatScreen> createState() =>
      _AiAssistantChatScreenState();
}

class _AiAssistantChatScreenState extends ConsumerState<AiAssistantChatScreen> {
  /// Content column cap so tablets/desktop don't stretch bubbles edge to edge.
  static const _maxContentWidth = 820.0;
  static const _maxRecording = Duration(seconds: 60);

  final _draftCtrl = TextEditingController();
  final _scrollCtrl = ScrollController();
  final _focusNode = FocusNode();
  final _speech = SpeechToText();

  bool _sending = false;
  CancelToken? _chatCancel;
  CancelToken? _bootCancel;
  String? _sessionId;
  String? _pendingConfirmMessage;
  String? _lastFailedText;
  List<AiChatBubble> _messages = [];
  List<AiChatSession> _sessions = [];
  List<String> _suggestions = List.of(defaultAiSuggestions);
  AiProviderHealth? _health;
  String? _error;

  bool _speechReady = false;
  bool _recording = false;
  String _draftBeforeRecording = '';
  Duration _recordElapsed = Duration.zero;
  Timer? _recordTimer;
  bool _autoSentInitial = false;

  @override
  void initState() {
    super.initState();
    _bootstrap();
    final q = widget.initialPrompt?.trim();
    if (q != null && q.isNotEmpty) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _sendInitialIfNeeded(q));
    }
  }

  Future<void> _sendInitialIfNeeded(String q) async {
    if (_autoSentInitial || !mounted) return;
    _autoSentInitial = true;
    await _send(overrideText: q);
  }

  @override
  void dispose() {
    _chatCancel?.cancel();
    _bootCancel?.cancel();
    _recordTimer?.cancel();
    if (_recording) _speech.cancel();
    _draftCtrl.dispose();
    _scrollCtrl.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  /// Health + sessions load in the background — the chat is usable at once and
  /// a slow/unreachable provider can never pin the screen behind a loader.
  Future<void> _bootstrap() async {
    _bootCancel?.cancel();
    final cancel = CancelToken();
    _bootCancel = cancel;
    setState(() => _error = null);
    final api = ref.read(aiChatApiProvider);

    unawaited(api
        .getProviderHealth(cancelToken: cancel)
        .catchError((_) => const AiProviderHealth(
              provider: 'Unknown',
              configured: false,
              reachable: false,
              statusMessage: 'Could not load provider status.',
            ))
        .then((h) {
      if (mounted && !cancel.isCancelled) setState(() => _health = h);
    }));

    unawaited(api
        .listSessions(cancelToken: cancel)
        .catchError((_) => <AiChatSession>[])
        .then((s) {
      if (mounted && !cancel.isCancelled) setState(() => _sessions = s);
    }));
  }

  /// Re-read last assistant row so widgets appear when backend persisted payload.
  Future<void> _syncStructuredFromHistory() async {
    final sid = _sessionId;
    if (sid == null || sid.isEmpty || !mounted) return;
    try {
      final rows = await ref.read(aiChatApiProvider).getMessages(sid);
      final assistants = rows.where((m) => m.isAssistant).toList();
      if (assistants.isEmpty) return;
      final last = assistants.last;
      if (last.structuredPayload == null && last.toolUsed == null) return;
      if (!mounted) return;
      setState(() {
        for (var i = _messages.length - 1; i >= 0; i--) {
          if (_messages[i].isUser) continue;
          _messages = [
            ..._messages.sublist(0, i),
            _messages[i].copyWith(
              structuredPayload: last.structuredPayload,
              toolUsed: last.toolUsed,
              createdAt: last.createdAt ?? _messages[i].createdAt,
            ),
            ..._messages.sublist(i + 1),
          ];
          break;
        }
      });
    } catch (_) {}
  }

  Future<void> _refreshSessions() async {
    try {
      final sessions = await ref.read(aiChatApiProvider).listSessions();
      if (mounted) setState(() => _sessions = sessions);
    } catch (_) {}
  }

  void _newChat() {
    _chatCancel?.cancel();
    setState(() {
      _sessionId = null;
      _messages = [];
      _pendingConfirmMessage = null;
      _lastFailedText = null;
      _draftCtrl.clear();
      _error = null;
      _suggestions = List.of(defaultAiSuggestions);
    });
  }

  Future<void> _openSession(AiChatSession session) async {
    _chatCancel?.cancel();
    setState(() {
      _sessionId = session.id;
      _messages = [];
      _pendingConfirmMessage = null;
      _error = null;
    });
    try {
      final rows = await ref.read(aiChatApiProvider).getMessages(session.id);
      if (!mounted || _sessionId != session.id) return;
      setState(() {
        _messages = rows
            .where((m) => m.isUser || m.isAssistant)
            .map(
              (m) => AiChatBubble(
                role: m.isUser ? 'user' : 'assistant',
                content: m.content,
                structuredPayload: m.structuredPayload,
                toolUsed: m.toolUsed,
                createdAt: m.createdAt,
              ),
            )
            .toList();
      });
      _scrollToBottom();
    } catch (e) {
      if (mounted) setState(() => _error = AiChatApi.describeError(e));
    }
  }

  Future<void> _send({
    String? overrideText,
    bool confirmWrite = false,
    bool isRetry = false,
  }) async {
    if (_recording) await _stopRecording();
    final text = (overrideText ?? _draftCtrl.text).trim();
    if (text.isEmpty || _sending) return;

    if (!confirmWrite && !isRetry) {
      setState(() {
        _messages = [
          ..._messages,
          AiChatBubble(
            role: 'user',
            content: text,
            createdAt: DateTime.now().toUtc(),
          ),
        ];
        _draftCtrl.clear();
        _pendingConfirmMessage = null;
      });
    }

    final cancel = CancelToken();
    _chatCancel = cancel;
    setState(() {
      _sending = true;
      _error = null;
      _lastFailedText = null;
    });
    _scrollToBottom();

    try {
      final res = await ref.read(aiChatApiProvider).chat(
            message: text,
            sessionId: _sessionId,
            confirmWrite: confirmWrite,
            cancelToken: cancel,
          );
      if (!mounted || cancel.isCancelled) return;
      setState(() {
        _sessionId = res.sessionId;
        if (res.suggestedPrompts.isNotEmpty) {
          _suggestions = res.suggestedPrompts;
        }
        _messages = [
          ..._messages,
          AiChatBubble(
            role: 'assistant',
            content: res.answer,
            mode: res.mode,
            usedLlm: res.usedLlm,
            tools: res.displayTools,
            pendingConfirmation: res.pendingConfirmation,
            createdAt: DateTime.now().toUtc(),
          ),
        ];
        _pendingConfirmMessage = res.pendingConfirmation ? text : null;
      });
      unawaited(_syncStructuredFromHistory());
      unawaited(_refreshSessions());
      _scrollToBottom();
    } catch (e) {
      if (!mounted) return;
      final cancelled = e is DioException && e.type == DioExceptionType.cancel;
      setState(() {
        _error = cancelled ? null : AiChatApi.describeError(e);
        _lastFailedText = cancelled || confirmWrite ? null : text;
      });
    } finally {
      if (identical(_chatCancel, cancel)) _chatCancel = null;
      if (mounted && identical(_chatCancel, null)) {
        setState(() => _sending = false);
      }
    }
  }

  void _cancelSending() {
    _chatCancel?.cancel('user');
  }

  Future<void> _confirmWrite() async {
    final msg = _pendingConfirmMessage;
    if (msg == null) return;
    await _send(overrideText: msg, confirmWrite: true);
  }

  // ── Voice input (on-device speech-to-text into the composer) ────────────

  Future<bool> _ensureSpeech() async {
    if (_speechReady) return true;
    final mic = await Permission.microphone.request();
    if (!mic.isGranted) {
      _snack(
        'Microphone permission is needed for voice questions.',
        action: mic.isPermanentlyDenied
            ? const SnackBarAction(label: 'Settings', onPressed: openAppSettings)
            : null,
      );
      return false;
    }
    try {
      _speechReady = await _speech.initialize(
        onStatus: (s) {
          if ((s == 'done' || s == 'notListening') && _recording) {
            _finishRecording();
          }
        },
        onError: (SpeechRecognitionError err) {
          if (!_recording) return;
          _finishRecording();
          if (err.errorMsg != 'error_no_match' &&
              err.errorMsg != 'error_speech_timeout') {
            _snack('Voice input stopped: ${err.errorMsg}');
          }
        },
      );
    } catch (_) {
      _speechReady = false;
    }
    if (!_speechReady) {
      _snack('Speech recognition is not available on this device.');
    }
    return _speechReady;
  }

  Future<void> _startRecording() async {
    if (_sending || _recording) return;
    if (!await _ensureSpeech() || !mounted) return;
    _focusNode.unfocus();
    _draftBeforeRecording = _draftCtrl.text.trim();
    setState(() {
      _recording = true;
      _recordElapsed = Duration.zero;
    });
    _recordTimer?.cancel();
    _recordTimer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      setState(() => _recordElapsed += const Duration(seconds: 1));
    });
    await _speech.listen(
      listenOptions: SpeechListenOptions(
        listenFor: _maxRecording,
        pauseFor: const Duration(seconds: 5),
        partialResults: true,
        listenMode: ListenMode.dictation,
        cancelOnError: true,
      ),
      onResult: (r) {
        if (!mounted || !_recording) return;
        final words = r.recognizedWords.trim();
        final joined = [_draftBeforeRecording, words]
            .where((s) => s.isNotEmpty)
            .join(' ');
        _draftCtrl.value = TextEditingValue(
          text: joined,
          selection: TextSelection.collapsed(offset: joined.length),
        );
        if (r.finalResult) _finishRecording();
      },
    );
  }

  /// Stop and keep the transcript in the composer for review before sending.
  Future<void> _stopRecording() async {
    if (!_recording) return;
    await _speech.stop();
    _finishRecording();
  }

  /// Discard anything dictated in this recording.
  Future<void> _cancelRecording() async {
    if (!_recording) return;
    await _speech.cancel();
    _finishRecording();
    _draftCtrl.text = _draftBeforeRecording;
  }

  void _finishRecording() {
    _recordTimer?.cancel();
    _recordTimer = null;
    if (mounted && _recording) setState(() => _recording = false);
  }

  void _snack(String msg, {SnackBarAction? action}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(msg), action: action));
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scrollCtrl.hasClients) return;
      final max = _scrollCtrl.position.maxScrollExtent;
      if (max <= 0) return;
      _scrollCtrl.jumpTo(max);
    });
  }

  void _showChatMenu() {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.history_rounded),
              title: const Text('Chat history'),
              onTap: () {
                Navigator.pop(ctx);
                _showSessionsSheet();
              },
            ),
            ListTile(
              leading: const Icon(Icons.edit_square),
              title: const Text('New chat'),
              onTap: () {
                Navigator.pop(ctx);
                _newChat();
              },
            ),
            ListTile(
              leading: const Icon(Icons.refresh_rounded),
              title: const Text('Refresh status'),
              onTap: () {
                Navigator.pop(ctx);
                _bootstrap();
              },
            ),
          ],
        ),
      ),
    );
  }

  void _showSessionsSheet() {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (ctx) {
        final df = DateFormat('dd MMM · HH:mm');
        return DraggableScrollableSheet(
          expand: false,
          initialChildSize: 0.55,
          minChildSize: 0.3,
          maxChildSize: 0.92,
          builder: (ctx, sheetScroll) => SafeArea(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 8, 8),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Recent chats',
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w700,
                            color: ctx.sg.textPrimary,
                          ),
                        ),
                      ),
                      TextButton.icon(
                        onPressed: () {
                          Navigator.pop(ctx);
                          _newChat();
                        },
                        icon: const Icon(Icons.add_rounded),
                        label: const Text('New'),
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: _sessions.isEmpty
                      ? Center(
                          child: Text(
                            'No saved sessions yet',
                            style: TextStyle(color: ctx.sg.textSecondary),
                          ),
                        )
                      : ListView.separated(
                          controller: sheetScroll,
                          padding: const EdgeInsets.fromLTRB(12, 0, 12, 16),
                          itemCount: _sessions.length,
                          separatorBuilder: (_, __) =>
                              const SizedBox(height: 6),
                          itemBuilder: (_, i) {
                            final s = _sessions[i];
                            final active = s.id == _sessionId;
                            return Material(
                              color: active
                                  ? AppColors.primary.withValues(alpha: 0.08)
                                  : ctx.sg.card,
                              borderRadius: BorderRadius.circular(AppRadii.md),
                              child: ListTile(
                                title: Text(
                                  s.title,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: TextStyle(
                                    fontWeight: FontWeight.w600,
                                    color: ctx.sg.textPrimary,
                                  ),
                                ),
                                subtitle: Text(
                                  '${s.messageCount} msgs'
                                  '${s.updatedAt != null ? ' · ${df.format(s.updatedAt!.toLocal())}' : ''}',
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: TextStyle(
                                    fontSize: 12,
                                    color: ctx.sg.textSecondary,
                                  ),
                                ),
                                onTap: () {
                                  Navigator.pop(ctx);
                                  _openSession(s);
                                },
                              ),
                            );
                          },
                        ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final online = _health?.reachable ?? _health?.configured != false;
    return Scaffold(
      backgroundColor: p.surface,
      resizeToAvoidBottomInset: true,
      appBar: AiAssistantAppBar(
        showStatusStrip: true,
        online: online,
        onMenu: _showChatMenu,
      ),
      body: Align(
        alignment: Alignment.topCenter,
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: _maxContentWidth),
          child: Column(
            children: [
              if (_error != null) _buildErrorBanner(),
              Expanded(child: _buildMessages()),
              if (_pendingConfirmMessage != null && !_sending)
                _ConfirmBar(
                    onConfirm: _confirmWrite,
                    onDismiss: () {
                      setState(() => _pendingConfirmMessage = null);
                    }),
              if (!_sending && !_recording && _suggestions.isNotEmpty)
                _buildSuggestions(),
              _buildComposer(),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildErrorBanner() {
    return Material(
      color: AppColors.error.withValues(alpha: 0.08),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 6, 8, 6),
        child: Row(
          children: [
            const Icon(Icons.error_outline, color: AppColors.error, size: 18),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                _error!,
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 12, color: AppColors.error),
              ),
            ),
            if (_lastFailedText != null)
              TextButton(
                onPressed: _sending
                    ? null
                    : () => _send(overrideText: _lastFailedText, isRetry: true),
                child: const Text('Retry'),
              ),
            IconButton(
              tooltip: 'Dismiss',
              visualDensity: VisualDensity.compact,
              icon: const Icon(Icons.close_rounded, size: 18),
              onPressed: () => setState(() {
                _error = null;
                _lastFailedText = null;
              }),
            ),
          ],
        ),
      ),
    );
  }

  static String _copilotEmptyHint(AiProviderHealth? health) {
    if (health == null) {
      return 'Ops questions use fleet tools and rules. Optional LLM enhances answers when available.';
    }
    if (!health.configured) {
      return 'No LLM configured — answers use validated fleet tools and rules (offline vehicles, alerts, maintenance, risk).';
    }
    if (!health.reachable) {
      return 'Ollama is offline — answers use tools and rules until the LLM is reachable.';
    }
    return 'Ask about offline vehicles, alerts, maintenance, or driver risk. Tools always run; LLM is optional.';
  }

  Widget _buildMessages() {
    final p = context.sg;
    if (_messages.isEmpty) {
      return ListView(
        controller: _scrollCtrl,
        keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
        padding: const EdgeInsets.fromLTRB(24, 36, 24, 16),
        children: [
          Align(
            alignment: Alignment.centerLeft,
            child: Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(AppRadii.md),
                border: Border.all(
                  color: AppColors.primary.withValues(alpha: 0.22),
                ),
              ),
              child: Icon(
                Icons.auto_awesome_rounded,
                size: 28,
                color: AppColors.primary.withValues(alpha: 0.95),
              ),
            ),
          ),
          const SizedBox(height: 16),
          Text(
            'Ask about your fleet',
            style: TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w800,
              letterSpacing: -0.35,
              color: p.textPrimary,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            _copilotEmptyHint(_health),
            style: TextStyle(
              fontSize: 14,
              color: p.textSecondary,
              height: 1.4,
            ),
          ),
        ],
      );
    }

    return LayoutBuilder(builder: (context, constraints) {
      final w = constraints.maxWidth;
      final maxBubbleWidth = w < 480 ? w * 0.88 : w * 0.78;
      return ListView.builder(
        controller: _scrollCtrl,
        padding: const EdgeInsets.fromLTRB(12, 12, 12, 12),
        keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
        itemCount: _messages.length + (_sending ? 1 : 0),
        itemBuilder: (context, index) {
          if (_sending && index == _messages.length) {
            return Padding(
              padding: const EdgeInsets.symmetric(vertical: 8),
              child: Wrap(
                crossAxisAlignment: WrapCrossAlignment.center,
                spacing: 10,
                children: [
                  const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                  Text(
                    'Thinking…',
                    style: TextStyle(color: context.sg.textSecondary),
                  ),
                  TextButton(
                    onPressed: _cancelSending,
                    child: const Text('Stop'),
                  ),
                ],
              ),
            );
          }
          return AiChatBubbleTile(
            bubble: _messages[index],
            maxWidth: maxBubbleWidth,
          );
        },
      );
    });
  }

  Widget _buildSuggestions() {
    final p = context.sg;
    final chips = [
      ...AiAssistantCatalog.quickChips,
      ..._suggestions.where((s) => !AiAssistantCatalog.quickChips.contains(s)),
    ];
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      physics: const BouncingScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
      child: Row(
        children: [
          for (final s in chips.take(8))
            Padding(
              padding: const EdgeInsetsDirectional.only(end: 8),
              child: ActionChip(
                label: Text(
                  s,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: p.textPrimary,
                  ),
                ),
                onPressed: _sending
                    ? null
                    : () => _send(
                          overrideText: AiAssistantCatalog.quickChipPrompt(s),
                        ),
                backgroundColor: p.chipBg,
                side: BorderSide(color: p.border.withValues(alpha: 0.8)),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(AppRadii.pill),
                ),
                visualDensity: VisualDensity.compact,
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildRecordingBar() {
    final p = context.sg;
    final mm = _recordElapsed.inMinutes.toString().padLeft(2, '0');
    final ss = (_recordElapsed.inSeconds % 60).toString().padLeft(2, '0');
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.fromLTRB(12, 4, 4, 4),
      decoration: BoxDecoration(
        color: AppColors.error.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(AppRadii.lg),
        border: Border.all(color: AppColors.error.withValues(alpha: 0.3)),
      ),
      child: Row(
        children: [
          const Icon(Icons.fiber_manual_record_rounded,
              color: AppColors.error, size: 14),
          const SizedBox(width: 8),
          Text(
            '$mm:$ss',
            style: TextStyle(
              fontFeatures: const [FontFeature.tabularFigures()],
              fontWeight: FontWeight.w700,
              color: p.textPrimary,
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              'Listening… speak your question',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 12, color: p.textSecondary),
            ),
          ),
          TextButton(
            onPressed: _cancelRecording,
            child: const Text('Cancel'),
          ),
          FilledButton.tonal(
            onPressed: _stopRecording,
            child: const Text('Done'),
          ),
        ],
      ),
    );
  }

  Widget _buildComposer() {
    final p = context.sg;
    return Material(
      color: p.card,
      elevation: 0,
      child: Container(
        decoration: BoxDecoration(
          border: Border(
            top: BorderSide(color: p.border.withValues(alpha: 0.85)),
          ),
        ),
        child: SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 10, 12, 12),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (_recording) _buildRecordingBar(),
                Row(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Expanded(
                      child: sgImeSafeTextField(
                        controller: _draftCtrl,
                        focusNode: _focusNode,
                        enabled: !_sending,
                        readOnly: _recording,
                        minLines: 1,
                        maxLines: 5,
                        textInputAction: TextInputAction.send,
                        textCapitalization: TextCapitalization.sentences,
                        onSubmitted: (_) => _send(),
                        decoration: InputDecoration(
                          hintText: 'Ask SheikhGo AI anything…',
                          hintStyle: TextStyle(
                            color: p.textMuted,
                            fontWeight: FontWeight.w500,
                          ),
                          prefixIcon: Icon(
                            Icons.auto_awesome_rounded,
                            size: 20,
                            color: AiAssistantColors.heroBottom
                                .withValues(alpha: 0.85),
                          ),
                          filled: true,
                          fillColor: p.surface,
                          isDense: true,
                          contentPadding: const EdgeInsets.symmetric(
                            horizontal: 16,
                            vertical: 12,
                          ),
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(AppRadii.lg),
                            borderSide: BorderSide(
                              color: p.border.withValues(alpha: 0.85),
                            ),
                          ),
                          enabledBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(AppRadii.lg),
                            borderSide: BorderSide(
                              color: p.border.withValues(alpha: 0.85),
                            ),
                          ),
                          focusedBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(AppRadii.lg),
                            borderSide: const BorderSide(
                              color: AppColors.primary,
                              width: 1.4,
                            ),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 6),
                    IconButton(
                      tooltip: _recording ? 'Stop recording' : 'Voice input',
                      onPressed: _sending
                          ? null
                          : (_recording ? _stopRecording : _startRecording),
                      style: IconButton.styleFrom(
                        minimumSize: const Size(48, 48),
                        foregroundColor:
                            _recording ? AppColors.error : AppColors.primary,
                      ),
                      icon: Icon(
                        _recording ? Icons.stop_circle_rounded : Icons.mic_rounded,
                      ),
                    ),
                    const SizedBox(width: 4),
                    FilledButton(
                      onPressed: _sending ? _cancelSending : () => _send(),
                      style: FilledButton.styleFrom(
                        backgroundColor: AiAssistantColors.userBubble,
                        shape: const CircleBorder(),
                        padding: const EdgeInsets.all(14),
                        minimumSize: const Size(48, 48),
                      ),
                      child: Icon(
                        _sending ? Icons.stop_rounded : Icons.send_rounded,
                        size: 20,
                        color: Colors.white,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _ConfirmBar extends StatelessWidget {
  const _ConfirmBar({required this.onConfirm, required this.onDismiss});
  final VoidCallback onConfirm;
  final VoidCallback onDismiss;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.fromLTRB(12, 0, 12, 8),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.warning.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(AppRadii.md),
        border: Border.all(
          color: AppColors.warning.withValues(alpha: 0.4),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Write action needs confirmation',
            style: TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: 13,
              color: context.sg.textPrimary,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'The Tool Engine prepared a change but has not applied it yet.',
            style: TextStyle(fontSize: 12, color: context.sg.textSecondary),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: onDismiss,
                  child: const Text('Cancel'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: FilledButton(
                  onPressed: onConfirm,
                  child: const Text('Confirm'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
