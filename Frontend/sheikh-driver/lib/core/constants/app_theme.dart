import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Brand + semantic colors (mode-independent).
/// Prefer [SgPalette.of] / [BuildContext.sg] for surfaces and text in UI.
abstract class AppColors {
  /// Brand — Deep Navy, Teal, Emerald (Settings / marketing parity).
  static const navy = Color(0xFF071D2E);
  static const emerald = Color(0xFF14B8A6);

  static const primary = Color(0xFF0D9488);
  static const primaryLight = Color(0xFF14B8A6);
  static const primaryDark = Color(0xFF0F766E);
  static const accent = Color(0xFF0D9488);

  /// Legacy light-mode aliases — prefer Theme / [SgPalette] in new code.
  static const surface = Color(0xFFF4F8FA);
  static const cardBg = Color(0xFFFFFFFF);
  static const textPrimary = Color(0xFF0F1C27);
  static const textSecondary = Color(0xFF5B6B7C);
  static const textMuted = Color(0xFF8B97A5);

  static const success = Color(0xFF0E9F6E);
  static const warning = Color(0xFFE3A008);
  static const error = Color(0xFFE11D48);
  static const info = Color(0xFF2563EB);
  static const cyan = Color(0xFF0891B2);

  static const divider = Color(0xFFE6EBF0);
  static const border = Color(0xFFD5DEE7);
  static const chipBg = Color(0xFFEEF3F6);

  static const splashNavyTop = Color(0xFF0A1628);
  static const splashNavyBottom = Color(0xFF0F2137);
  static const splashLightTop = Color(0xFFF4F8FB);
  static const splashLightBottom = Color(0xFFFFFFFF);

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

/// Theme-aware surfaces / text — modern apps resolve these from the active mode.
@immutable
class SgPalette extends ThemeExtension<SgPalette> {
  const SgPalette({
    required this.surface,
    required this.card,
    required this.textPrimary,
    required this.textSecondary,
    required this.textMuted,
    required this.border,
    required this.divider,
    required this.chipBg,
    required this.navBar,
    required this.heroWash,
  });

  final Color surface;
  final Color card;
  final Color textPrimary;
  final Color textSecondary;
  final Color textMuted;
  final Color border;
  final Color divider;
  final Color chipBg;
  final Color navBar;
  final Color heroWash;

  static const light = SgPalette(
    surface: Color(0xFFF4F8FA),
    card: Color(0xFFFFFFFF),
    textPrimary: Color(0xFF0F1C27),
    textSecondary: Color(0xFF5B6B7C),
    textMuted: Color(0xFF8B97A5),
    border: Color(0xFFD5DEE7),
    divider: Color(0xFFE6EBF0),
    chipBg: Color(0xFFEEF3F6),
    navBar: Color(0xFFFFFFFF),
    heroWash: Color(0xFF0D9488),
  );

  static const dark = SgPalette(
    surface: Color(0xFF0E141B),
    card: Color(0xFF171E27),
    textPrimary: Color(0xFFF2F5F8),
    textSecondary: Color(0xFFA8B3C0),
    textMuted: Color(0xFF7A8796),
    border: Color(0xFF2A3441),
    divider: Color(0xFF243040),
    chipBg: Color(0xFF1F2833),
    navBar: Color(0xFF121820),
    heroWash: Color(0xFF2A9B82),
  );

  static SgPalette of(BuildContext context) =>
      Theme.of(context).extension<SgPalette>() ?? light;

  @override
  SgPalette copyWith({
    Color? surface,
    Color? card,
    Color? textPrimary,
    Color? textSecondary,
    Color? textMuted,
    Color? border,
    Color? divider,
    Color? chipBg,
    Color? navBar,
    Color? heroWash,
  }) {
    return SgPalette(
      surface: surface ?? this.surface,
      card: card ?? this.card,
      textPrimary: textPrimary ?? this.textPrimary,
      textSecondary: textSecondary ?? this.textSecondary,
      textMuted: textMuted ?? this.textMuted,
      border: border ?? this.border,
      divider: divider ?? this.divider,
      chipBg: chipBg ?? this.chipBg,
      navBar: navBar ?? this.navBar,
      heroWash: heroWash ?? this.heroWash,
    );
  }

  @override
  SgPalette lerp(ThemeExtension<SgPalette>? other, double t) {
    if (other is! SgPalette) return this;
    return SgPalette(
      surface: Color.lerp(surface, other.surface, t)!,
      card: Color.lerp(card, other.card, t)!,
      textPrimary: Color.lerp(textPrimary, other.textPrimary, t)!,
      textSecondary: Color.lerp(textSecondary, other.textSecondary, t)!,
      textMuted: Color.lerp(textMuted, other.textMuted, t)!,
      border: Color.lerp(border, other.border, t)!,
      divider: Color.lerp(divider, other.divider, t)!,
      chipBg: Color.lerp(chipBg, other.chipBg, t)!,
      navBar: Color.lerp(navBar, other.navBar, t)!,
      heroWash: Color.lerp(heroWash, other.heroWash, t)!,
    );
  }
}

extension SgPaletteX on BuildContext {
  SgPalette get sg => SgPalette.of(this);
}

abstract class AppSpacing {
  static const xs = 4.0;
  static const sm = 8.0;
  static const md = 12.0;
  static const lg = 16.0;
  static const xl = 20.0;
  static const xxl = 24.0;
  static const huge = 32.0;
  static const screen = EdgeInsets.fromLTRB(20, 12, 20, 24);
}

abstract class AppShadows {
  /// Soft ops-card lift — readable on light surface without glow stacks.
  static List<BoxShadow> get card => [
        BoxShadow(
          color: const Color(0xFF0F1C27).withValues(alpha: 0.045),
          blurRadius: 16,
          offset: const Offset(0, 6),
        ),
        BoxShadow(
          color: const Color(0xFF0F1C27).withValues(alpha: 0.03),
          blurRadius: 4,
          offset: const Offset(0, 1),
        ),
      ];

  static List<BoxShadow> get nav => [
        BoxShadow(
          color: Colors.black.withValues(alpha: 0.08),
          blurRadius: 24,
          offset: const Offset(0, -6),
        ),
      ];
}

abstract class AppRadii {
  static const sm = 10.0;
  static const md = 14.0;
  static const lg = 18.0;
  static const xl = 24.0;
  static const pill = 999.0;
}

abstract class AppTheme {
  static TextTheme _textTheme(Brightness brightness) {
    final base = GoogleFonts.plusJakartaSansTextTheme();
    final onSurface =
        brightness == Brightness.dark ? SgPalette.dark.textPrimary : SgPalette.light.textPrimary;
    final muted =
        brightness == Brightness.dark ? SgPalette.dark.textSecondary : SgPalette.light.textSecondary;
    return base.copyWith(
      displaySmall: base.displaySmall?.copyWith(
        fontWeight: FontWeight.w800,
        letterSpacing: -0.6,
        color: onSurface,
      ),
      headlineMedium: base.headlineMedium?.copyWith(
        fontWeight: FontWeight.w800,
        letterSpacing: -0.4,
        color: onSurface,
      ),
      headlineSmall: base.headlineSmall?.copyWith(
        fontWeight: FontWeight.w700,
        letterSpacing: -0.3,
        color: onSurface,
      ),
      titleLarge: base.titleLarge?.copyWith(
        fontWeight: FontWeight.w700,
        color: onSurface,
      ),
      titleMedium: base.titleMedium?.copyWith(
        fontWeight: FontWeight.w600,
        color: onSurface,
      ),
      bodyLarge: base.bodyLarge?.copyWith(color: onSurface, height: 1.4),
      bodyMedium: base.bodyMedium?.copyWith(color: muted, height: 1.4),
      labelLarge: base.labelLarge?.copyWith(
        fontWeight: FontWeight.w700,
        letterSpacing: 0.1,
      ),
    );
  }

  static ThemeData get dark => _build(
        brightness: Brightness.dark,
        palette: SgPalette.dark,
        primary: AppColors.primaryLight,
        scaffold: SgPalette.dark.surface,
        appBarBg: SgPalette.dark.card,
        appBarFg: SgPalette.dark.textPrimary,
        inputFill: const Color(0xFF1C2430),
      );

  static ThemeData get light => _build(
        brightness: Brightness.light,
        palette: SgPalette.light,
        primary: AppColors.primary,
        scaffold: SgPalette.light.surface,
        appBarBg: SgPalette.light.card,
        appBarFg: SgPalette.light.textPrimary,
        inputFill: SgPalette.light.card,
      );

  static ThemeData _build({
    required Brightness brightness,
    required SgPalette palette,
    required Color primary,
    required Color scaffold,
    required Color appBarBg,
    required Color appBarFg,
    required Color inputFill,
  }) {
    final text = _textTheme(brightness);
    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      textTheme: text,
      colorScheme: ColorScheme.fromSeed(
        seedColor: AppColors.primary,
        primary: primary,
        secondary: AppColors.accent,
        surface: palette.card,
        brightness: brightness,
        error: AppColors.error,
      ),
      scaffoldBackgroundColor: scaffold,
      extensions: [palette],
      appBarTheme: AppBarTheme(
        backgroundColor: appBarBg,
        foregroundColor: appBarFg,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: text.titleLarge?.copyWith(fontSize: 18),
        iconTheme: IconThemeData(color: appBarFg),
      ),
      cardTheme: CardThemeData(
        color: palette.card,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadii.lg),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: primary,
          foregroundColor: Colors.white,
          elevation: 0,
          minimumSize: const Size.fromHeight(52),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadii.lg),
          ),
          textStyle: text.labelLarge?.copyWith(fontSize: 16),
          padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 22),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: primary,
          minimumSize: const Size.fromHeight(52),
          side: BorderSide(color: primary.withValues(alpha: 0.45), width: 1.4),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadii.lg),
          ),
          textStyle: text.labelLarge?.copyWith(fontSize: 15),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: inputFill,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
          borderSide: BorderSide(color: palette.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
          borderSide: BorderSide(color: palette.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
          borderSide: BorderSide(color: primary, width: 1.6),
        ),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        labelStyle: TextStyle(color: palette.textSecondary),
        hintStyle: TextStyle(color: palette.textMuted),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: palette.navBar,
        indicatorColor: primary.withValues(alpha: 0.14),
        labelTextStyle: WidgetStateProperty.resolveWith((states) {
          final selected = states.contains(WidgetState.selected);
          return text.labelLarge?.copyWith(
                fontSize: 11,
                fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                color: selected ? primary : palette.textMuted,
              );
        }),
        iconTheme: WidgetStateProperty.resolveWith((states) {
          final selected = states.contains(WidgetState.selected);
          return IconThemeData(
            size: 24,
            color: selected ? primary : palette.textMuted,
          );
        }),
      ),
      bottomNavigationBarTheme: BottomNavigationBarThemeData(
        selectedItemColor: primary,
        unselectedItemColor: palette.textMuted,
        backgroundColor: palette.navBar,
        type: BottomNavigationBarType.fixed,
        elevation: 0,
        selectedLabelStyle: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700),
        unselectedLabelStyle: const TextStyle(fontSize: 11),
      ),
      checkboxTheme: CheckboxThemeData(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(6)),
        fillColor: WidgetStateProperty.resolveWith(
          (s) => s.contains(WidgetState.selected) ? primary : null,
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(AppRadii.md)),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: palette.chipBg,
        selectedColor: primary.withValues(alpha: 0.14),
        disabledColor: palette.chipBg,
        labelStyle: text.labelLarge?.copyWith(
          fontSize: 13,
          fontWeight: FontWeight.w600,
          color: palette.textSecondary,
        ),
        secondaryLabelStyle: text.labelLarge?.copyWith(
          fontSize: 13,
          fontWeight: FontWeight.w700,
          color: primary,
        ),
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 0),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadii.md),
          side: BorderSide(color: palette.border.withValues(alpha: 0.8)),
        ),
        side: BorderSide(color: palette.border.withValues(alpha: 0.8)),
        showCheckmark: false,
      ),
      dividerTheme: DividerThemeData(
        color: palette.divider,
        thickness: 1,
        space: 1,
      ),
    );
  }
}
