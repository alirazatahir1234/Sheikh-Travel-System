import 'package:flutter/material.dart';

/// SheikhGo Fleet — ERP-aligned design tokens
abstract class AppColors {
  /// Primary teal from Figma (~#1D836C / Material teal)
  static const primary = Color(0xFF1D836C);
  static const primaryLight = Color(0xFF2A9B82);
  static const primaryDark = Color(0xFF146652);
  static const accent = Color(0xFF0D9488);

  static const surface = Color(0xFFF5F7FA);
  static const cardBg = Color(0xFFFFFFFF);
  static const textPrimary = Color(0xFF1A2B3C);
  static const textSecondary = Color(0xFF6B7280);
  static const textMuted = Color(0xFF9CA3AF);

  static const success = Color(0xFF10B981);
  static const warning = Color(0xFFF59E0B);
  static const error = Color(0xFFEF4444);
  static const info = Color(0xFF3B82F6);
  static const cyan = Color(0xFF06B6D4);

  static const divider = Color(0xFFE5E7EB);
  static const border = Color(0xFFD1D5DB);
  static const chipBg = Color(0xFFF3F4F6);

  /// Splash / launch branding
  static const splashNavyTop = Color(0xFF0A1628);
  static const splashNavyBottom = Color(0xFF0F2137);
  static const splashLightTop = Color(0xFFF4F8FB);
  static const splashLightBottom = Color(0xFFFFFFFF);

  /// Trip status badge colors
  static Color statusColor(String status) {
    final s = status.toLowerCase().replaceAll(' ', '');
    return switch (s) {
      'scheduled' || 'confirmed' || 'assigned' => info,
      'accepted' || 'available' => cyan,
      'started' || 'enroute' || 'ontrip' || 'inprogress' => warning,
      'completed' || 'done' => success,
      'cancelled' || 'rejected' || 'suspended' => error,
      'arrived' || 'atpickup' || 'onboard' => primary,
      _ => textSecondary,
    };
  }
}

abstract class AppShadows {
  static List<BoxShadow> get card => [
        BoxShadow(
          color: const Color(0xFF1A2B3C).withValues(alpha: 0.06),
          blurRadius: 12,
          offset: const Offset(0, 4),
        ),
      ];
}

abstract class AppRadii {
  static const sm = 8.0;
  static const md = 12.0;
  static const lg = 16.0;
  static const xl = 20.0;
  static const pill = 999.0;
}

abstract class AppTheme {
  static ThemeData get dark => ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(
          seedColor: AppColors.primary,
          primary: AppColors.primaryLight,
          secondary: AppColors.accent,
          surface: const Color(0xFF1E2530),
          brightness: Brightness.dark,
        ),
        scaffoldBackgroundColor: const Color(0xFF141820),
        appBarTheme: const AppBarTheme(
          backgroundColor: Color(0xFF1A2130),
          foregroundColor: Colors.white,
          elevation: 0,
          centerTitle: true,
          titleTextStyle: TextStyle(
            color: Colors.white,
            fontSize: 18,
            fontWeight: FontWeight.w600,
          ),
        ),
        cardTheme: CardThemeData(
          color: const Color(0xFF1E2530),
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadii.md),
            side: const BorderSide(color: Color(0xFF2D3548)),
          ),
        ),
        filledButtonTheme: _filledButton(AppColors.primaryLight),
        outlinedButtonTheme: _outlinedButton(AppColors.primaryLight),
        inputDecorationTheme: _inputs(
          fill: const Color(0xFF252C3B),
          border: const Color(0xFF2D3548),
          focus: AppColors.primaryLight,
        ),
        bottomNavigationBarTheme: const BottomNavigationBarThemeData(
          selectedItemColor: AppColors.primaryLight,
          unselectedItemColor: Color(0xFF6B7280),
          backgroundColor: Color(0xFF1A2130),
          type: BottomNavigationBarType.fixed,
          elevation: 8,
          selectedLabelStyle: TextStyle(fontSize: 11, fontWeight: FontWeight.w600),
          unselectedLabelStyle: TextStyle(fontSize: 11),
        ),
      );

  static ThemeData get light => ThemeData(
        useMaterial3: true,
        fontFamily: null,
        colorScheme: ColorScheme.fromSeed(
          seedColor: AppColors.primary,
          primary: AppColors.primary,
          secondary: AppColors.accent,
          surface: AppColors.cardBg,
        ),
        scaffoldBackgroundColor: AppColors.surface,
        appBarTheme: const AppBarTheme(
          backgroundColor: AppColors.cardBg,
          foregroundColor: AppColors.textPrimary,
          elevation: 0,
          scrolledUnderElevation: 0.5,
          centerTitle: true,
          iconTheme: IconThemeData(color: AppColors.textPrimary),
          titleTextStyle: TextStyle(
            color: AppColors.textPrimary,
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        cardTheme: CardThemeData(
          color: AppColors.cardBg,
          elevation: 0,
          margin: EdgeInsets.zero,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadii.md),
          ),
        ),
        filledButtonTheme: _filledButton(AppColors.primary),
        outlinedButtonTheme: _outlinedButton(AppColors.primary),
        inputDecorationTheme: _inputs(
          fill: AppColors.cardBg,
          border: AppColors.border,
          focus: AppColors.primary,
        ),
        bottomNavigationBarTheme: const BottomNavigationBarThemeData(
          selectedItemColor: AppColors.primary,
          unselectedItemColor: AppColors.textMuted,
          backgroundColor: Colors.white,
          type: BottomNavigationBarType.fixed,
          elevation: 12,
          selectedLabelStyle: TextStyle(fontSize: 11, fontWeight: FontWeight.w600),
          unselectedLabelStyle: TextStyle(fontSize: 11),
        ),
        checkboxTheme: CheckboxThemeData(
          fillColor: WidgetStateProperty.resolveWith(
            (s) => s.contains(WidgetState.selected) ? AppColors.primary : null,
          ),
        ),
      );

  static FilledButtonThemeData _filledButton(Color bg) => FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: bg,
          foregroundColor: Colors.white,
          elevation: 0,
          minimumSize: const Size.fromHeight(48),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadii.md),
          ),
          textStyle: const TextStyle(
            fontSize: 15,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.4,
          ),
          padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 24),
        ),
      );

  static OutlinedButtonThemeData _outlinedButton(Color color) =>
      OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: color,
          minimumSize: const Size.fromHeight(48),
          side: BorderSide(color: color, width: 1.5),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadii.md),
          ),
          textStyle: const TextStyle(
            fontSize: 15,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.4,
          ),
        ),
      );

  static InputDecorationTheme _inputs({
    required Color fill,
    required Color border,
    required Color focus,
  }) =>
      InputDecorationTheme(
        filled: true,
        fillColor: fill,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
          borderSide: BorderSide(color: border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
          borderSide: BorderSide(color: border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
          borderSide: BorderSide(color: focus, width: 1.5),
        ),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        labelStyle: const TextStyle(color: AppColors.textSecondary),
        hintStyle: const TextStyle(color: AppColors.textMuted),
      );
}
