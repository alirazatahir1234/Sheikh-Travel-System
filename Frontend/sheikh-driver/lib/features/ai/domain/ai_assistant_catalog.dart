import 'package:flutter/material.dart';

/// Premium AI Assistant copy + starter prompts (no fabricated fleet data).
class AiAssistantFeature {
  const AiAssistantFeature({
    required this.title,
    required this.subtitle,
    required this.prompt,
    required this.icon,
    required this.accent,
  });

  final String title;
  final String subtitle;
  final String prompt;
  final IconData icon;
  final Color accent;
}

abstract class AiAssistantCatalog {
  static const heroTitle = 'AI Assistant';
  static const heroSubtitle = 'Your intelligent fleet co-pilot.';

  static const quickChips = [
    'Fleet status',
    "Today's trips",
    'Alerts',
    'Reports',
  ];

  static const tryAskingPool = [
    'Show vehicles that are offline',
    'Any critical alerts right now?',
    "Today's trip summary?",
    'Which vehicles are moving?',
    'Summarize driver risk this week',
    'What maintenance is overdue?',
  ];

  static const features = [
    AiAssistantFeature(
      title: 'Fleet Status',
      subtitle: 'Get live fleet overview',
      prompt: 'What is my current fleet status?',
      icon: Icons.directions_car_rounded,
      accent: Color(0xFF10B981),
    ),
    AiAssistantFeature(
      title: 'Alerts & Issues',
      subtitle: 'Check active alerts',
      prompt: 'Show critical GPS alerts right now',
      icon: Icons.warning_amber_rounded,
      accent: Color(0xFFF59E0B),
    ),
    AiAssistantFeature(
      title: 'Trips & Routes',
      subtitle: 'Trip status and details',
      prompt: "Summarize today's trips",
      icon: Icons.route_rounded,
      accent: Color(0xFF0D9488),
    ),
    AiAssistantFeature(
      title: 'Reports & Insights',
      subtitle: 'Performance and analytics',
      prompt: 'Give me fleet performance insights for this week',
      icon: Icons.bar_chart_rounded,
      accent: Color(0xFF14B8A6),
    ),
    AiAssistantFeature(
      title: 'Drivers',
      subtitle: 'Driver status & activity',
      prompt: 'Which drivers need attention this week?',
      icon: Icons.badge_outlined,
      accent: Color(0xFF059669),
    ),
    AiAssistantFeature(
      title: 'Help & How-to',
      subtitle: 'Learn features, get assistance',
      prompt: 'How do I use SheikhGo fleet features?',
      icon: Icons.menu_book_rounded,
      accent: Color(0xFF2563EB),
    ),
  ];

  static String quickChipPrompt(String chip) => switch (chip) {
        'Fleet status' => 'What is my current fleet status?',
        "Today's trips" => "Summarize today's trips",
        'Alerts' => 'Show critical GPS alerts right now',
        'Reports' => 'Give me fleet performance insights for this week',
        _ => chip,
      };
}
