import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../domain/ai_assistant_theme.dart';
import 'ai_robot_avatar.dart';

/// Teal floating entry to AI home (complements More menu, not a duplicate route).
class AiAssistantFab extends StatelessWidget {
  const AiAssistantFab({super.key});

  @override
  Widget build(BuildContext context) {
    return Padding(
      // Clear section CTAs (Open Map / View All) while scrolling.
      padding: const EdgeInsets.only(bottom: 20, right: 2),
      child: FloatingActionButton(
        heroTag: 'ai_assistant_fab',
        backgroundColor: AiAssistantColors.userBubble,
        elevation: 4,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        onPressed: () => context.push('/ai'),
        child: const AiRobotAvatar(size: 44, compact: true),
      ),
    );
  }
}
