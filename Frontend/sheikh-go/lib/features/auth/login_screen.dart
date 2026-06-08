import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/auth_api_client.dart';
import '../../core/constants/app_theme.dart';
import '../../core/services/session_storage.dart';
import '../home/home_screen.dart';

enum _LoginTab { phone, email }

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  _LoginTab _tab = _LoginTab.phone;
  var _hidePassword = true;
  var _rememberMe = false;
  var _loading = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadRememberedLogin();
  }

  Future<void> _loadRememberedLogin() async {
    final remembered = await ref.read(rememberedLoginProvider.future);
    if (!mounted || remembered == null || remembered.isEmpty) return;
    setState(() {
      _rememberMe = true;
      if (remembered.contains('@')) {
        _tab = _LoginTab.email;
        _emailController.text = remembered;
      } else {
        _tab = _LoginTab.phone;
        _phoneController.text = remembered;
      }
    });
  }

  @override
  void dispose() {
    _phoneController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  String get _identifier {
    return _tab == _LoginTab.phone
        ? _phoneController.text.trim()
        : _emailController.text.trim();
  }

  Future<void> _login() async {
    final identifier = _identifier;
    final password = _passwordController.text;

    if (identifier.isEmpty || password.isEmpty) {
      setState(() => _error = 'Enter your ${_tab == _LoginTab.phone ? 'mobile number' : 'email'} and password.');
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final session = await ref.read(authApiProvider).login(
            emailOrPhone: identifier,
            password: password,
          );
      await ref.read(sessionProvider.notifier).setSession(session);
      if (_rememberMe) {
        await ref.read(sessionProvider.notifier).setRememberedLogin(identifier);
      } else {
        await ref.read(sessionProvider.notifier).setRememberedLogin(null);
      }
      if (!mounted) return;
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (_) => const HomeScreen()),
      );
    } on DioException catch (e) {
      setState(() => _error = formatDioError(e));
    } catch (e) {
      setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _showComingSoon(String feature) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('$feature is not available yet.')),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [AppColors.backgroundTop, AppColors.backgroundBottom],
          ),
        ),
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 420),
                child: Column(
                  children: [
                    const _BrandHeader(),
                    const SizedBox(height: 28),
                    _LoginCard(
                      tab: _tab,
                      onTabChanged: (tab) => setState(() => _tab = tab),
                      phoneController: _phoneController,
                      emailController: _emailController,
                      passwordController: _passwordController,
                      hidePassword: _hidePassword,
                      onTogglePassword: () =>
                          setState(() => _hidePassword = !_hidePassword),
                      rememberMe: _rememberMe,
                      onRememberMeChanged: (v) => setState(() => _rememberMe = v),
                      loading: _loading,
                      error: _error,
                      onSubmit: _login,
                      onForgotPassword: () => _showComingSoon('Password reset'),
                      onGoogle: () => _showComingSoon('Google sign-in'),
                      onSso: () => _showComingSoon('SSO'),
                    ),
                    const SizedBox(height: 24),
                    _CreateAccountFooter(
                      onTap: () => _showComingSoon('Account registration'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _BrandHeader extends StatelessWidget {
  const _BrandHeader();

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          'SheikhGo',
          style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                color: AppColors.primary,
                fontWeight: FontWeight.w800,
                letterSpacing: -0.5,
              ),
        ),
        const SizedBox(height: 6),
        Text(
          'Sheikh Travel Customer Hub',
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                color: Colors.white54,
              ),
        ),
      ],
    );
  }
}

class _LoginCard extends StatelessWidget {
  const _LoginCard({
    required this.tab,
    required this.onTabChanged,
    required this.phoneController,
    required this.emailController,
    required this.passwordController,
    required this.hidePassword,
    required this.onTogglePassword,
    required this.rememberMe,
    required this.onRememberMeChanged,
    required this.loading,
    required this.error,
    required this.onSubmit,
    required this.onForgotPassword,
    required this.onGoogle,
    required this.onSso,
  });

  final _LoginTab tab;
  final ValueChanged<_LoginTab> onTabChanged;
  final TextEditingController phoneController;
  final TextEditingController emailController;
  final TextEditingController passwordController;
  final bool hidePassword;
  final VoidCallback onTogglePassword;
  final bool rememberMe;
  final ValueChanged<bool> onRememberMeChanged;
  final bool loading;
  final String? error;
  final VoidCallback onSubmit;
  final VoidCallback onForgotPassword;
  final VoidCallback onGoogle;
  final VoidCallback onSso;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(22, 18, 22, 24),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(22),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.25),
            blurRadius: 30,
            offset: const Offset(0, 16),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _TabBar(tab: tab, onChanged: onTabChanged),
          const SizedBox(height: 22),
          if (tab == _LoginTab.phone) ...[
            _FieldLabel('Mobile Number'),
            const SizedBox(height: 8),
            TextField(
              controller: phoneController,
              keyboardType: TextInputType.phone,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(
                hintText: '03001234567',
                suffixIcon: Icon(Icons.phone_outlined, color: AppColors.textMuted),
              ),
            ),
          ] else ...[
            _FieldLabel('Email address'),
            const SizedBox(height: 8),
            TextField(
              controller: emailController,
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              autocorrect: false,
              decoration: const InputDecoration(
                hintText: 'admin@sheikhtravel.com',
                suffixIcon: Icon(Icons.mail_outline, color: AppColors.textMuted),
              ),
            ),
          ],
          const SizedBox(height: 18),
          Row(
            children: [
              const Expanded(child: _FieldLabel('Access Code / Password')),
              TextButton(
                onPressed: onForgotPassword,
                style: TextButton.styleFrom(
                  foregroundColor: AppColors.link,
                  padding: EdgeInsets.zero,
                  minimumSize: Size.zero,
                  tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                ),
                child: const Text('Forgot password', style: TextStyle(fontSize: 13)),
              ),
            ],
          ),
          const SizedBox(height: 8),
          TextField(
            controller: passwordController,
            obscureText: hidePassword,
            textInputAction: TextInputAction.done,
            onSubmitted: (_) => onSubmit(),
            decoration: InputDecoration(
              hintText: '••••••••',
              suffixIcon: IconButton(
                onPressed: onTogglePassword,
                icon: Icon(
                  hidePassword ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                  color: AppColors.textMuted,
                ),
              ),
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              Switch(
                value: rememberMe,
                onChanged: onRememberMeChanged,
                activeThumbColor: Colors.white,
                activeTrackColor: AppColors.primary,
              ),
              const Text('Remember me', style: TextStyle(color: AppColors.textMuted)),
            ],
          ),
          if (error != null) ...[
            const SizedBox(height: 8),
            Text(error!, style: const TextStyle(color: Colors.red, fontSize: 13)),
          ],
          const SizedBox(height: 12),
          SizedBox(
            height: 52,
            child: FilledButton(
              onPressed: loading ? null : onSubmit,
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary,
                foregroundColor: Colors.white,
                shape: const StadiumBorder(),
                textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
              ),
              child: loading
                  ? const SizedBox(
                      width: 22,
                      height: 22,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : const Text('Sign in'),
            ),
          ),
          const SizedBox(height: 22),
          const Row(
            children: [
              Expanded(child: Divider(color: AppColors.border)),
              Padding(
                padding: EdgeInsets.symmetric(horizontal: 12),
                child: Text('or continue with', style: TextStyle(color: AppColors.textMuted, fontSize: 13)),
              ),
              Expanded(child: Divider(color: AppColors.border)),
            ],
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: _SocialButton(
                  label: 'Google',
                  icon: Icons.g_mobiledata,
                  onPressed: onGoogle,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _SocialButton(
                  label: 'SSO',
                  icon: Icons.vpn_key_outlined,
                  onPressed: onSso,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _TabBar extends StatelessWidget {
  const _TabBar({required this.tab, required this.onChanged});

  final _LoginTab tab;
  final ValueChanged<_LoginTab> onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        _TabButton(
          label: 'Phone',
          selected: tab == _LoginTab.phone,
          onTap: () => onChanged(_LoginTab.phone),
        ),
        _TabButton(
          label: 'Email',
          selected: tab == _LoginTab.email,
          onTap: () => onChanged(_LoginTab.email),
        ),
      ],
    );
  }
}

class _TabButton extends StatelessWidget {
  const _TabButton({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: InkWell(
        onTap: onTap,
        child: Column(
          children: [
            Text(
              label,
              style: TextStyle(
                fontSize: 15,
                fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                color: selected ? AppColors.text : AppColors.textMuted,
              ),
            ),
            const SizedBox(height: 10),
            AnimatedContainer(
              duration: const Duration(milliseconds: 180),
              height: 3,
              decoration: BoxDecoration(
                color: selected ? AppColors.primary : Colors.transparent,
                borderRadius: BorderRadius.circular(99),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _FieldLabel extends StatelessWidget {
  const _FieldLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: AppColors.textMuted,
        fontSize: 13,
        fontWeight: FontWeight.w500,
      ),
    );
  }
}

class _SocialButton extends StatelessWidget {
  const _SocialButton({
    required this.label,
    required this.icon,
    required this.onPressed,
  });

  final String label;
  final IconData icon;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      onPressed: onPressed,
      icon: Icon(icon, size: 20),
      label: Text(label),
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.text,
        side: const BorderSide(color: AppColors.border),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
        padding: const EdgeInsets.symmetric(vertical: 14),
      ),
    );
  }
}

class _CreateAccountFooter extends StatelessWidget {
  const _CreateAccountFooter({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: RichText(
        text: TextSpan(
          style: const TextStyle(color: Colors.white54, fontSize: 14),
          children: [
            const TextSpan(text: 'New here? '),
            TextSpan(
              text: 'Create account',
              style: TextStyle(
                color: AppColors.primary.withValues(alpha: 0.95),
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
