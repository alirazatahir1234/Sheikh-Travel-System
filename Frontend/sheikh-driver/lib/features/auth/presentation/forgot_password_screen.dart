import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../data/auth_api.dart';

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
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            'Enter the phone number on your driver account. If it matches, we send a reset code.',
            style: TextStyle(fontSize: 14, color: p.textMuted, height: 1.4),
          ),
          const SizedBox(height: 20),
          Form(
            key: _formKey,
            child: TextFormField(
              controller: _phone,
              keyboardType: TextInputType.phone,
              decoration: const InputDecoration(
                labelText: 'Phone',
                hintText: '+92…',
              ),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Phone is required' : null,
            ),
          ),
          if (_info != null) ...[
            const SizedBox(height: 12),
            Text(_info!, style: TextStyle(color: AppColors.error, fontSize: 13)),
          ],
          const SizedBox(height: 24),
          FilledButton(
            onPressed: _busy ? null : _submit,
            child: _busy
                ? const SizedBox(
                    height: 20,
                    width: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Send reset code'),
          ),
          TextButton(
            onPressed: () => context.go('/login'),
            child: const Text('Back to login'),
          ),
        ],
      ),
    );
  }
}
