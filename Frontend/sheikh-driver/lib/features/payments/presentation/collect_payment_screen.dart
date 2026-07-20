import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../core/constants/app_theme.dart';
import '../../../core/errors/error_handler.dart';
import '../data/payments_api.dart';
import '../domain/payment_summary_model.dart';

class CollectPaymentScreen extends ConsumerStatefulWidget {
  const CollectPaymentScreen({super.key, required this.tripId});
  final int tripId;

  @override
  ConsumerState<CollectPaymentScreen> createState() => _CollectPaymentScreenState();
}

class _CollectPaymentScreenState extends ConsumerState<CollectPaymentScreen> {
  final _amountCtrl = TextEditingController();
  final _refCtrl = TextEditingController();
  final _notesCtrl = TextEditingController();
  String _method = 'Cash';
  bool _busy = false;

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
            referenceNumber: _refCtrl.text.trim().isEmpty ? null : _refCtrl.text.trim(),
            notes: _notesCtrl.text.trim().isEmpty ? null : _notesCtrl.text.trim(),
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Payment submitted successfully'), backgroundColor: AppColors.success),
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
    final summaryAsync = FutureProvider.autoDispose(
      (ref) => ref.read(paymentsApiProvider).getSummary(widget.tripId),
    );
    final state = ref.watch(summaryAsync);
    return Scaffold(
      appBar: AppBar(title: const Text('Collect Payment')),
      body: state.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text(e.toString())),
        data: (s) {
          if (_amountCtrl.text.isEmpty) {
            _amountCtrl.text = s.balanceDue.toStringAsFixed(0);
          }
          final fmt = NumberFormat('#,##0');
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              _line('Booking', s.bookingNumber),
              _line('Total', 'PKR ${fmt.format(s.totalAmount)}'),
              _line('Paid', 'PKR ${fmt.format(s.paidAmount)}'),
              _line('Due', 'PKR ${fmt.format(s.balanceDue)}'),
              const SizedBox(height: 12),
              TextField(
                controller: _amountCtrl,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(labelText: 'Amount Received'),
              ),
              const SizedBox(height: 8),
              DropdownButtonFormField<String>(
                initialValue: _method,
                decoration: const InputDecoration(labelText: 'Payment Method'),
                items: const ['Cash', 'Card', 'JazzCash', 'EasyPaisa']
                    .map((m) => DropdownMenuItem(value: m, child: Text(m)))
                    .toList(),
                onChanged: _busy ? null : (v) => setState(() => _method = v ?? 'Cash'),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: _refCtrl,
                decoration: InputDecoration(labelText: _method == 'Cash' ? 'Reference (optional)' : 'Reference Number'),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: _notesCtrl,
                maxLines: 2,
                decoration: const InputDecoration(labelText: 'Notes (optional)'),
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: _busy ? null : () => _collect(s),
                child: _busy ? const CircularProgressIndicator() : const Text('Collect'),
              ),
              const SizedBox(height: 8),
              OutlinedButton(
                onPressed: _busy ? null : () => Navigator.pop(context, false),
                child: const Text('Skip'),
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _line(String label, String value) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 2),
        child: Row(
          children: [
            Expanded(child: Text(label, style: const TextStyle(color: AppColors.textSecondary))),
            Text(value, style: const TextStyle(fontWeight: FontWeight.w700)),
          ],
        ),
      );
}
