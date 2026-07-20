import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/legal/presentation/legal_document_screen.dart';

void main() {
  testWidgets('Privacy policy screen shows title and body', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: LegalDocumentScreen(kind: LegalDocumentKind.privacy),
      ),
    );
    expect(find.text('Privacy Policy'), findsOneWidget);
    expect(find.textContaining('workforce app'), findsOneWidget);
  });

  testWidgets('Terms screen shows title', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: LegalDocumentScreen(kind: LegalDocumentKind.terms),
      ),
    );
    expect(find.text('Terms of Service'), findsOneWidget);
    expect(find.textContaining('authorized driver'), findsOneWidget);
  });
}
