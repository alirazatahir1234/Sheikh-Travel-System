import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/api/dio_client.dart';
import '../../../core/config/app_config.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/providers/locale_provider.dart';
import '../../../core/providers/theme_provider.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';
import '../data/auth_repository.dart';
import '../domain/auth_models.dart';
import '../../dashboard/presentation/dashboard_notifier.dart';
import '../../trips/presentation/trips_notifier.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _identifierCtrl = TextEditingController();
  final _passwordCtrl = TextEditingController();
  final _formKey = GlobalKey<FormState>();
  bool _loading = false;
  bool _rememberMe = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final stored = ref.read(prefsBoxProvider).get(
            rememberMePrefsKey,
            defaultValue: true,
          );
      if (stored is bool && mounted) {
        setState(() => _rememberMe = stored);
      }
    });
  }

  @override
  void dispose() {
    _identifierCtrl.dispose();
    _passwordCtrl.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      await ref.read(prefsBoxProvider).put(rememberMePrefsKey, _rememberMe);
      await ref.read(authRepositoryProvider).login(
            LoginRequest(
              identifier: _identifierCtrl.text.trim(),
              password: _passwordCtrl.text,
              rememberMe: _rememberMe,
            ),
          );
      ref.invalidate(dashboardProvider);
      ref.invalidate(tripsProvider);
      if (mounted) context.go('/dashboard');
    } catch (e) {
      setState(() => _error = formatDioError(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final top = MediaQuery.paddingOf(context).top;

    return Scaffold(
      backgroundColor: p.surface,
      body: Column(
        children: [
          // Brand hero — matches splash navy, modern ride-hailing pattern.
          Container(
            width: double.infinity,
            padding: EdgeInsets.fromLTRB(24, top + 28, 24, 36),
            decoration: const BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  AppColors.splashNavyTop,
                  AppColors.splashNavyBottom,
                  Color(0xFF143D38),
                ],
              ),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 64,
                  height: 64,
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(AppRadii.lg),
                    border: Border.all(color: Colors.white.withValues(alpha: 0.18)),
                  ),
                  child: Image.asset(
                    'assets/branding/sheikhgo_logo.png',
                    fit: BoxFit.contain,
                  ),
                ),
                const SizedBox(height: 20),
                Text(
                  'SheikhGo Tracking',
                  style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w800,
                        letterSpacing: -0.5,
                      ),
                ),
                const SizedBox(height: 6),
                Text(
                  'Owners track the fleet · Drivers run trips',
                  style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        color: Colors.white.withValues(alpha: 0.72),
                      ),
                ),
              ],
            ),
          ),
          Expanded(
            child: Transform.translate(
              offset: const Offset(0, -18),
              child: SingleChildScrollView(
                padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
                child: Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      SgCard(
                        padding: const EdgeInsets.fromLTRB(18, 20, 18, 18),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            Text(
                              'Welcome back',
                              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                                    color: p.textPrimary,
                                    fontWeight: FontWeight.w800,
                                    letterSpacing: -0.35,
                                  ),
                            ),
                            const SizedBox(height: 6),
                            Text(
                              'Use your work email or phone number',
                              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                    color: p.textSecondary,
                                    height: 1.35,
                                  ),
                            ),
                            const SizedBox(height: 20),
                            // Tap → full-width sheet: avoids Gboard side toolbar
                            // (voice / clipboard / corrections) crushing the field.
                            SgSheetTextFormField(
                              controller: _identifierCtrl,
                              label: 'Email or phone',
                              hint: 'email or 03021234567',
                              keyboardType: TextInputType.emailAddress,
                              prefixIcon: Icons.person_outline_rounded,
                              validator: (v) => (v == null || v.trim().isEmpty)
                                  ? 'Email or phone is required'
                                  : null,
                            ),
                            const SizedBox(height: 12),
                            SgSheetTextFormField(
                              controller: _passwordCtrl,
                              label: 'Password',
                              obscureText: true,
                              keyboardType: TextInputType.visiblePassword,
                              prefixIcon: Icons.lock_outline_rounded,
                              validator: (v) => (v == null || v.isEmpty)
                                  ? 'Password is required'
                                  : null,
                            ),
                            const SizedBox(height: 8),
                            Row(
                              children: [
                                SizedBox(
                                  height: 28,
                                  width: 28,
                                  child: Checkbox(
                                    value: _rememberMe,
                                    onChanged: (v) =>
                                        setState(() => _rememberMe = v ?? true),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: Text(
                                    'Remember me',
                                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                          color: p.textSecondary,
                                        ),
                                  ),
                                ),
                                TextButton(
                                  onPressed: () => context.push('/forgot-password'),
                                  child: const Text(
                                    'Forgot?',
                                    style: TextStyle(
                                      fontSize: 13,
                                      fontWeight: FontWeight.w800,
                                      color: AppColors.primary,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                            if (_error != null) ...[
                              const SizedBox(height: 8),
                              Container(
                                padding: const EdgeInsets.all(12),
                                decoration: BoxDecoration(
                                  color: AppColors.error.withValues(alpha: 0.08),
                                  borderRadius:
                                      BorderRadius.circular(AppRadii.sm),
                                  border: Border.all(
                                    color: AppColors.error.withValues(alpha: 0.22),
                                  ),
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
                                          color: AppColors.error,
                                          fontSize: 13,
                                          fontWeight: FontWeight.w600,
                                          height: 1.3,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                            const SizedBox(height: 16),
                            SgPrimaryButton(
                              label: 'Continue',
                              loading: _loading,
                              onPressed: _login,
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 16),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          TextButton(
                            onPressed: () => context.push('/legal/terms'),
                            child: Text(
                              'Terms',
                              style: TextStyle(
                                fontSize: 12,
                                color: p.textSecondary,
                              ),
                            ),
                          ),
                          Text('·', style: TextStyle(color: p.textMuted)),
                          TextButton(
                            onPressed: () => context.push('/legal/privacy'),
                            child: Text(
                              'Privacy',
                              style: TextStyle(
                                fontSize: 12,
                                color: p.textSecondary,
                              ),
                            ),
                          ),
                        ],
                      ),
                      Text(
                        'v${AppConfig.appVersion}',
                        textAlign: TextAlign.center,
                        style: TextStyle(fontSize: 11, color: p.textMuted),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
