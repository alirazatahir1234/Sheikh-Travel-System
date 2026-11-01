import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../core/constants/app_theme.dart';

/// Shared keyboard-safe input patterns for sheikh-driver.
///
/// Causes addressed:
/// 1. Gboard floating toolbar crushing inline fields → [SgSheetTextFormField]
/// 2. Narrow / suffix-crushed hit targets → full-width sheet / pad entry
/// 3. Search fields without controllers losing focus on provider rebuild → [SgSearchField]
/// 4. setState swapping suffixIcon mid-keystroke → listenable clear button
/// 5. Modal sheets ignoring viewInsets → [showSgTextEntrySheet] pads keyboard
/// 6. Password / email / phone IME chrome → sheet + disabled suggestions
/// 7. Bottom composers crushed by keyboard chrome → [sgImeSafeTextField]
/// 8. Numeric fields rejecting input → [SgSheetAmountFormField] custom pad
/// 9. Dialog fields under keyboard → padded [showSgTextEntrySheet] / insets

TextField sgImeSafeTextField({
  Key? key,
  TextEditingController? controller,
  FocusNode? focusNode,
  InputDecoration? decoration,
  TextInputType? keyboardType,
  TextInputAction? textInputAction,
  bool obscureText = false,
  bool autofocus = false,
  bool enabled = true,
  int? maxLines = 1,
  int? minLines,
  int? maxLength,
  bool expands = false,
  TextCapitalization textCapitalization = TextCapitalization.none,
  List<TextInputFormatter>? inputFormatters,
  ValueChanged<String>? onChanged,
  ValueChanged<String>? onSubmitted,
  Iterable<String>? autofillHints,
  bool readOnly = false,
  String? obscuringCharacter,
}) {
  return TextField(
    key: key,
    controller: controller,
    focusNode: focusNode,
    decoration: decoration,
    keyboardType: keyboardType,
    textInputAction: textInputAction,
    obscureText: obscureText,
    autofocus: autofocus,
    enabled: enabled,
    maxLines: obscureText ? 1 : maxLines,
    minLines: obscureText ? null : minLines,
    maxLength: maxLength,
    expands: expands,
    textCapitalization: textCapitalization,
    inputFormatters: inputFormatters,
    onChanged: onChanged,
    onSubmitted: onSubmitted,
    autofillHints: autofillHints ?? const <String>[],
    readOnly: readOnly,
    obscuringCharacter: obscuringCharacter ?? '•',
    autocorrect: false,
    enableSuggestions: false,
    enableIMEPersonalizedLearning: false,
    spellCheckConfiguration: const SpellCheckConfiguration.disabled(),
    scrollPadding: const EdgeInsets.all(80),
  );
}

TextFormField sgImeSafeTextFormField({
  Key? key,
  TextEditingController? controller,
  FocusNode? focusNode,
  InputDecoration? decoration,
  TextInputType? keyboardType,
  TextInputAction? textInputAction,
  bool obscureText = false,
  bool autofocus = false,
  bool enabled = true,
  int? maxLines = 1,
  int? minLines,
  TextCapitalization textCapitalization = TextCapitalization.none,
  List<TextInputFormatter>? inputFormatters,
  ValueChanged<String>? onChanged,
  FormFieldValidator<String>? validator,
  Iterable<String>? autofillHints,
}) {
  return TextFormField(
    key: key,
    controller: controller,
    focusNode: focusNode,
    decoration: decoration,
    keyboardType: keyboardType,
    textInputAction: textInputAction,
    obscureText: obscureText,
    autofocus: autofocus,
    enabled: enabled,
    maxLines: obscureText ? 1 : maxLines,
    minLines: obscureText ? null : minLines,
    textCapitalization: textCapitalization,
    inputFormatters: inputFormatters,
    onChanged: onChanged,
    validator: validator,
    autofillHints: autofillHints ?? const <String>[],
    autocorrect: false,
    enableSuggestions: false,
    enableIMEPersonalizedLearning: false,
    spellCheckConfiguration: const SpellCheckConfiguration.disabled(),
    scrollPadding: const EdgeInsets.all(80),
  );
}

/// Opens a full-width bottom sheet for text entry (avoids Gboard side toolbar).
Future<String?> showSgTextEntrySheet(
  BuildContext context, {
  required String title,
  String initialValue = '',
  String? hint,
  TextInputType keyboardType = TextInputType.text,
  bool obscureText = false,
  TextCapitalization textCapitalization = TextCapitalization.none,
  int maxLines = 1,
}) {
  return showModalBottomSheet<String>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (ctx) => _SgTextEntrySheet(
      title: title,
      initialValue: initialValue,
      hint: hint,
      keyboardType: keyboardType,
      obscureText: obscureText,
      textCapitalization: textCapitalization,
      maxLines: maxLines,
    ),
  );
}

class _SgTextEntrySheet extends StatefulWidget {
  const _SgTextEntrySheet({
    required this.title,
    required this.initialValue,
    this.hint,
    required this.keyboardType,
    required this.obscureText,
    required this.textCapitalization,
    required this.maxLines,
  });

  final String title;
  final String initialValue;
  final String? hint;
  final TextInputType keyboardType;
  final bool obscureText;
  final TextCapitalization textCapitalization;
  final int maxLines;

  @override
  State<_SgTextEntrySheet> createState() => _SgTextEntrySheetState();
}

class _SgTextEntrySheetState extends State<_SgTextEntrySheet> {
  late final TextEditingController _ctrl;
  final _focus = FocusNode();
  bool _obscure = true;

  @override
  void initState() {
    super.initState();
    _ctrl = TextEditingController(text: widget.initialValue);
    _obscure = widget.obscureText;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _focus.requestFocus();
    });
  }

  @override
  void dispose() {
    _ctrl.dispose();
    _focus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    final bottom = MediaQuery.viewInsetsOf(context).bottom;
    return Padding(
      padding: EdgeInsets.only(
        left: 20,
        right: 20,
        top: 12,
        bottom: bottom + MediaQuery.paddingOf(context).bottom + 16,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Center(
            child: Container(
              width: 36,
              height: 4,
              decoration: BoxDecoration(
                color: p.border.withValues(alpha: 0.9),
                borderRadius: BorderRadius.circular(2),
              ),
            ),
          ),
          const SizedBox(height: 16),
          Text(
            widget.title,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              letterSpacing: -0.3,
              color: p.textPrimary,
            ),
          ),
          const SizedBox(height: 14),
          sgImeSafeTextField(
            controller: _ctrl,
            focusNode: _focus,
            keyboardType: widget.keyboardType,
            textInputAction: TextInputAction.done,
            obscureText: widget.obscureText && _obscure,
            textCapitalization: widget.textCapitalization,
            maxLines: widget.obscureText ? 1 : widget.maxLines,
            decoration: InputDecoration(
              hintText: widget.hint,
              filled: true,
              fillColor: p.chipBg.withValues(alpha: 0.55),
              suffixIcon: widget.obscureText
                  ? IconButton(
                      icon: Icon(
                        _obscure
                            ? Icons.visibility_outlined
                            : Icons.visibility_off_outlined,
                        color: p.textSecondary,
                      ),
                      onPressed: () => setState(() => _obscure = !_obscure),
                    )
                  : null,
            ),
            onSubmitted: (v) => Navigator.pop(context, v.trim()),
          ),
          const SizedBox(height: 16),
          SizedBox(
            height: 48,
            child: FilledButton(
              onPressed: () => Navigator.pop(context, _ctrl.text.trim()),
              child: const Text(
                'Done',
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Tap → sheet text entry. Use for form fields Gboard would otherwise cover.
class SgSheetTextFormField extends StatelessWidget {
  const SgSheetTextFormField({
    super.key,
    required this.controller,
    required this.label,
    this.hint,
    this.validator,
    this.keyboardType = TextInputType.text,
    this.obscureText = false,
    this.textCapitalization = TextCapitalization.none,
    this.maxLines = 1,
    this.prefixIcon,
    this.helperExample,
  });

  final TextEditingController controller;
  final String label;
  final String? hint;
  final String? Function(String?)? validator;
  final TextInputType keyboardType;
  final bool obscureText;
  final TextCapitalization textCapitalization;
  final int maxLines;
  final IconData? prefixIcon;
  final String? helperExample;

  Future<void> _open(BuildContext context, FormFieldState<String> state) async {
    final result = await showSgTextEntrySheet(
      context,
      title: label,
      initialValue: controller.text,
      hint: hint ?? helperExample,
      keyboardType: keyboardType,
      obscureText: obscureText,
      textCapitalization: textCapitalization,
      maxLines: maxLines,
    );
    if (result != null) {
      controller.text = result;
      controller.selection =
          TextSelection.collapsed(offset: controller.text.length);
      state.didChange(controller.text);
      state.validate();
    }
  }

  @override
  Widget build(BuildContext context) {
    return FormField<String>(
      validator: (_) => validator?.call(controller.text),
      builder: (state) {
        return ListenableBuilder(
          listenable: controller,
          builder: (context, _) {
            final value = controller.text;
            final display = obscureText && value.isNotEmpty
                ? '•' * value.length.clamp(0, 24)
                : value.trim();
            return InkWell(
              onTap: () => _open(context, state),
              borderRadius: BorderRadius.circular(AppRadii.sm),
              child: InputDecorator(
                isEmpty: value.trim().isEmpty,
                decoration: InputDecoration(
                  labelText: label,
                  hintText: hint ?? helperExample,
                  errorText: state.errorText,
                  prefixIcon: prefixIcon != null ? Icon(prefixIcon) : null,
                  suffixIcon: Icon(
                    obscureText
                        ? Icons.lock_outline_rounded
                        : Icons.edit_outlined,
                    size: 20,
                    color: context.sg.textSecondary,
                  ),
                ),
                child: Text(
                  display.isEmpty ? ' ' : display,
                  maxLines: maxLines.clamp(1, 3),
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    letterSpacing: -0.15,
                    color: display.isEmpty
                        ? Colors.transparent
                        : context.sg.textPrimary,
                  ),
                ),
              ),
            );
          },
        );
      },
    );
  }
}

/// Tap → custom number pad (no system keyboard toolbar).
class SgSheetAmountFormField extends StatelessWidget {
  const SgSheetAmountFormField({
    super.key,
    required this.controller,
    required this.label,
    required this.unit,
    this.validator,
    this.helperExample,
    this.allowDecimal = true,
  });

  final TextEditingController controller;
  final String label;
  final String unit;
  final String? helperExample;
  final bool allowDecimal;
  final String? Function(String?)? validator;

  Future<void> _open(BuildContext context, FormFieldState<String> state) async {
    final result = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      builder: (ctx) => _SgAmountPadSheet(
        title: label,
        unit: unit,
        initialValue: controller.text,
        allowDecimal: allowDecimal,
      ),
    );
    if (result != null) {
      controller.text = result;
      controller.selection =
          TextSelection.collapsed(offset: controller.text.length);
      state.didChange(controller.text);
      state.validate();
    }
  }

  @override
  Widget build(BuildContext context) {
    return FormField<String>(
      validator: (_) => validator?.call(controller.text),
      builder: (state) {
        return ListenableBuilder(
          listenable: controller,
          builder: (context, _) {
            final value = controller.text.trim();
            return InkWell(
              onTap: () => _open(context, state),
              borderRadius: BorderRadius.circular(AppRadii.sm),
              child: InputDecorator(
                isEmpty: value.isEmpty,
                decoration: InputDecoration(
                  labelText: label,
                  hintText: helperExample,
                  errorText: state.errorText,
                  suffixIcon: Padding(
                    padding: const EdgeInsets.only(right: 12),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          unit,
                          style: TextStyle(
                            color: context.sg.textSecondary,
                            fontWeight: FontWeight.w700,
                            fontSize: 13,
                          ),
                        ),
                        const SizedBox(width: 6),
                        Icon(
                          Icons.dialpad_rounded,
                          size: 20,
                          color: context.sg.textMuted,
                        ),
                      ],
                    ),
                  ),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppRadii.sm),
                  ),
                  isDense: true,
                  contentPadding: const EdgeInsets.symmetric(
                    horizontal: 14,
                    vertical: 14,
                  ),
                ),
                child: Text(
                  value.isEmpty ? ' ' : value,
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    letterSpacing: -0.2,
                    color: value.isEmpty
                        ? Colors.transparent
                        : context.sg.textPrimary,
                  ),
                ),
              ),
            );
          },
        );
      },
    );
  }
}

class _SgAmountPadSheet extends StatefulWidget {
  const _SgAmountPadSheet({
    required this.title,
    required this.unit,
    required this.initialValue,
    this.allowDecimal = true,
  });

  final String title;
  final String unit;
  final String initialValue;
  final bool allowDecimal;

  @override
  State<_SgAmountPadSheet> createState() => _SgAmountPadSheetState();
}

class _SgAmountPadSheetState extends State<_SgAmountPadSheet> {
  late String _value;

  @override
  void initState() {
    super.initState();
    _value = widget.initialValue.trim().replaceAll(',', '.');
    if (!widget.allowDecimal) {
      _value = _value.split('.').first;
    }
  }

  void _append(String ch) {
    setState(() {
      if (ch == '.') {
        if (!widget.allowDecimal || _value.contains('.')) return;
      }
      if (_value == '0' && ch != '.') {
        _value = ch;
        return;
      }
      final next = '$_value$ch';
      final normalized = next.replaceAll(',', '.');
      final ok = widget.allowDecimal
          ? RegExp(r'^\d*\.?\d{0,3}$').hasMatch(normalized)
          : RegExp(r'^\d{0,9}$').hasMatch(normalized);
      if (ok) _value = normalized;
    });
  }

  void _backspace() {
    if (_value.isEmpty) return;
    setState(() => _value = _value.substring(0, _value.length - 1));
  }

  @override
  Widget build(BuildContext context) {
    final keys = widget.allowDecimal
        ? const ['1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '0', '⌫']
        : const ['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', '⌫'];
    final p = context.sg;
    return Padding(
      padding: EdgeInsets.only(
        left: 20,
        right: 20,
        top: 12,
        bottom: MediaQuery.paddingOf(context).bottom + 16,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 36,
            height: 4,
            decoration: BoxDecoration(
              color: p.border.withValues(alpha: 0.9),
              borderRadius: BorderRadius.circular(2),
            ),
          ),
          const SizedBox(height: 16),
          Text(
            widget.title,
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              letterSpacing: -0.3,
              color: p.textPrimary,
            ),
          ),
          const SizedBox(height: 14),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(AppRadii.md),
              border: Border.all(
                color: AppColors.primary.withValues(alpha: 0.28),
              ),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    _value.isEmpty ? '0' : _value,
                    style: TextStyle(
                      fontSize: 34,
                      fontWeight: FontWeight.w800,
                      letterSpacing: -0.6,
                      color: _value.isEmpty ? p.textMuted : p.textPrimary,
                    ),
                  ),
                ),
                Text(
                  widget.unit,
                  style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                    color: AppColors.primaryDark,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          GridView.count(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            crossAxisCount: 3,
            mainAxisSpacing: 8,
            crossAxisSpacing: 8,
            childAspectRatio: 1.55,
            children: [
              for (final k in keys)
                if (k.isEmpty)
                  const SizedBox.shrink()
                else
                  FilledButton.tonal(
                    style: FilledButton.styleFrom(
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(AppRadii.sm),
                      ),
                    ),
                    onPressed: () {
                      if (k == '⌫') {
                        _backspace();
                      } else {
                        _append(k);
                      }
                    },
                    child: Text(
                      k,
                      style: const TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
            ],
          ),
          const SizedBox(height: 14),
          SizedBox(
            width: double.infinity,
            height: 48,
            child: FilledButton(
              onPressed: () => Navigator.pop(context, _value),
              child: const Text(
                'Done',
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Search field with a stable controller so provider rebuilds never wipe focus.
class SgSearchField extends StatefulWidget {
  const SgSearchField({
    super.key,
    required this.onChanged,
    this.hintText = 'Search…',
    this.initialQuery = '',
    this.controller,
    this.autofocus = false,
  });

  final ValueChanged<String> onChanged;
  final String hintText;
  final String initialQuery;

  /// Optional external controller (e.g. parent clear-all). If null, owned here.
  final TextEditingController? controller;
  final bool autofocus;

  @override
  State<SgSearchField> createState() => _SgSearchFieldState();
}

class _SgSearchFieldState extends State<SgSearchField> {
  late final TextEditingController _ctrl;
  late final bool _ownsCtrl;
  final _focus = FocusNode();

  @override
  void initState() {
    super.initState();
    _ownsCtrl = widget.controller == null;
    _ctrl =
        widget.controller ?? TextEditingController(text: widget.initialQuery);
  }

  @override
  void dispose() {
    _focus.dispose();
    if (_ownsCtrl) _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final p = context.sg;
    return ListenableBuilder(
      listenable: _ctrl,
      builder: (context, _) {
        final hasQuery = _ctrl.text.trim().isNotEmpty;
        return sgImeSafeTextField(
          controller: _ctrl,
          focusNode: _focus,
          autofocus: widget.autofocus,
          textInputAction: TextInputAction.search,
          onChanged: widget.onChanged,
          decoration: InputDecoration(
            hintText: widget.hintText,
            hintStyle: TextStyle(
              color: p.textMuted,
              fontWeight: FontWeight.w500,
            ),
            prefixIcon: Icon(
              Icons.search_rounded,
              color: p.textSecondary,
              size: 22,
            ),
            suffixIcon: hasQuery
                ? IconButton(
                    tooltip: 'Clear',
                    icon: Icon(Icons.close_rounded, color: p.textSecondary),
                    onPressed: () {
                      _ctrl.clear();
                      widget.onChanged('');
                    },
                  )
                : null,
            filled: true,
            fillColor: p.card,
            border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(AppRadii.md),
              borderSide: BorderSide(color: p.border.withValues(alpha: 0.85)),
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(AppRadii.md),
              borderSide: BorderSide(color: p.border.withValues(alpha: 0.8)),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(AppRadii.md),
              borderSide: const BorderSide(
                color: AppColors.primary,
                width: 1.4,
              ),
            ),
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 14,
              vertical: 13,
            ),
          ),
        );
      },
    );
  }
}
