import 'package:flutter/material.dart';

class AppColors {
  static const primary = Color(0xFF0F4D3A);
  static const primaryDark = Color(0xFF0B3D2E);
  static const primaryLight = Color(0xFF1B7F75);
  static const backgroundTop = Color(0xFF0A1F1A);
  static const backgroundBottom = Color(0xFF050F0D);
  static const card = Colors.white;
  static const text = Color(0xFF0F172A);
  static const textMuted = Color(0xFF64748B);
  static const textSoft = Color(0xFF94A3B8);
  static const border = Color(0xFFE2E8F0);
  static const link = Color(0xFF2563EB);

  // Enterprise sidebar palette
  static const sidebarBg = Color(0xFFF0F4F8);
  static const sidebarSurface = Color(0xFFE8EEF4);
  static const sidebarProfileBg = Color(0xFFDCE8F4);
  static const sidebarActiveBg = Color(0xFFD4E8E4);
  static const sidebarActiveText = Color(0xFF0D6B64);
  static const contentBg = Color(0xFFEEF2F6);
}

ThemeData buildSheikhGoTheme() {
  return ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: AppColors.primary,
      primary: AppColors.primary,
      surface: AppColors.card,
    ),
    scaffoldBackgroundColor: AppColors.backgroundBottom,
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.white,
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: const BorderSide(color: AppColors.border),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: const BorderSide(color: AppColors.border),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
      ),
      labelStyle: const TextStyle(color: AppColors.textMuted, fontSize: 13),
    ),
  );
}
