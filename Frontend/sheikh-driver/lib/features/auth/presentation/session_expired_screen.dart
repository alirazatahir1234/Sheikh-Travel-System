import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_theme.dart';
import '../data/auth_repository.dart';

class SessionExpiredScreen extends ConsumerWidget {
  const SessionExpiredScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      backgroundColor: context.sg.surface,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(
                Icons.lock_clock_outlined,
                size: 56,
                color: AppColors.warning,
              ),
              const SizedBox(height: 16),
              Text(
                'Session expired',
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w800,
                  color: context.sg.textPrimary,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                'Your sign-in expired or is no longer valid. Sign in again to continue monitoring the fleet.',
                textAlign: TextAlign.center,
                style: TextStyle(color: context.sg.textSecondary, height: 1.4),
              ),
              const SizedBox(height: 28),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: () {
                    ref.read(authRepositoryProvider).clearSessionExpiredFlag();
                    context.go('/login');
                  },
                  child: const Text('Sign in again'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
