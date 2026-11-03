import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../../../../core/constants/app_theme.dart';
import '../../domain/ai_assistant_theme.dart';
import '../../domain/ai_chat_models.dart';
import '../../domain/ai_structured_payload.dart';
import '../ai_message_segments.dart';
import 'ai_robot_avatar.dart';
import 'ai_structured_cards.dart';

class AiChatBubbleTile extends StatelessWidget {
  const AiChatBubbleTile({
    super.key,
    required this.bubble,
    required this.maxWidth,
  });

  final AiChatBubble bubble;
  final double maxWidth;

  @override
  Widget build(BuildContext context) {
    final isUser = bubble.isUser;
    final structured = parseAiStructuredPayload(
      bubble.structuredPayload,
      toolUsed: bubble.toolUsed,
    );
    final showProse = structured?.fleetStatus == null ||
        bubble.content.trim().length > 120;

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment:
            isUser ? CrossAxisAlignment.end : CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment:
                isUser ? MainAxisAlignment.end : MainAxisAlignment.start,
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              if (!isUser) ...[
                const AiRobotAvatar(size: 32, compact: true),
                const SizedBox(width: 8),
              ],
              Flexible(
                child: ConstrainedBox(
                  constraints: BoxConstraints(maxWidth: maxWidth),
                  child: _BubbleBody(bubble: bubble, showProse: showProse),
                ),
              ),
            ],
          ),
          if (!isUser && structured != null && structured.hasWidgets) ...[
            const SizedBox(height: 8),
            ConstrainedBox(
              constraints: BoxConstraints(maxWidth: maxWidth),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (structured.fleetStatus != null)
                    AiFleetSummaryCard(summary: structured.fleetStatus!),
                  for (final alert in structured.alerts) ...[
                    const SizedBox(height: 8),
                    AiAlertInsightCard(alert: alert),
                  ],
                ],
              ),
            ),
          ],
          if (isUser) ...[
            const SizedBox(height: 4),
            _UserMeta(createdAt: bubble.createdAt ?? DateTime.now()),
          ],
        ],
      ),
    );
  }
}

class _BubbleBody extends StatelessWidget {
  const _BubbleBody({required this.bubble, required this.showProse});

  final AiChatBubble bubble;
  final bool showProse;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final isUser = bubble.isUser;
    final fg = isUser ? Colors.white : p.textPrimary;
    final bg = isUser ? AiAssistantColors.userBubble : p.card;
    final segments = splitAiMessage(bubble.content);

    if (!showProse && !isUser && bubble.content.trim().isEmpty) {
      return const SizedBox.shrink();
    }

    return DecoratedBox(
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.only(
          topLeft: const Radius.circular(AppRadii.lg),
          topRight: const Radius.circular(AppRadii.lg),
          bottomLeft: Radius.circular(isUser ? AppRadii.lg : 6),
          bottomRight: Radius.circular(isUser ? 6 : AppRadii.lg),
        ),
        border: isUser
            ? null
            : Border.all(color: p.border.withValues(alpha: 0.75)),
        boxShadow: isUser ? null : AppShadows.card,
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 11),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            if (showProse)
              for (final seg in segments)
                if (seg.preformatted)
                  _PreformattedBlock(text: seg.text, color: fg)
                else
                  SelectableText(
                    seg.text,
                    style: TextStyle(
                      fontSize: 14,
                      height: 1.4,
                      fontWeight: FontWeight.w500,
                      color: fg,
                    ),
                  ),
          ],
        ),
      ),
    );
  }
}

class _UserMeta extends StatelessWidget {
  const _UserMeta({required this.createdAt});

  final DateTime createdAt;

  @override
  Widget build(BuildContext context) {
    final time = DateFormat('h:mm a').format(createdAt.toLocal());
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          time,
          style: TextStyle(fontSize: 11, color: context.sg.textMuted),
        ),
        const SizedBox(width: 4),
        Icon(
          Icons.done_all_rounded,
          size: 14,
          color: AiAssistantColors.heroBottom.withValues(alpha: 0.9),
        ),
      ],
    );
  }
}

class _PreformattedBlock extends StatelessWidget {
  const _PreformattedBlock({required this.text, required this.color});
  final String text;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.symmetric(vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(6),
      ),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.all(8),
        child: SelectableText(
          text,
          style: TextStyle(
            fontFamily: 'monospace',
            fontSize: 12.5,
            height: 1.35,
            color: color,
          ),
        ),
      ),
    );
  }
}
