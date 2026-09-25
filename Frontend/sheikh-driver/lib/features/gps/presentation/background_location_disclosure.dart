import 'package:flutter/material.dart';

import '../../../core/constants/app_theme.dart';

/// Prominent in-app disclosure required by Play before requesting
/// “Allow all the time” / Always location for live trip tracking.
Future<bool> showBackgroundLocationDisclosure(BuildContext context) async {
  final result = await showDialog<bool>(
    context: context,
    barrierDismissible: false,
    builder: (ctx) => AlertDialog(
      title: const Text('Background location'),
      content: const SingleChildScrollView(
        child: Text(
          'SheikhGo Fleet collects your precise location while a trip is active, '
          'including when the app is in the background or the screen is off.\n\n'
          'This is used only for live trip and fleet GPS tracking so dispatch can '
          'see your vehicle location. Location sharing stops when you end tracking.\n\n'
          'On the next screen, choose “Allow all the time” (or Always) so tracking '
          'can continue reliably.',
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(ctx, false),
          child: const Text('Not now'),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(ctx, true),
          style: FilledButton.styleFrom(backgroundColor: AppColors.primary),
          child: const Text('Continue'),
        ),
      ],
    ),
  );
  return result == true;
}
