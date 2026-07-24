import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../data/ai_chat_api.dart';
import '../domain/ai_chat_models.dart';

class AiCopilotScreen extends ConsumerStatefulWidget {
  const AiCopilotScreen({super.key, this.initialPrompt});

  /// Prefills the composer when opened from dashboard CTAs (`/ai?q=...`).
  final String? initialPrompt;

  @override
  ConsumerState<AiCopilotScreen> createState() => _AiCopilotScreenState();
}

class _AiCopilotScreenState extends ConsumerState<AiCopilotScreen> {
  final _draftCtrl = TextEditingController();
  final _scrollCtrl = ScrollController();
  final _focusNode = FocusNode();

  bool _bootLoading = true;
  bool _sending = false;
  String? _sessionId;
  String? _pendingConfirmMessage;
  List<AiChatBubble> _messages = [];
  List<AiChatSession> _sessions = [];
  List<String> _suggestions = List.of(defaultAiSuggestions);
  AiProviderHealth? _health;
  String? _error;

  @override
  void initState() {
    super.initState();
    final q = widget.initialPrompt?.trim();
    if (q != null && q.isNotEmpty) {
      _draftCtrl.text = q;
    }
    _bootstrap();
  }

  @override
  void dispose() {
    _draftCtrl.dispose();
    _scrollCtrl.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    setState(() {
      _bootLoading = true;
      _error = null;
    });
    final api = ref.read(aiChatApiProvider);
    try {
      final results = await Future.wait([
        api.getProviderHealth().catchError((_) => const AiProviderHealth(
              provider: 'Unknown',
              configured: false,
              reachable: false,
              statusMessage: 'Could not load provider status.',
            )),
        api.listSessions().catchError((_) => <AiChatSession>[]),
      ]);
      if (!mounted) return;
      setState(() {
        _health = results[0] as AiProviderHealth;
        _sessions = results[1] as List<AiChatSession>;
        _bootLoading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _bootLoading = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _refreshSessions() async {
    try {
      final sessions = await ref.read(aiChatApiProvider).listSessions();
      if (mounted) setState(() => _sessions = sessions);
    } catch (_) {}
  }

  void _newChat() {
    setState(() {
      _sessionId = null;
      _messages = [];
      _pendingConfirmMessage = null;
      _draftCtrl.clear();
      _error = null;
      _suggestions = List.of(defaultAiSuggestions);
    });
  }

  Future<void> _openSession(AiChatSession session) async {
    setState(() {
      _sessionId = session.id;
      _messages = [];
      _pendingConfirmMessage = null;
      _error = null;
    });
    try {
      final rows = await ref.read(aiChatApiProvider).getMessages(session.id);
      if (!mounted) return;
      setState(() {
        _messages = rows
            .where((m) => m.isUser || m.isAssistant)
            .map((m) => AiChatBubble(
                  role: m.isUser ? 'user' : 'assistant',
                  content: m.content,
                ))
            .toList();
      });
      _scrollToBottom();
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  Future<void> _send({
    String? overrideText,
    bool confirmWrite = false,
  }) async {
    final text = (overrideText ?? _draftCtrl.text).trim();
    if (text.isEmpty || _sending) return;

    if (!confirmWrite) {
      setState(() {
        _messages = [
          ..._messages,
          AiChatBubble(role: 'user', content: text),
        ];
        _draftCtrl.clear();
        _pendingConfirmMessage = null;
      });
    }

    setState(() {
      _sending = true;
      _error = null;
    });
    _scrollToBottom();

    try {
      final res = await ref.read(aiChatApiProvider).chat(
            message: text,
            sessionId: _sessionId,
            confirmWrite: confirmWrite,
          );
      if (!mounted) return;
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
          ),
        ];
        _pendingConfirmMessage =
            res.pendingConfirmation ? text : null;
      });
      await _refreshSessions();
      _scrollToBottom();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  Future<void> _confirmWrite() async {
    final msg = _pendingConfirmMessage;
    if (msg == null) return;
    await _send(overrideText: msg, confirmWrite: true);
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scrollCtrl.hasClients) return;
      final max = _scrollCtrl.position.maxScrollExtent;
      if (max <= 0) return;
      _scrollCtrl.jumpTo(max);
    });
  }

  void _showSessionsSheet() {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      isScrollControlled: true,
      builder: (ctx) {
        final df = DateFormat('dd MMM · HH:mm');
        return SafeArea(
          child: SizedBox(
            height: MediaQuery.of(ctx).size.height * 0.55,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 8, 8),
                  child: Row(
                    children: [
                      const Text(
                        'Recent chats',
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const Spacer(),
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
                      ? const Center(
                          child: Text(
                            'No saved sessions yet',
                            style: TextStyle(color: AppColors.textSecondary),
                          ),
                        )
                      : ListView.separated(
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
                                  : AppColors.cardBg,
                              borderRadius:
                                  BorderRadius.circular(AppRadii.md),
                              child: ListTile(
                                title: Text(
                                  s.title,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                                subtitle: Text(
                                  '${s.messageCount} msgs'
                                  '${s.updatedAt != null ? ' · ${df.format(s.updatedAt!.toLocal())}' : ''}',
                                  style: const TextStyle(
                                    fontSize: 12,
                                    color: AppColors.textSecondary,
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
    return Scaffold(
      backgroundColor: AppColors.surface,
      appBar: AppBar(
        title: const Text('SheikhGo AI'),
        actions: [
          IconButton(
            tooltip: 'Chat history',
            icon: const Icon(Icons.history_rounded),
            onPressed: _showSessionsSheet,
          ),
          IconButton(
            tooltip: 'New chat',
            icon: const Icon(Icons.edit_square),
            onPressed: _newChat,
          ),
          IconButton(
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh_rounded),
            onPressed: _bootstrap,
          ),
        ],
      ),
      body: _bootLoading
          ? const Center(child: CircularProgressIndicator())
          : Column(
              children: [
                if (_health != null) _ProviderStrip(health: _health!),
                if (_error != null)
                  Material(
                    color: AppColors.error.withValues(alpha: 0.08),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 8,
                      ),
                      child: Row(
                        children: [
                          const Icon(Icons.error_outline,
                              color: AppColors.error, size: 18),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              _error!,
                              style: const TextStyle(
                                fontSize: 12,
                                color: AppColors.error,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                Expanded(child: _buildMessages()),
                if (_pendingConfirmMessage != null && !_sending)
                  _ConfirmBar(onConfirm: _confirmWrite, onDismiss: () {
                    setState(() => _pendingConfirmMessage = null);
                  }),
                if (!_sending && _suggestions.isNotEmpty) _buildSuggestions(),
                _buildComposer(),
              ],
            ),
    );
  }

  Widget _buildMessages() {
    if (_messages.isEmpty) {
      return ListView(
        controller: _scrollCtrl,
        padding: const EdgeInsets.fromLTRB(24, 32, 24, 16),
        children: [
          Icon(
            Icons.auto_awesome_rounded,
            size: 48,
            color: AppColors.primary.withValues(alpha: 0.85),
          ),
          const SizedBox(height: 16),
          const Text(
            'Ask about your fleet',
            style: TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            _health != null && !_health!.reachable
                ? 'Ollama is offline — answers use tools and rules until the LLM is reachable.'
                : 'Natural language queries run through AiChatGateway and the Tool Engine.',
            style: const TextStyle(
              fontSize: 14,
              color: AppColors.textSecondary,
              height: 1.4,
            ),
          ),
        ],
      );
    }

    return ListView.builder(
      controller: _scrollCtrl,
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
      keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
      itemCount: _messages.length + (_sending ? 1 : 0),
      itemBuilder: (context, index) {
        if (_sending && index == _messages.length) {
          return const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Row(
              children: [
                SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
                SizedBox(width: 10),
                Text(
                  'Thinking…',
                  style: TextStyle(color: AppColors.textSecondary),
                ),
              ],
            ),
          );
        }
        return _BubbleTile(bubble: _messages[index]);
      },
    );
  }

  Widget _buildSuggestions() {
    return SizedBox(
      height: 44,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
        itemCount: _suggestions.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (_, i) {
          final s = _suggestions[i];
          return ActionChip(
            label: Text(
              s,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 12),
            ),
            onPressed: _sending
                ? null
                : () {
                    _draftCtrl.text = s;
                    _send();
                  },
            backgroundColor: AppColors.chipBg,
            side: BorderSide.none,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppRadii.pill),
            ),
          );
        },
      ),
    );
  }

  Widget _buildComposer() {
    return SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 4, 12, 12),
        child: Row(
          children: [
            Expanded(
              child: TextField(
                controller: _draftCtrl,
                focusNode: _focusNode,
                enabled: !_sending,
                minLines: 1,
                maxLines: 4,
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => _send(),
                decoration: InputDecoration(
                  hintText: 'Message SheikhGo AI…',
                  filled: true,
                  fillColor: AppColors.cardBg,
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: 16,
                    vertical: 12,
                  ),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadii.lg),
                    borderSide: const BorderSide(color: AppColors.border),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadii.lg),
                    borderSide: const BorderSide(color: AppColors.border),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadii.lg),
                    borderSide: const BorderSide(color: AppColors.primary),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 8),
            FilledButton(
              onPressed: _sending ? null : () => _send(),
              style: FilledButton.styleFrom(
                shape: const CircleBorder(),
                padding: const EdgeInsets.all(14),
              ),
              child: const Icon(Icons.send_rounded, size: 20),
            ),
          ],
        ),
      ),
    );
  }
}

class _ProviderStrip extends StatelessWidget {
  const _ProviderStrip({required this.health});
  final AiProviderHealth health;

  @override
  Widget build(BuildContext context) {
    final ok = health.reachable;
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.fromLTRB(16, 8, 16, 4),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: ok
            ? AppColors.primary.withValues(alpha: 0.08)
            : AppColors.warning.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(AppRadii.md),
        border: Border.all(
          color: ok
              ? AppColors.primary.withValues(alpha: 0.25)
              : AppColors.warning.withValues(alpha: 0.35),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            ok ? Icons.check_circle_rounded : Icons.error_outline_rounded,
            size: 20,
            color: ok ? AppColors.primary : AppColors.warning,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  [
                    health.provider,
                    if (health.model != null && health.model!.isNotEmpty)
                      health.model!,
                  ].join(' · '),
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 13,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  health.statusMessage,
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                    height: 1.3,
                  ),
                ),
              ],
            ),
          ),
        ],
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
          const Text(
            'Write action needs confirmation',
            style: TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: 13,
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'The Tool Engine prepared a change but has not applied it yet.',
            style: TextStyle(fontSize: 12, color: AppColors.textSecondary),
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

class _BubbleTile extends StatelessWidget {
  const _BubbleTile({required this.bubble});
  final AiChatBubble bubble;

  @override
  Widget build(BuildContext context) {
    final isUser = bubble.isUser;
    final maxBubbleWidth = MediaQuery.sizeOf(context).width * 0.86;

    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        mainAxisAlignment:
            isUser ? MainAxisAlignment.end : MainAxisAlignment.start,
        children: [
          ConstrainedBox(
            constraints: BoxConstraints(maxWidth: maxBubbleWidth),
            child: DecoratedBox(
              decoration: BoxDecoration(
                color: isUser ? AppColors.primary : AppColors.cardBg,
                borderRadius: BorderRadius.only(
                  topLeft: const Radius.circular(16),
                  topRight: const Radius.circular(16),
                  bottomLeft: Radius.circular(isUser ? 16 : 4),
                  bottomRight: Radius.circular(isUser ? 4 : 16),
                ),
                boxShadow: isUser ? null : AppShadows.card,
              ),
              child: Padding(
                padding:
                    const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      bubble.content,
                      style: TextStyle(
                        fontSize: 14,
                        height: 1.4,
                        color: isUser ? Colors.white : AppColors.textPrimary,
                      ),
                    ),
                    if (!isUser && bubble.mode != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        [
                          bubble.mode!,
                          if (bubble.usedLlm) 'LLM',
                          if (bubble.tools.isNotEmpty) bubble.tools.join(', '),
                          if (bubble.pendingConfirmation) 'needs confirm',
                        ].join(' · '),
                        style: const TextStyle(
                          fontSize: 11,
                          color: AppColors.textMuted,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
