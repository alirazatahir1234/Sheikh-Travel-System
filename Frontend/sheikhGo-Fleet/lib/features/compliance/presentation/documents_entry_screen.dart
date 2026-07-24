import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../auth/data/auth_repository.dart';
import '../../documents/presentation/documents_screen.dart';
import 'compliance_list_screen.dart';

/// Staff see fleet compliance docs; drivers keep self-service documents.
class DocumentsEntryScreen extends ConsumerWidget {
  const DocumentsEntryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(fleetSessionProvider);
    if (session == null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (session.isDriverOnly) return const DocumentsScreen();
    return const ComplianceListScreen();
  }
}
