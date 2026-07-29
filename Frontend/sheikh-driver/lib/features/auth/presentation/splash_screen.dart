import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:package_info_plus/package_info_plus.dart';
import '../../../core/constants/app_theme.dart';
import '../data/auth_repository.dart';

const kSplashMinDisplay = Duration(seconds: 2);
const kSplashMaxAuthWait = Duration(seconds: 3);
const kSplashTagline = 'Smart GPS Operations';

/// Resolves post-splash route (testable).
String resolveSplashRoute({
  required bool isLoggedIn,
  required bool sessionExpired,
}) {
  if (isLoggedIn) return '/dashboard';
  if (sessionExpired) return '/session-expired';
  return '/login';
}

/// Brief branded splash while session restore completes.
class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({super.key});

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen>
    with TickerProviderStateMixin {
  late final AnimationController _logoCtrl;
  late final AnimationController _loaderCtrl;
  late final Animation<double> _logoFade;
  late final Animation<double> _logoScale;
  String? _versionLabel;

  @override
  void initState() {
    super.initState();
    final reduceMotion = WidgetsBinding.instance.platformDispatcher.accessibilityFeatures.reduceMotion;
    _logoCtrl = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 450),
    );
    _loaderCtrl = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1400),
    );
    _logoFade = CurvedAnimation(parent: _logoCtrl, curve: Curves.easeOut);
    _logoScale = Tween<double>(begin: 0.92, end: 1).animate(
      CurvedAnimation(parent: _logoCtrl, curve: Curves.easeOutCubic),
    );
    _logoCtrl.forward();
    if (!reduceMotion) {
      _loaderCtrl.repeat();
    } else {
      _loaderCtrl.value = 0.4;
    }
    unawaited(_loadVersion());
    WidgetsBinding.instance.addPostFrameCallback((_) => _route());
  }

  Future<void> _loadVersion() async {
    try {
      final info = await PackageInfo.fromPlatform();
      if (mounted) setState(() => _versionLabel = 'v ${info.version}');
    } catch (_) {}
  }

  Future<void> _route() async {
    final started = DateTime.now();
    final auth = ref.read(authRepositoryProvider);

    while (auth.isLoading) {
      if (DateTime.now().difference(started) >= kSplashMaxAuthWait) break;
      await Future<void>.delayed(const Duration(milliseconds: 50));
      if (!mounted) return;
    }

    final elapsed = DateTime.now().difference(started);
    final remaining = kSplashMinDisplay - elapsed;
    if (remaining > Duration.zero) {
      await Future<void>.delayed(remaining);
    }
    if (!mounted) return;

    final destination = resolveSplashRoute(
      isLoggedIn: auth.isLoggedIn,
      sessionExpired: auth.sessionExpired,
    );
    context.go(destination);
  }

  @override
  void dispose() {
    _logoCtrl.dispose();
    _loaderCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Scaffold(
      body: SplashBrandPanel(
        isDark: isDark,
        logoFade: _logoFade,
        logoScale: _logoScale,
        loaderValue: _loaderCtrl,
        versionLabel: _versionLabel,
      ),
    );
  }
}

/// Brand layout shared by splash screen and widget tests.
class SplashBrandPanel extends StatelessWidget {
  const SplashBrandPanel({
    super.key,
    required this.isDark,
    required this.logoFade,
    required this.logoScale,
    required this.loaderValue,
    this.versionLabel,
  });

  final bool isDark;
  final Animation<double> logoFade;
  final Animation<double> logoScale;
  final Animation<double> loaderValue;
  final String? versionLabel;

  @override
  Widget build(BuildContext context) {
    final reduceMotion = MediaQuery.of(context).disableAnimations;

    return DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: isDark
              ? [AppColors.splashNavyTop, AppColors.splashNavyBottom]
              : [AppColors.splashLightTop, AppColors.splashLightBottom],
        ),
      ),
      child: Stack(
        fit: StackFit.expand,
        children: [
          if (isDark)
            Align(
              alignment: Alignment.bottomCenter,
              child: Container(
                height: 220,
                decoration: BoxDecoration(
                  gradient: RadialGradient(
                    center: Alignment.bottomCenter,
                    radius: 1.1,
                    colors: [
                      AppColors.primary.withValues(alpha: 0.22),
                      Colors.transparent,
                    ],
                  ),
                ),
              ),
            ),
          SafeArea(
            child: Center(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 32),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    FadeTransition(
                      opacity: logoFade,
                      child: ScaleTransition(
                        scale: logoScale,
                        child: Image.asset(
                          'assets/branding/sheikhgo_logo.png',
                          width: 200,
                          height: 200,
                          fit: BoxFit.contain,
                          errorBuilder: (_, __, ___) => Icon(
                            Icons.radar_rounded,
                            size: 88,
                            color: isDark ? AppColors.primaryLight : AppColors.primary,
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(height: 20),
                    Text(
                      kSplashTagline,
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        letterSpacing: 0.6,
                        color: isDark
                            ? Colors.white.withValues(alpha: 0.78)
                            : AppColors.textSecondary,
                      ),
                    ),
                    const SizedBox(height: 36),
                    SplashLoaderBar(
                      progress: loaderValue,
                      isDark: isDark,
                      staticProgress: reduceMotion,
                    ),
                    const SizedBox(height: 12),
                    Text(
                      'Loading…',
                      style: TextStyle(
                        fontSize: 12,
                        color: isDark
                            ? Colors.white.withValues(alpha: 0.45)
                            : AppColors.textMuted,
                      ),
                    ),
                    if (versionLabel != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        versionLabel!,
                        style: TextStyle(
                          fontSize: 11,
                          color: isDark
                              ? Colors.white.withValues(alpha: 0.35)
                              : AppColors.textMuted,
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

class SplashLoaderBar extends StatelessWidget {
  const SplashLoaderBar({
    super.key,
    required this.progress,
    required this.isDark,
    this.staticProgress = false,
  });

  final Animation<double> progress;
  final bool isDark;
  final bool staticProgress;

  @override
  Widget build(BuildContext context) {
    const width = 148.0;
    const height = 3.0;

    return AnimatedBuilder(
      animation: progress,
      builder: (context, _) {
        final t = staticProgress ? 0.4 : progress.value;
        final shimmer = (t * 2) % 1.0;
        return SizedBox(
          width: width,
          height: height,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(AppRadii.pill),
            child: Stack(
              fit: StackFit.expand,
              children: [
                ColoredBox(
                  color: isDark
                      ? Colors.white.withValues(alpha: 0.12)
                      : AppColors.primary.withValues(alpha: 0.15),
                ),
                Align(
                  alignment: Alignment(-1 + 2 * shimmer, 0),
                  child: Container(
                    width: width * 0.35,
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        colors: [
                          AppColors.primary.withValues(alpha: 0.2),
                          AppColors.primaryLight,
                          AppColors.accent,
                        ],
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}
