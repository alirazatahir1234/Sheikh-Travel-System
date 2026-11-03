import 'dart:math';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../domain/ai_assistant_catalog.dart';
import 'widgets/ai_assistant_app_bar.dart';

class AiAssistantHomeScreen extends StatefulWidget {
  const AiAssistantHomeScreen({super.key});

  @override
  State<AiAssistantHomeScreen> createState() => _AiAssistantHomeScreenState();
}

class _AiAssistantHomeScreenState extends State<AiAssistantHomeScreen> {
  late List<String> _tryAsking;

  @override
  void initState() {
    super.initState();
    _tryAsking = _pickTryAsking();
  }

  List<String> _pickTryAsking() {
    final pool = List<String>.from(AiAssistantCatalog.tryAskingPool);
    pool.shuffle(Random());
    return pool.take(3).toList();
  }

  void _openChat(String prompt) {
    context.push(
      '/ai/chat?q=${Uri.encodeComponent(prompt.trim())}',
    );
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Scaffold(
      backgroundColor: p.surface,
      appBar: AiAssistantAppBar(
        onMenu: null,
      ),
      body: ListView(
        padding: EdgeInsets.zero,
        children: [
          const AiAssistantHero(),
          Transform.translate(
            offset: const Offset(0, -18),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _PromptCard(onTap: () => _openChat('What is my fleet status?')),
                  const SizedBox(height: 16),
                  GridView.count(
                    crossAxisCount: 2,
                    shrinkWrap: true,
                    physics: const NeverScrollableScrollPhysics(),
                    mainAxisSpacing: 10,
                    crossAxisSpacing: 10,
                    childAspectRatio: 1.35,
                    children: [
                      for (final f in AiAssistantCatalog.features)
                        _FeatureCard(
                          feature: f,
                          onTap: () => _openChat(f.prompt),
                        ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  Row(
                    children: [
                      Text(
                        'Try asking',
                        style: TextStyle(
                          fontWeight: FontWeight.w800,
                          fontSize: 15,
                          color: p.textPrimary,
                        ),
                      ),
                      const Spacer(),
                      IconButton(
                        tooltip: 'Shuffle suggestions',
                        visualDensity: VisualDensity.compact,
                        onPressed: () =>
                            setState(() => _tryAsking = _pickTryAsking()),
                        icon: Icon(Icons.refresh_rounded, color: p.textSecondary),
                      ),
                    ],
                  ),
                  for (final q in _tryAsking)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: _TryAskingRow(
                        text: q,
                        onTap: () => _openChat(q),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _PromptCard extends StatelessWidget {
  const _PromptCard({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Material(
      color: p.card,
      borderRadius: BorderRadius.circular(AppRadii.xl),
      elevation: 0,
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadii.xl),
        onTap: onTap,
        child: Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.xl),
            boxShadow: AppShadows.card,
            border: Border.all(color: p.border.withValues(alpha: 0.7)),
          ),
          padding: const EdgeInsets.all(18),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: const Icon(Icons.auto_awesome_rounded,
                    color: AppColors.primary, size: 22),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'How can I help you today?',
                      style: TextStyle(
                        fontWeight: FontWeight.w800,
                        fontSize: 16,
                        color: p.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'Ask anything about your fleet, vehicles, trips, drivers, alerts, or reports.',
                      style: TextStyle(
                        fontSize: 13,
                        height: 1.4,
                        color: p.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _FeatureCard extends StatelessWidget {
  const _FeatureCard({required this.feature, required this.onTap});

  final AiAssistantFeature feature;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Material(
      color: p.card,
      borderRadius: BorderRadius.circular(AppRadii.lg),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadii.lg),
        onTap: onTap,
        child: Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.lg),
            border: Border.all(color: p.border.withValues(alpha: 0.75)),
            boxShadow: AppShadows.card,
          ),
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(feature.icon, color: feature.accent, size: 26),
              const Spacer(),
              Text(
                feature.title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontWeight: FontWeight.w800,
                  fontSize: 13,
                  color: p.textPrimary,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                feature.subtitle,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 11, color: p.textSecondary),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TryAskingRow extends StatelessWidget {
  const _TryAskingRow({required this.text, required this.onTap});

  final String text;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Material(
      color: p.card,
      borderRadius: BorderRadius.circular(AppRadii.lg),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadii.lg),
        onTap: onTap,
        child: Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadii.lg),
            border: Border.all(color: p.border.withValues(alpha: 0.75)),
          ),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  '“$text”',
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                    color: p.textPrimary,
                  ),
                ),
              ),
              Icon(Icons.north_east_rounded,
                  size: 18, color: AppColors.primary),
            ],
          ),
        ),
      ),
    );
  }
}
