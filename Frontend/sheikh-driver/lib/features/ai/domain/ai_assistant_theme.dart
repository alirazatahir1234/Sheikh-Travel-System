import 'package:flutter/material.dart';
import '../../../core/constants/app_theme.dart';

/// AI Assistant visual tokens (mock parity; respects light/dark via [SgPalette]).
abstract class AiAssistantColors {
  static const heroTop = Color(0xFF004D40);
  static const heroBottom = Color(0xFF0D9488);
  static const userBubble = Color(0xFF0F766E);
  static const aiBubbleLight = Color(0xFFF1F5F9);
  static const critical = Color(0xFFEF4444);
  static const idle = Color(0xFFF59E0B);
  static const moving = Color(0xFF0EA5E9);
  static const online = Color(0xFF10B981);

  static LinearGradient heroGradient(BuildContext context) {
    final dark = Theme.of(context).brightness == Brightness.dark;
    if (dark) {
      return const LinearGradient(
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
        colors: [Color(0xFF0A3D36), Color(0xFF145E54)],
      );
    }
    return const LinearGradient(
      begin: Alignment.topLeft,
      end: Alignment.bottomRight,
      colors: [heroTop, heroBottom],
    );
  }
}
