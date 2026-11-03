import 'package:flutter/material.dart';
import '../../domain/ai_assistant_theme.dart';

/// Lightweight robot mascot (no external 3D asset).
class AiRobotAvatar extends StatelessWidget {
  const AiRobotAvatar({super.key, this.size = 72, this.compact = false});

  final double size;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final body = size * (compact ? 0.85 : 1);
    return SizedBox(
      width: size,
      height: size,
      child: DecoratedBox(
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [
              Colors.white.withValues(alpha: 0.95),
              Colors.white.withValues(alpha: 0.78),
            ],
          ),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.12),
              blurRadius: compact ? 8 : 16,
              offset: const Offset(0, 6),
            ),
          ],
        ),
        child: Stack(
          alignment: Alignment.center,
          children: [
            Icon(
              Icons.smart_toy_rounded,
              size: body * 0.62,
              color: AiAssistantColors.heroBottom,
            ),
            if (!compact)
              Positioned(
                bottom: body * 0.22,
                child: Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                  decoration: BoxDecoration(
                    color: AiAssistantColors.heroTop,
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: const Text(
                    'SG',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 9,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 0.4,
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
