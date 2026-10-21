import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/auth_api.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';

/// Driver-facing forgot-password: request a phone reset code.
class ForgotPasswordScreen extends ConsumerStatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  ConsumerState<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends ConsumerState<ForgotPasswordScreen> {
  final _phone = TextEditingController();
  final _formKey = GlobalKey<FormState>();
  bool _busy = false;
  String? _info;

  @override
  void dispose() {
    _phone.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _info = null;
    });
    try {
      final debugToken = await ref.read(authApiProvider).forgotPassword(_phone.text);
      if (!mounted) return;
      final uri = Uri(
        path: '/reset-password',
        queryParameters: {
          if (debugToken != null && debugToken.isNotEmpty) 'token': debugToken,
          'phone': _phone.text.trim(),
        },
      );
      context.push(uri.toString());
    } catch (e) {
      if (!mounted) return;
      setState(() => _info = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(title: const Text('Forgot password')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
        children: [
          SgCard(
            padding: const EdgeInsets.fromLTRB(18, 18, 18, 18),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Reset via phone',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: p.textPrimary,
                        fontWeight: FontWeight.w800,
                        letterSpacing: -0.3,
                      ),
                ),
                const SizedBox(height: 8),
                Text(
                  'Enter the phone number on your driver account. If it matches, we send a reset code.',
                  style: TextStyle(
                    fontSize: 14,
                    color: p.textSecondary,
                    height: 1.4,
                  ),
                ),
                const SizedBox(height: 20),
                Form(
                  key: _formKey,
                  child: SgSheetTextFormField(
                    controller: _phone,
                    label: 'Phone',
                    hint: '+92…',
                    keyboardType: TextInputType.phone,
                    prefixIcon: Icons.phone_outlined,
                    validator: (v) =>
                        (v == null || v.trim().isEmpty) ? 'Phone is required' : null,
                  ),
                ),
                if (_info != null) ...[
                  const SizedBox(height: 12),
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: AppColors.error.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(AppRadii.sm),
                      border: Border.all(
                        color: AppColors.error.withValues(alpha: 0.22),
                      ),
                    ),
                    child: Text(
                      _info!,
                      style: const TextStyle(
                        color: AppColors.error,
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                        height: 1.3,
                      ),
                    ),
                  ),
                ],
                const SizedBox(height: 20),
                SizedBox(
                  height: 48,
                  child: FilledButton(
                    onPressed: _busy ? null : _submit,
                    child: _busy
                        ? const SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text(
                            'Send reset code',
                            style: TextStyle(fontWeight: FontWeight.w700),
                          ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 8),
          TextButton(
            onPressed: () => context.go('/login'),
            child: Text(
              'Back to login',
              style: TextStyle(
                fontWeight: FontWeight.w700,
                color: p.textSecondary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
