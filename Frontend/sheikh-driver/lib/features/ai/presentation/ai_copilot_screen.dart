import 'package:flutter/material.dart';
import 'ai_assistant_chat_screen.dart';
import 'ai_assistant_home_screen.dart';

/// Back-compat entry — prefer [AiAssistantHomeScreen] / [AiAssistantChatScreen].
class AiCopilotScreen extends StatelessWidget {
  const AiCopilotScreen({super.key, this.initialPrompt});

  final String? initialPrompt;

  @override
  Widget build(BuildContext context) {
    final q = initialPrompt?.trim();
    if (q != null && q.isNotEmpty) {
      return AiAssistantChatScreen(initialPrompt: q);
    }
    return const AiAssistantHomeScreen();
  }
}
