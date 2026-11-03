import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/constants/app_theme.dart';
import '../../../notifications/presentation/notifications_notifier.dart';
import '../../domain/ai_assistant_catalog.dart';
import '../../domain/ai_assistant_theme.dart';
import 'ai_robot_avatar.dart';

class AiAssistantAppBar extends ConsumerWidget implements PreferredSizeWidget {
  const AiAssistantAppBar({
    super.key,
    this.showStatusStrip = false,
    this.online = true,
    this.onMenu,
  });

  final bool showStatusStrip;
  final bool online;
  final VoidCallback? onMenu;

  @override
  Size get preferredSize =>
      Size.fromHeight(showStatusStrip ? kToolbarHeight + 36 : kToolbarHeight);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread = ref.watch(unreadNotificationsCountProvider).valueOrNull ?? 0;
    final p = context.sg;

    return AppBar(
      centerTitle: true,
      title: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Image.asset(
                'assets/branding/sheikhgo_logo.png',
                height: 22,
                errorBuilder: (_, __, ___) => Text(
                  'SheikhGo',
                  style: TextStyle(
                    fontWeight: FontWeight.w800,
                    color: p.textPrimary,
                  ),
                ),
              ),
            ],
          ),
          if (showStatusStrip) ...[
            const SizedBox(height: 2),
            Text(
              online
                  ? 'Online · Powered by SheikhGo AI'
                  : 'Tools mode · SheikhGo AI',
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w500,
                color: p.textSecondary,
              ),
            ),
          ],
        ],
      ),
      actions: [
        if (onMenu != null)
          IconButton(
            icon: const Icon(Icons.more_vert_rounded),
            onPressed: onMenu,
          )
        else
          Stack(
            clipBehavior: Clip.none,
            children: [
              IconButton(
                tooltip: 'Notifications',
                icon: const Icon(Icons.notifications_outlined),
                onPressed: () => context.push('/notifications'),
              ),
              if (unread > 0)
                Positioned(
                  right: 8,
                  top: 8,
                  child: Container(
                    width: 8,
                    height: 8,
                    decoration: const BoxDecoration(
                      color: AppColors.error,
                      shape: BoxShape.circle,
                    ),
                  ),
                ),
            ],
          ),
      ],
    );
  }
}

class AiAssistantHero extends StatelessWidget {
  const AiAssistantHero({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      decoration: BoxDecoration(
        gradient: AiAssistantColors.heroGradient(context),
      ),
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 28),
          child: Column(
            children: [
              const AiRobotAvatar(size: 96),
              const SizedBox(height: 12),
              Wrap(
                crossAxisAlignment: WrapCrossAlignment.center,
                spacing: 8,
                children: [
                  Text(
                    AiAssistantCatalog.heroTitle,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 22,
                      fontWeight: FontWeight.w800,
                      letterSpacing: -0.3,
                    ),
                  ),
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.18),
                      borderRadius: BorderRadius.circular(AppRadii.pill),
                      border: Border.all(
                        color: Colors.white.withValues(alpha: 0.35),
                      ),
                    ),
                    child: const Text(
                      'Beta',
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text(
                AiAssistantCatalog.heroSubtitle,
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: Colors.white.withValues(alpha: 0.92),
                  fontSize: 14,
                  height: 1.35,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
