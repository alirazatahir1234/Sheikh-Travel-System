import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../../../shared/widgets/sg_ui.dart';
import '../data/payments_api.dart';
import '../domain/payment_summary_model.dart';
import '../../../shared/widgets/sg_keyboard_fields.dart';

final paymentSummaryProvider =
    FutureProvider.autoDispose.family<PaymentSummary, int>(
  (ref, tripId) => ref.read(paymentsApiProvider).getSummary(tripId),
);

class CollectPaymentScreen extends ConsumerStatefulWidget {
  const CollectPaymentScreen({super.key, required this.tripId});
  final int tripId;

  @override
  ConsumerState<CollectPaymentScreen> createState() =>
      _CollectPaymentScreenState();
}

class _CollectPaymentScreenState extends ConsumerState<CollectPaymentScreen> {
  final _amountCtrl = TextEditingController();
  final _refCtrl = TextEditingController();
  final _notesCtrl = TextEditingController();
  String _method = 'Cash';
  bool _busy = false;
  bool _seededAmount = false;

  @override
  void dispose() {
    _amountCtrl.dispose();
    _refCtrl.dispose();
    _notesCtrl.dispose();
    super.dispose();
  }

  Future<void> _collect(PaymentSummary summary) async {
    final amount = double.tryParse(_amountCtrl.text.trim()) ?? 0;
    if (amount <= 0) {
      _toast('Enter valid amount');
      return;
    }
    if (_method != 'Cash' && _refCtrl.text.trim().isEmpty) {
      _toast('Reference is required for $_method');
      return;
    }
    setState(() => _busy = true);
    try {
      await ref.read(paymentsApiProvider).collect(
            tripId: widget.tripId,
            amountReceived: amount,
            paymentMethod: _method,
            referenceNumber:
                _refCtrl.text.trim().isEmpty ? null : _refCtrl.text.trim(),
            notes:
                _notesCtrl.text.trim().isEmpty ? null : _notesCtrl.text.trim(),
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Payment submitted successfully'),
          backgroundColor: AppColors.success,
        ),
      );
      Navigator.pop(context, true);
    } catch (e) {
      _toast(ErrorHandler.message(e));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _toast(String msg) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(msg), backgroundColor: AppColors.error),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(paymentSummaryProvider(widget.tripId));
    final p = context.sg;
    return Scaffold(
      backgroundColor: p.surface,
      appBar: AppBar(title: const Text('Collect Payment')),
      body: state.when(
        loading: () => const Center(child: SgBrandLoader()),
        error: (e, _) => Center(child: Text(e.toString())),
        data: (s) {
          if (!_seededAmount && _amountCtrl.text.isEmpty) {
            _seededAmount = true;
            _amountCtrl.text = s.balanceDue.toStringAsFixed(0);
          }
          final fmt = NumberFormat('#,##0');
          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
            children: [
              SgCard(
                padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
                child: Column(
                  children: [
                    _line(context, 'Booking', s.bookingNumber),
                    const SizedBox(height: 8),
                    _line(context, 'Total', 'PKR ${fmt.format(s.totalAmount)}'),
                    const SizedBox(height: 8),
                    _line(context, 'Paid', 'PKR ${fmt.format(s.paidAmount)}'),
                    const SizedBox(height: 10),
                    Divider(height: 1, color: p.divider.withValues(alpha: 0.9)),
                    const SizedBox(height: 10),
                    _line(
                      context,
                      'Due',
                      'PKR ${fmt.format(s.balanceDue)}',
                      emphasize: true,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              SgSheetAmountFormField(
                controller: _amountCtrl,
                label: 'Amount Received',
                unit: 'PKR',
                helperExample: 'e.g. 5000',
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _method,
                decoration: const InputDecoration(labelText: 'Payment Method'),
                items: const ['Cash', 'Card', 'JazzCash', 'EasyPaisa']
                    .map((m) => DropdownMenuItem(value: m, child: Text(m)))
                    .toList(),
                onChanged:
                    _busy ? null : (v) => setState(() => _method = v ?? 'Cash'),
              ),
              const SizedBox(height: 12),
              SgSheetTextFormField(
                controller: _refCtrl,
                label: _method == 'Cash'
                    ? 'Reference (optional)'
                    : 'Reference Number',
              ),
              const SizedBox(height: 12),
              SgSheetTextFormField(
                controller: _notesCtrl,
                label: 'Notes (optional)',
                maxLines: 2,
                textCapitalization: TextCapitalization.sentences,
              ),
              const SizedBox(height: 20),
              SizedBox(
                height: 48,
                child: FilledButton(
                  onPressed: _busy ? null : () => _collect(s),
                  child: _busy
                      ? const SizedBox(
                          height: 22,
                          width: 22,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text(
                          'Collect',
                          style: TextStyle(fontWeight: FontWeight.w700),
                        ),
                ),
              ),
              const SizedBox(height: 10),
              SizedBox(
                height: 48,
                child: OutlinedButton(
                  onPressed: _busy ? null : () => Navigator.pop(context, false),
                  child: const Text(
                    'Skip',
                    style: TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _line(
    BuildContext context,
    String label,
    String value, {
    bool emphasize = false,
  }) {
    final p = context.sg;
    return Row(
      children: [
        Expanded(
          child: Text(
            label,
            style: TextStyle(
              color: p.textSecondary,
              fontSize: 13.5,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
        Text(
          value,
          style: TextStyle(
            fontWeight: FontWeight.w800,
            fontSize: emphasize ? 16 : 14,
            letterSpacing: -0.2,
            color: emphasize ? AppColors.primaryDark : p.textPrimary,
          ),
        ),
      ],
    );
  }
}
